#!/usr/bin/env dotnet
#:property TargetFramework=net10.0
#:property PublishAot=false

// Standalone .NET 10 file-based app. No NuGet packages or compile-time OpenTAP references.
// Run: dotnet run benchmark.cs
// No arguments required. Generates fixtures and benchmarks nearby OpenTAP binaries.
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

try
{
    if (args.Length == 0)
    {
        await DefaultRun.Run();
        return;
    }
    var options = new Options(args);
    switch (options.Command)
    {
        case "generate": Generator.Generate(options); break;
        case "run": await Runner.Run(options); break;
        case "sample": Sample.Run(options); break; // Internal subprocess command.
        case "help": Options.Help(); break;
        default: throw new ArgumentException("Expected generate or run. Use --help for usage.");
    }
}
catch (Exception ex)
{
    while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
    Console.Error.WriteLine(ex);
    Environment.ExitCode = 1;
}

static class DefaultRun
{
    // These are internal calls, not arguments the user needs to supply.
    public static async Task Run([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        var roots = new[] { Directory.GetCurrentDirectory(), Path.GetDirectoryName(source)! };
        string? tap = null;
        foreach (string root in roots)
        {
            for (var dir = new DirectoryInfo(root); dir != null && tap == null; dir = dir.Parent)
            {
                foreach (string candidate in new[] { dir.FullName, Path.Combine(dir.FullName, "bin", "Release"), Path.Combine(dir.FullName, "bin", "Debug") })
                {
                    if (File.Exists(Path.Combine(candidate, "OpenTap.dll")) && File.Exists(Path.Combine(candidate, "OpenTap.Package.dll")))
                    { tap = candidate; break; }
                }
            }
            if (tap != null) break;
        }
        if (tap == null)
            throw new Exception("OpenTAP binaries not found. Run this file from your OpenTAP installation or built repository.");
        string workspace = Path.Combine(Path.GetDirectoryName(source)!, "artifacts");
        Console.WriteLine($"OpenTAP: {tap}");
        Console.WriteLine($"Workspace: {workspace}");
        if (!File.Exists(Path.Combine(workspace, "manifest.json")))
            Generator.Generate(new Options(["generate", "--workspace", workspace]));
        else
            Console.WriteLine("Reusing generated packages.");
        await Runner.Run(new Options(["run", "--workspace", workspace, "--tap-dir", tap, "--modes", "local"]));
    }
}

sealed class Options
{
    public string Command { get; }
    readonly Dictionary<string, string> values = new();
    public Options(string[] args)
    {
        Command = args.Length == 0 || args[0] is "--help" or "-h" ? "help" : args[0];
        for (int i = 1; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) throw new ArgumentException($"Unexpected argument {args[i]}");
            string key = args[i][2..];
            if (key == "keep-deployments") values.Add(key, "true");
            else
            {
                if (++i == args.Length || args[i].StartsWith("--")) throw new ArgumentException($"Missing value for --{key}");
                values.Add(key, args[i]);
            }
        }
        string[] common = ["workspace"];
        string[] allowed = Command switch
        {
            "generate" => [..common, "small-count", "small-kib", "small-files", "large-count", "large-mib", "large-files", "mix-small-count", "mix-large-count", "seed", "random-percent"],
            "run" => [..common, "tap-dir", "label", "repeats", "warmups", "images", "modes", "latency-ms", "mbps", "timeout", "keep-deployments"],
            "sample" => [..common, "tap-dir", "image", "mode", "repository", "target", "cache", "result"],
            _ => []
        };
        foreach (var key in values.Keys)
            if (!allowed.Contains(key)) throw new ArgumentException($"Unknown option --{key}");
    }
    public string Get(string key, string? fallback = null) => values.TryGetValue(key, out var value) ? value : fallback ?? throw new ArgumentException($"--{key} is required");
    public int Int(string key, int fallback, int min = 1, int max = int.MaxValue)
    {
        int value = int.Parse(Get(key, fallback.ToString()), System.Globalization.CultureInfo.InvariantCulture);
        return value >= min && value <= max ? value : throw new ArgumentException($"--{key} must be {min}..{max}");
    }
    public double Number(string key, double fallback)
    {
        double value = double.Parse(Get(key, fallback.ToString(System.Globalization.CultureInfo.InvariantCulture)), System.Globalization.CultureInfo.InvariantCulture);
        return double.IsFinite(value) && value >= 0 ? value : throw new ArgumentException($"--{key} must be finite and nonnegative");
    }
    public string[] List(string key, string fallback, string[] allowed)
    {
        var list = Get(key, fallback).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (list.Length == 0 || list.Distinct().Count() != list.Length || list.Any(s => !allowed.Contains(s)))
            throw new ArgumentException($"--{key} must be a comma-separated selection of {string.Join(',', allowed)}");
        return list;
    }
    public static void Help() => Console.WriteLine("""
        Image deployment benchmark (.NET 10 SDK required; standalone single-file app)

        dotnet run benchmark.cs

        Run from an OpenTAP installation or built repository. Packages are generated
        automatically under artifacts/ beside this file, then all three images are
        benchmarked with local files. No arguments needed.
        Results are saved under artifacts/results/.
        """);
}

