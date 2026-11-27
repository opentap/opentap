using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

// Native subset of actions/upload-artifact 043fb46d1a93c77aae656e7c1c64a875d1fc6a0a
// (@actions/artifact 6.2.0): named ZIP artifacts, Twirp create/finalize and Azure
// block blobs. No Node uploader, Azure SDK, PAT, or GITHUB_TOKEN is involved.
// Backend IDs come from the runner-issued JWT scope, not public run/job IDs.
internal sealed record GitHubArtifact(string Name, string[] Files, string RootDirectory);
internal sealed record GitHubArtifactResult(string Name, string Id, long Size, string Digest);

internal sealed class GitHubArtifactUploader : IDisposable
{
    const int ChunkSize = 8 * 1024 * 1024;
    const string Service = "/twirp/github.actions.results.api.v1.ArtifactService/";
    readonly HttpClient client;
    readonly Uri results;
    readonly string token, runId, jobId;
    readonly int retention;
    readonly string temporaryDirectory;

    public GitHubArtifactUploader(string temporaryDirectory, int retentionDays, HttpMessageHandler? transport = null)
    {
        token = Required("ACTIONS_RUNTIME_TOKEN");
        results = new Uri(new Uri(Required("ACTIONS_RESULTS_URL")).GetLeftPart(UriPartial.Authority));
        if (results.Scheme != "https") throw new InvalidOperationException("GitHub artifact service requires HTTPS.");
        // Decode claims only to locate the backend IDs. GitHub validates the JWT
        // signature when authorizing RPCs; we do not authenticate a user locally.
        var parts = token.Split('.');
        if (parts.Length != 3) throw new InvalidOperationException("Invalid artifact runtime token.");
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
        using var jwt = JsonDocument.Parse(Convert.FromBase64String(payload));
        var scope = jwt.RootElement.GetProperty("scp").GetString()!.Split(' ')
            .Select(value => value.Split(':')).SingleOrDefault(value => value.Length == 3 && value[0] == "Actions.Results")
            ?? throw new InvalidOperationException("Artifact runtime token has no Actions.Results scope.");
        runId = scope[1]; jobId = scope[2];
        if (retentionDays < 1) throw new ArgumentOutOfRangeException(nameof(retentionDays));
        retention = int.TryParse(Environment.GetEnvironmentVariable("GITHUB_RETENTION_DAYS"), out var max) && max > 0
            ? Math.Min(retentionDays, max) : retentionDays;
        this.temporaryDirectory = temporaryDirectory;
        Directory.CreateDirectory(temporaryDirectory);
        // Do not follow redirects with a bearer token or a signed blob URL.
        client = new HttpClient(transport ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KS8400-native-artifacts/1.0");
    }

    public async Task<GitHubArtifactResult> UploadAsync(GitHubArtifact artifact, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(artifact.Name) || artifact.Name.IndexOfAny("\" :<>|*?\\/\r\n".ToCharArray()) >= 0)
            throw new ArgumentException("Invalid GitHub artifact name.");
        var zipPath = Path.Combine(temporaryDirectory, Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            var root = Path.GetFullPath(artifact.RootDirectory);
            // Build one ZIP on disk, not in memory. Retrying HTTP requests reuses
            // exactly the same bytes and hash; directories/symlinks are excluded.
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var source in artifact.Files)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var full = Path.GetFullPath(source);
                    var relative = Path.GetRelativePath(root, full).Replace('\\', '/');
                    if (relative == ".." || relative.StartsWith("../") || Path.IsPathRooted(relative) || !names.Add(relative))
                        throw new ArgumentException("Artifact file is outside its root or duplicates a ZIP entry.");
                    if (!File.Exists(full) || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Artifact input must be an existing regular file: " + full);
                    zip.CreateEntryFromFile(full, relative, CompressionLevel.NoCompression);
                }
                if (names.Count == 0) throw new ArgumentException("Artifact has no files.");
            }
            // Phase 1: register the named artifact. The hash covers the actual
            // uploaded ZIP bytes, not the original package/installer inside it.
            await using var stream = File.OpenRead(zipPath);
            var digest = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellation)).ToLowerInvariant();
            stream.Position = 0;
            using var created = await RpcAsync("CreateArtifact", new
            {
                workflow_run_backend_id = runId, workflow_job_run_backend_id = jobId, name = artifact.Name,
                // Protobuf JSON encodes StringValue wrappers as plain strings.
                version = 7, mime_type = "application/zip",
                expires_at = DateTimeOffset.UtcNow.AddDays(retention).ToString("O", CultureInfo.InvariantCulture)
            }, cancellation);
            CheckOk(created, "CreateArtifact");
            var signed = new Uri(ResponseProperty(created, "signed_upload_url", "signedUploadUrl").GetString()!);
            if (signed.Scheme != "https") throw new InvalidOperationException("Artifact blob URL requires HTTPS.");
            // Phase 2: the create RPC returns a SAS URL authorizing blob writes.
            // Never attach the GitHub bearer token to these Azure requests.
            // Upload blocks sequentially per artifact; the graph runs artifacts
            // concurrently. One 8 MiB buffer is retained until its retries finish.
            var blockIds = new List<string>();
            var buffer = new byte[ChunkSize];
            for (int index = 0; stream.Position < stream.Length; index++)
            {
                var length = await stream.ReadAtLeastAsync(buffer, (int)Math.Min(buffer.Length, stream.Length - stream.Position), false, cancellation);
                // Azure requires equal-length Base64 block IDs. Keep their order
                // separately: it is the commit list, not the ID, that orders bytes.
                var id = Convert.ToBase64String(Encoding.ASCII.GetBytes(index.ToString("D8", CultureInfo.InvariantCulture)));
                blockIds.Add(id);
                var uri = BlobUri(signed, "comp=block&blockid=" + Uri.EscapeDataString(id));
                await RequestAsync(() =>
                {
                    var request = BlobRequest(uri);
                    request.Content = new ByteArrayContent(buffer, 0, length);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    return request;
                }, "Upload artifact block", cancellation);
            }
            // Staged blocks do not form a readable blob until this ordered list
            // is committed. Latest permits repeating a PUT of the same block ID.
            var blockList = new XElement("BlockList", blockIds.Select(id => new XElement("Latest", id))).ToString(SaveOptions.DisableFormatting);
            await RequestAsync(() =>
            {
                var request = BlobRequest(BlobUri(signed, "comp=blocklist"));
                request.Headers.TryAddWithoutValidation("x-ms-blob-content-type", "application/zip");
                request.Content = new StringContent(blockList, Encoding.UTF8, "application/xml");
                return request;
            }, "Commit artifact blocks", cancellation);
            // Phase 3: only a finalized artifact appears as a completed GitHub
            // download. The service receives the committed ZIP size and digest.
            using var finalized = await RpcAsync("FinalizeArtifact", new
            {
                workflow_run_backend_id = runId, workflow_job_run_backend_id = jobId, name = artifact.Name,
                size = stream.Length.ToString(CultureInfo.InvariantCulture), hash = "sha256:" + digest
            }, cancellation);
            CheckOk(finalized, "FinalizeArtifact");
            return new(artifact.Name, ResponseProperty(finalized, "artifact_id", "artifactId").ToString(), stream.Length, digest);
        }
        finally { if (File.Exists(zipPath)) File.Delete(zipPath); }
    }

    async Task<JsonDocument> RpcAsync(string method, object payload, CancellationToken cancellation)
    {
        // The pinned toolkit explicitly uses useProtoFieldName: true: Twirp
        // requests must use snake_case protobuf field names, not C# camelCase.
        var json = JsonSerializer.Serialize(payload);
        var body = await RequestAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(results, Service + method));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return request;
        }, method, cancellation);
        return JsonDocument.Parse(body);
    }

    async Task<string> RequestAsync(Func<HttpRequestMessage> create, string operation, CancellationToken cancellation)
    {
        // Retry the individual RPC/block/commit, not the entire artifact creation
        // sequence. Recreate and dispose request content on every attempt, keeping
        // the same block bytes/IDs. Authentication and other permanent 4xx errors
        // fail immediately; 408/429/5xx and transport timeouts get five attempts.
        for (int attempt = 0; ; attempt++)
        {
            TimeSpan delay = TimeSpan.FromSeconds(Math.Min(30, 2 << attempt));
            try
            {
                using var request = create();
                using var response = await client.SendAsync(request, cancellation);
                if (response.IsSuccessStatusCode) return await response.Content.ReadAsStringAsync(cancellation);
                bool transient = response.StatusCode == HttpStatusCode.RequestTimeout || (int)response.StatusCode == 429 || (int)response.StatusCode >= 500;
                if (!transient || attempt >= 4)
                    // Never expose response bodies or URLs: these may contain SAS tokens.
                    throw new InvalidOperationException($"{operation} failed: HTTP {(int)response.StatusCode}.");
                var retry = response.Headers.RetryAfter;
                delay = retry?.Delta ?? (retry?.Date is { } date ? date - DateTimeOffset.UtcNow : delay);
                delay = TimeSpan.FromSeconds(Math.Clamp(delay.TotalSeconds, 0, 120));
            }
            catch (HttpRequestException) when (attempt < 4) { }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested && attempt < 4) { }
            catch (HttpRequestException) { throw new InvalidOperationException(operation + " failed: network error."); }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { throw new InvalidOperationException(operation + " timed out."); }
            await Task.Delay(delay, cancellation);
        }
    }

    static HttpRequestMessage BlobRequest(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, uri);
        request.Headers.TryAddWithoutValidation("x-ms-version", "2023-11-03");
        return request;
    }
    static Uri BlobUri(Uri signed, string query) => new(signed.AbsoluteUri + (string.IsNullOrEmpty(signed.Query) ? "?" : "&") + query);
    static JsonElement ResponseProperty(JsonDocument response, string protoName, string jsonName) =>
        response.RootElement.TryGetProperty(protoName, out var value) ? value : response.RootElement.GetProperty(jsonName);

    static void CheckOk(JsonDocument response, string operation)
    {
        if (!response.RootElement.GetProperty("ok").GetBoolean()) throw new InvalidOperationException(operation + " was not accepted by GitHub.");
    }
    static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value : throw new InvalidOperationException("Missing " + name + ". Run the artifact-runtime workflow bridge first.");
    public void Dispose() => client.Dispose();
}