static class Data
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    public static string FileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    public static void Write(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Json));
    public static Manifest Load(string root)
    {
        var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Path.Combine(root, "manifest.json"))) ?? throw new Exception("Invalid manifest");
        foreach (var package in manifest.packages.Values)
        {
            SafePath(Path.Combine(root, "repository"), package.archive);
            foreach (var file in package.files)
            {
                SafePath(root, file.path);
                if (file.bytes < 0 || file.sha256.Length != 64) throw new Exception("Invalid payload metadata");
            }
        }
        foreach (var names in manifest.images.Values)
            if (names.Length == 0 || names.Distinct().Count() != names.Length || names.Any(n => !manifest.packages.ContainsKey(n)))
                throw new Exception("Invalid image package list");
        return manifest;
    }
    public static string SafePath(string root, string relative)
    {
        var path = Path.GetFullPath(Path.Combine(root, relative));
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (Path.IsPathRooted(relative) || !path.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new Exception($"Path escapes benchmark directory: {relative}");
        return path;
    }
}
record Manifest(Dictionary<string, PackageInfo> packages, Dictionary<string, string[]> images);
record PackageInfo(string archive, long archive_bytes, PayloadFile[] files);
record PayloadFile(string path, long bytes, string sha256);

static class Generator
{
    public static void Generate(Options o)
    {
        string root = Path.GetFullPath(o.Get("workspace"));
        string manifestPath = Path.Combine(root, "manifest.json");
        if (File.Exists(manifestPath) || Directory.Exists(Path.Combine(root, "repository")))
            throw new Exception("Workspace already has packages. Use a new workspace to change workloads.");
        int smallCount = o.Int("small-count", 100), largeCount = o.Int("large-count", 5);
        int smallFiles = o.Int("small-files", 16), largeFiles = o.Int("large-files", 1);
        long smallSize = (long)o.Int("small-kib", 256) * 1024, largeSize = (long)o.Int("large-mib", 100) * 1024 * 1024;
        int mixSmall = o.Int("mix-small-count", 50, 1, smallCount), mixLarge = o.Int("mix-large-count", 3, 1, largeCount);
        int seed = o.Int("seed", 42, 0), randomPercent = o.Int("random-percent", 75, 0, 100);
        var random = new DeterministicRandom((ulong)seed);
        var packages = new Dictionary<string, PackageInfo>();
        var small = new List<string>(); var large = new List<string>();
        string repo = Path.Combine(root, "repository");
        Directory.CreateDirectory(repo);
        XNamespace ns = "http://opentap.io/schemas/package";
        foreach (var (kind, count, size, fileCount, names) in new[] {
            ("Small", smallCount, smallSize, smallFiles, small), ("Large", largeCount, largeSize, largeFiles, large) })
        {
            for (int index = 0; index < count; index++)
            {
                string name = $"DeployBench{kind}{index:000}", archive = $"{name}.1.0.0.AnyCPU.TapPackage";
                names.Add(name);
                var files = new List<PayloadFile>();
                var metadataFiles = new XElement(ns + "Files");
                var metadata = new XElement(ns + "Package", new XAttribute("Name", name), new XAttribute("Version", "1.0.0"),
                    new XAttribute("OS", "Windows,Linux,MacOS"), new XAttribute("Architecture", "AnyCPU"), new XAttribute("Class", ""),
                    new XElement(ns + "Description", "Synthetic deployment benchmark payload"), metadataFiles);
                using (var zip = ZipFile.Open(Path.Combine(repo, archive), ZipArchiveMode.Create))
                {
                    byte[] buffer = new byte[1024 * 1024];
                    for (int f = 0; f < fileCount; f++)
                    {
                        string path = $"BenchmarkPayload/{name}/file{f:0000}.bin";
                        long length = size / fileCount + (f < size % fileCount ? 1 : 0);
                        metadataFiles.Add(new XElement(ns + "File", new XAttribute("Path", path)));
                        var entry = Entry(zip, path);
                        using var stream = entry.Open();
                        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                        for (long remaining = length; remaining > 0;)
                        {
                            int n = (int)Math.Min(remaining, buffer.Length), randomSize = n * randomPercent / 100;
                            random.Fill(buffer.AsSpan(0, randomSize));
                            buffer.AsSpan(randomSize, n - randomSize).Clear();
                            stream.Write(buffer, 0, n); hash.AppendData(buffer, 0, n); remaining -= n;
                        }
                        files.Add(new(path, length, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()));
                    }
                    using var metaStream = Entry(zip, $"Packages/{name}/package.xml").Open();
                    metadata.Save(metaStream);
                }
                packages.Add(name, new(archive, new FileInfo(Path.Combine(repo, archive)).Length, files.ToArray()));
                Console.WriteLine($"Generated {name}: payload {size:N0} bytes, archive {packages[name].archive_bytes:N0} bytes");
            }
        }
        var images = new Dictionary<string, string[]> {
            ["many-small"] = small.ToArray(), ["few-large"] = large.ToArray(),
            ["mixed"] = small.Take(mixSmall).Concat(large.Take(mixLarge)).ToArray() };
        Data.Write(manifestPath, new {
            format = 1, generator = new { implementation = "dotnet-splitmix64-v1", seed, random_percent = randomPercent,
                small_count = smallCount, small_kib = smallSize / 1024, small_files = smallFiles,
                large_count = largeCount, large_mib = largeSize / (1024 * 1024), large_files = largeFiles,
                mix_small_count = mixSmall, mix_large_count = mixLarge }, packages, images });
        foreach (var (name, names) in images)
            Data.Write(Path.Combine(root, name + ".image.json"), new { Packages = names.Select(n => new { Name = n, Version = "1.0.0" }) });
        Console.WriteLine($"Workspace: {root}");
    }
    static ZipArchiveEntry Entry(ZipArchive zip, string path)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        entry.ExternalAttributes = 0;
        return entry;
    }
    // Explicit PRNG to keep payload bytes identical across OS/.NET versions; not cryptographic.
    sealed class DeterministicRandom(ulong seed)
    {
        ulong state = seed;
        public void Fill(Span<byte> bytes)
        {
            for (int i = 0; i < bytes.Length;)
            {
                ulong z = unchecked(state += 0x9e3779b97f4a7c15UL);
                z = unchecked((z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL);
                z = unchecked((z ^ (z >> 27)) * 0x94d049bb133111ebUL);
                z ^= z >> 31;
                for (int j = 0; j < 8 && i < bytes.Length; j++, i++) { bytes[i] = (byte)z; z >>= 8; }
            }
        }
    }
}

static class Runner
{
    public static async Task Run(Options o)
    {
        string root = Path.GetFullPath(o.Get("workspace")), tap = Path.GetFullPath(o.Get("tap-dir"));
        var manifest = Data.Load(root);
        if (!File.Exists(Path.Combine(tap, "OpenTap.Package.dll")) || !File.Exists(Path.Combine(tap, "OpenTap.dll")))
            throw new Exception("--tap-dir must contain OpenTap.dll and OpenTap.Package.dll");
        string[] images = o.List("images", "many-small,few-large,mixed", manifest.images.Keys.ToArray());
        string[] modes = o.List("modes", "local", ["local", "cold", "warm"]);
        int repeats = o.Int("repeats", 3), warmups = o.Int("warmups", 1, 0), timeout = o.Int("timeout", 1800);
        double latency = o.Number("latency-ms", 0), mbps = o.Number("mbps", 0);
        string label = o.Get("label", "baseline");
        if (label is "." or ".." || label.Length == 0 || label.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || label.Contains('/') || label.Contains('\\'))
            throw new ArgumentException("Label must be a filename component");
        bool keep = o.Get("keep-deployments", "false") == "true";
        string runDir = Path.Combine(root, "results", label + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(runDir);
        Data.Write(Path.Combine(runDir, "environment.json"), new { label, tap_dir = tap,
            latency_ms_per_request = latency, mib_per_second_per_connection = mbps,
            manifest_sha256 = Data.FileHash(Path.Combine(root, "manifest.json")), repeats, warmups,
            harness_runtime = RuntimeInformation.FrameworkDescription });
        await using var server = modes.Any(mode => mode != "local") ? new RepositoryServer(root, manifest, latency, mbps) : null;
        var results = new List<JsonElement>();
        Console.WriteLine($"Results: {runDir}");
        foreach (string image in images)
        foreach (string mode in modes)
        for (int i = -warmups; i < repeats; i++)
        {
            string sample = Path.Combine(runDir, $"{image}-{mode}-{i}");
            Directory.CreateDirectory(sample);
            string target = Path.Combine(sample, "target"), cache = Path.Combine(sample, "cache");
            try
            {
                var start = SelfProcess();
                string[] sampleArgs = ["sample", "--workspace", root, "--tap-dir", tap, "--image", image, "--mode", mode,
                    "--repository", server?.Url ?? "",  "--target", target, "--cache", cache, "--result", Path.Combine(sample, "result.json")];
                foreach (string arg in sampleArgs) start.ArgumentList.Add(arg);
                start.WorkingDirectory = tap;
                start.RedirectStandardOutput = true; start.RedirectStandardError = true;
                using var process = Process.Start(start) ?? throw new Exception("Could not start sample process");
                // Read both streams concurrently, preserving logs even on timeout/failure.
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                bool timedOut = false;
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
                try { await process.WaitForExitAsync(deadline.Token); }
                catch (OperationCanceledException) { timedOut = true; process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
                await File.WriteAllTextAsync(Path.Combine(sample, "deployment.log"), await stdout + "\n--- stderr ---\n" + await stderr);
                if (timedOut || process.ExitCode != 0)
                    throw new Exception($"Deployment {(timedOut ? "timed out" : "failed")}; inspect {Path.Combine(sample, "deployment.log")}");
                var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(Path.Combine(sample, "result.json")))!;
                result["iteration"] = JsonSerializer.SerializeToElement(i); result["warmup"] = JsonSerializer.SerializeToElement(i < 0);
                results.Add(JsonSerializer.SerializeToElement(result));
                Data.Write(Path.Combine(runDir, "samples.json"), results);
                Console.WriteLine($"{image} {mode} #{i}: {result["deploy_seconds"].GetDouble():F3}s (validated)");
            }
            finally
            {
                if (!keep) { Cleanup(target); Cleanup(cache); }
            }
        }
        var summary = new List<object>();
        foreach (string image in images)
        foreach (string mode in modes)
        {
            var times = results.Where(r => r.GetProperty("image").GetString() == image && r.GetProperty("mode").GetString() == mode && !r.GetProperty("warmup").GetBoolean())
                .Select(r => r.GetProperty("deploy_seconds").GetDouble()).Order().ToArray();
            double median = times.Length % 2 == 1 ? times[times.Length / 2] : (times[times.Length / 2 - 1] + times[times.Length / 2]) / 2;
            summary.Add(new { image, mode, samples = times.Length, median_seconds = median, min_seconds = times[0], max_seconds = times[^1] });
            Console.WriteLine($"{image} {mode}: median {median:F3}s (min {times[0]:F3}, max {times[^1]:F3})");
        }
        Data.Write(Path.Combine(runDir, "summary.json"), summary);
        string baselinePath = Path.Combine(root, "baseline.json");
        string workloadHash = Data.FileHash(Path.Combine(root, "manifest.json"));
        if (!File.Exists(baselinePath))
        {
            // CreateNew ensures an existing baseline is never overwritten.
            using var baselineFile = new FileStream(baselinePath, FileMode.CreateNew, FileAccess.Write);
            JsonSerializer.Serialize(baselineFile, new {
                manifest_sha256 = workloadHash, summary, samples = results,
                environment = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(runDir, "environment.json")))
            }, Data.Json);
            Console.WriteLine($"Saved baseline: {baselinePath}");
        }
        else
        {
            var baseline = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(baselinePath));
            if (baseline.GetProperty("manifest_sha256").GetString() != workloadHash)
                Console.WriteLine("Baseline workload differs; skipping comparison. Existing baseline left unchanged.");
            else
            {
                Console.WriteLine($"Compared with baseline: {baselinePath} (negative = faster)");
                foreach (var current in JsonSerializer.SerializeToElement(summary).EnumerateArray())
                {
                    string image = current.GetProperty("image").GetString()!, mode = current.GetProperty("mode").GetString()!;
                    var previous = baseline.GetProperty("summary").EnumerateArray().FirstOrDefault(row =>
                        row.GetProperty("image").GetString() == image && row.GetProperty("mode").GetString() == mode);
                    if (previous.ValueKind == JsonValueKind.Undefined) continue;
                    double before = previous.GetProperty("median_seconds").GetDouble();
                    double now = current.GetProperty("median_seconds").GetDouble();
                    Console.WriteLine($"{image} {mode}: {before:F3}s -> {now:F3}s ({(now / before - 1) * 100:+0.0;-0.0;0.0}%)");
                }
            }
        }
    }
    static ProcessStartInfo SelfProcess()
    {
        string executable = Environment.ProcessPath ?? throw new Exception("Cannot locate benchmark executable");
        var info = new ProcessStartInfo(executable) { UseShellExecute = false };
        // File-based apps normally use an apphost. Also support `dotnet benchmark.dll`.
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        return info;
    }
    static void Cleanup(string path)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); return; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == 4) Console.Error.WriteLine($"Warning: could not clean {path}: {ex.Message}");
                else Thread.Sleep(200);
            }
        }
    }
}

// Minimal loopback HTTP download endpoint, implemented with TcpListener to avoid
// Windows HttpListener URL ACL/admin requirements. One request per connection.
sealed class RepositoryServer : IAsyncDisposable
{
    readonly TcpListener listener = new(IPAddress.Loopback, 0);
    readonly CancellationTokenSource stop = new();
    readonly string root;
    readonly Manifest manifest;
    readonly double latency, mbps;
    readonly Task acceptLoop;
    readonly System.Collections.Concurrent.ConcurrentBag<Task> clients = new();
    public string Url { get; }
    public RepositoryServer(string root, Manifest manifest, double latency, double mbps)
    {
        this.root = root; this.manifest = manifest; this.latency = latency; this.mbps = mbps;
        listener.Start(); Url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        acceptLoop = Accept();
    }
    async Task Accept()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stop.Token);
                clients.Add(Serve(client));
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException && stop.IsCancellationRequested) { }
    }
    async Task Serve(TcpClient client)
    {
        using (client)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            deadline.CancelAfter(TimeSpan.FromMinutes(30));
            var token = deadline.Token;
            try
            {
                var stream = client.GetStream();
                using var reader = new StreamReader(stream, System.Text.Encoding.ASCII, false, 4096, leaveOpen: true);
                string request = await reader.ReadLineAsync(token) ?? "";
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int count = 0; ; count++)
                {
                    string line = await reader.ReadLineAsync(token) ?? "";
                    if (line.Length == 0) break;
                    if (count > 100 || line.Length > 8192) throw new IOException("Oversized headers");
                    int colon = line.IndexOf(':'); if (colon > 0) headers[line[..colon]] = line[(colon + 1)..].Trim();
                }
                var parts = request.Split(' ');
                if (parts.Length < 2 || parts[0] != "GET") { await Error(stream, 400, "Bad Request", token); return; }
                string path = parts[1].Split('?')[0];
                string name = Uri.UnescapeDataString(path.TrimEnd('/').Split('/')[^1]);
                if (!manifest.packages.TryGetValue(name, out var package)) { await Error(stream, 404, "Not Found", token); return; }
                string filePath = Data.SafePath(Path.Combine(root, "repository"), package.archive);
                long size = new FileInfo(filePath).Length, start = 0, end = size - 1;
                bool range = headers.TryGetValue("Range", out string? header);
                if (range)
                {
                    var bounds = header!.StartsWith("bytes=") ? header[6..].Split('-') : [];
                    if (bounds.Length != 2 || !long.TryParse(bounds[0], out start) || start < 0 || start >= size ||
                        (bounds[1].Length > 0 && (!long.TryParse(bounds[1], out end) || end < start)))
                    { await Error(stream, 416, "Range Not Satisfiable", token); return; }
                    end = Math.Min(end, size - 1);
                }
                if (latency > 0) await Task.Delay(TimeSpan.FromMilliseconds(latency), token);
                string response = $"HTTP/1.1 {(range ? "206 Partial Content" : "200 OK")}\r\nContent-Type: application/octet-stream\r\n" +
                    $"Content-Disposition: attachment; filename=\"{package.archive}\"\r\nContent-Length: {end - start + 1}\r\nAccept-Ranges: bytes\r\nConnection: close\r\n" +
                    (range ? $"Content-Range: bytes {start}-{end}/{size}\r\n" : "") + "\r\n";
                await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(response), token);
                await using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 256 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                file.Position = start;
                byte[] buffer = new byte[256 * 1024];
                long remaining = end - start + 1;
                var pacing = Stopwatch.StartNew(); long sent = 0;
                while (remaining > 0)
                {
                    int n = await file.ReadAsync(buffer.AsMemory(0, (int)Math.Min(remaining, buffer.Length)), token);
                    if (n == 0) throw new EndOfStreamException();
                    // Pace before writing, including the last chunk, so small files are throttled too.
                    sent += n;
                    if (mbps > 0)
                    {
                        double delay = sent / (mbps * 1024 * 1024) - pacing.Elapsed.TotalSeconds;
                        if (delay > 0) await Task.Delay(TimeSpan.FromSeconds(delay), token);
                    }
                    await stream.WriteAsync(buffer.AsMemory(0, n), token); remaining -= n;
                }
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException) { }
            catch (Exception ex) { Console.Error.WriteLine($"HTTP server: {ex.Message}"); }
        }
    }
    static Task Error(NetworkStream stream, int status, string text, CancellationToken token) =>
        stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {text}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), token).AsTask();
    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); listener.Stop(); await acceptLoop; await Task.WhenAll(clients); stop.Dispose();
    }
}

static class Sample
{
    public static void Run(Options o)
    {
        string root = Path.GetFullPath(o.Get("workspace")), tap = Path.GetFullPath(o.Get("tap-dir"));
        string target = Path.GetFullPath(o.Get("target")), cache = Path.GetFullPath(o.Get("cache"));
        string mode = o.Get("mode"), imageName = o.Get("image"), repo = o.Get("repository");
        if (mode is not ("local" or "cold" or "warm")) throw new ArgumentException("Invalid mode");
        if (Directory.Exists(target) || Directory.Exists(cache)) throw new Exception("Sample target/cache must not exist");
        Environment.SetEnvironmentVariable("OPENTAP_PACKAGE_CACHE_DIR", cache);
        Directory.CreateDirectory(cache);
        var manifest = Data.Load(root);
        var names = manifest.images[imageName];
        // Resolve assemblies from the supplied installation in this subprocess only.
        // No copy/build of the production binaries, no compile-time OpenTAP dependency.
        var assemblies = Directory.EnumerateFiles(tap, "*.dll", SearchOption.TopDirectoryOnly)
            .Concat(Directory.Exists(Path.Combine(tap, "Dependencies")) ? Directory.EnumerateFiles(Path.Combine(tap, "Dependencies"), "*.dll", SearchOption.AllDirectories) : [] )
            .GroupBy(p => Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        AssemblyLoadContext.Default.Resolving += (context, name) => name.Name != null && assemblies.TryGetValue(name.Name, out var path) ? context.LoadFromAssemblyPath(path) : null;
        var packageAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(tap, "OpenTap.Package.dll"));
        Type defType = packageAssembly.GetType("OpenTap.Package.PackageDef", true)!;
        Type imageType = packageAssembly.GetType("OpenTap.Package.ImageIdentifier", true)!;
        Type sourceType = packageAssembly.GetType("OpenTap.Package.HttpRepositoryPackageDefSource", true)!;
        var setup = Stopwatch.StartNew();
        var packages = Array.CreateInstance(defType, names.Length);
        for (int i = 0; i < names.Length; i++)
        {
            var def = defType.GetMethod("FromPackage", [typeof(string)])!.Invoke(null,
                [Data.SafePath(Path.Combine(root, "repository"), manifest.packages[names[i]].archive)])!;
            if (mode != "local")
            {
                var source = Activator.CreateInstance(sourceType)!;
                sourceType.GetProperty("RepositoryUrl")!.SetValue(source, repo);
                defType.GetProperty("PackageSource")!.SetValue(def, source);
            }
            packages.SetValue(def, i);
        }
        var constructor = imageType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
            [typeof(IEnumerable<>).MakeGenericType(defType), typeof(IEnumerable<string>)], null)
            ?? throw new Exception("ImageIdentifier constructor changed; update benchmark reflection binding");
        var image = constructor.Invoke([packages, new[] { repo }]);
        setup.Stop();
        var cacheTimer = Stopwatch.StartNew();
        if (mode == "warm") imageType.GetMethod("Cache")!.Invoke(image, null);
        cacheTimer.Stop();
        var progress = new SampleProgress();
        var progressEvent = imageType.GetEvent("ProgressUpdate")!;
        progressEvent.AddEventHandler(image, Delegate.CreateDelegate(progressEvent.EventHandlerType!, progress, typeof(SampleProgress).GetMethod("Update")!));
        progress.Timer.Start();
        imageType.GetMethod("Deploy", [typeof(string), typeof(CancellationToken)])!.Invoke(image, [target, CancellationToken.None]);
        progress.Timer.Stop();
        // Validation outside the timed region. Verify installed package names and all payload hashes.
        Type installationType = packageAssembly.GetType("OpenTap.Package.Installation", true)!;
        var installation = Activator.CreateInstance(installationType, [target])!;
        var getPackages = installationType.GetMethods().Single(m => m.Name == "GetPackages" && m.GetParameters().All(p => p.IsOptional));
        var installedDefs = (System.Collections.IEnumerable)getPackages.Invoke(installation, getPackages.GetParameters().Select(p => p.DefaultValue).ToArray())!;
        var installed = installedDefs.Cast<object>().Select(p => (string)defType.GetProperty("Name")!.GetValue(p)!).ToHashSet();
        foreach (string name in names)
        {
            if (!installed.Contains(name)) throw new Exception($"Package not installed: {name}");
            foreach (var file in manifest.packages[name].files)
            {
                string path = Data.SafePath(target, file.path);
                if (!File.Exists(path) || new FileInfo(path).Length != file.bytes || Data.FileHash(path) != file.sha256)
                    throw new Exception($"Missing, truncated, or corrupt payload: {path}");
            }
        }
        Data.Write(o.Get("result"), new {
            image = imageName, mode, package_count = names.Length,
            payload_bytes = names.Sum(n => manifest.packages[n].files.Sum(f => f.bytes)),
            archive_bytes = names.Sum(n => manifest.packages[n].archive_bytes),
            deploy_seconds = progress.Timer.Elapsed.TotalSeconds, before_install_seconds = progress.InstallStart,
            install_seconds = progress.InstallStart.HasValue ? progress.Timer.Elapsed.TotalSeconds - progress.InstallStart.Value : (double?)null,
            setup_seconds = setup.Elapsed.TotalSeconds, cache_preparation_seconds = mode == "warm" ? cacheTimer.Elapsed.TotalSeconds : 0,
            runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(), processors = Environment.ProcessorCount,
            package_assembly = packageAssembly.Location, package_version = packageAssembly.GetName().Version?.ToString(),
            package_assembly_sha256 = Data.FileHash(packageAssembly.Location), utc = DateTime.UtcNow, validated = true });
        Console.WriteLine($"{imageName} {mode}: {progress.Timer.Elapsed.TotalSeconds:F3}s (validated)");
    }
}
sealed class SampleProgress
{
    public Stopwatch Timer { get; } = new();
    public double? InstallStart { get; private set; }
    public void Update(int percent, string message)
    {
        if (message == "Installing packages.") InstallStart ??= Timer.Elapsed.TotalSeconds;
    }
}
