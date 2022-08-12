//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using OpenTap.Authentication;
using Tap.Shared;

namespace OpenTap.Package
{
    /// <summary>
    /// Implements a IPackageRepository that queries a server for OpenTAP packages via http/https.
    /// </summary>
    public class HttpPackageRepository : IPackageRepository, IPackageDownloadProgress
    {
        // from Stream.cs: pick a value that is the largest multiple of 4096 that is still smaller than the large object heap threshold (85K).
        private const int _DefaultCopyBufferSize = 81920;

        private static TraceSource log = Log.CreateSource("HttpPackageRepository");
        private const string ApiVersion = "3.0";
        private VersionSpecifier MinRepoVersion = new VersionSpecifier(3, 0, 0, "", "", VersionMatchBehavior.AnyPrerelease | VersionMatchBehavior.Compatible);
        private string defaultUrl;
        private HttpClient client;
        private HttpClient HttpClient => client ?? (client = GetHttpClient(Url));
        private static HttpClient GetHttpClient(string url)
        {
            var httpClient = AuthenticationSettings.Current.GetClient(null, true);
            httpClient.DefaultRequestHeaders.Add(HttpRequestHeader.Accept.ToString(), "application/xml");
            return httpClient;
        }

        /// <summary>
        /// If true, most warnings will be logged as debug messages
        /// </summary>
        public bool IsSilent;
        private SemanticVersion _version;

        bool IsInError() => _version == null && nextUpdateAt > DateTime.Now;

        /// <summary>
        /// Get or set the version of the repository
        /// </summary>
        public SemanticVersion Version
        {
            get
            {
                if (_version == null)
                    CheckRepoApiVersion();

                return _version;
            }
            private set
            {
                _version = value;
            }
        }

        /// <summary>
        /// Initialize a http repository with the given URL
        /// </summary>
        /// <param name="url"></param>

        public HttpPackageRepository(string url)
        {
            Url = url.TrimEnd('/');
            defaultUrl = this.Url;

            // Get the users Uniquely generated id
            var id = GetUserId();

            string installDir = ExecutorClient.ExeDir;
            UpdateId = String.Format("{0:X8}{1:X8}", MurMurHash3.Hash(id), MurMurHash3.Hash(installDir));
        }

        Action<string, long, long> IPackageDownloadProgress.OnProgressUpdate { get; set; }
        internal static string GetUserId()
        {
            var idPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), "OpenTap", "OpenTapGeneratedId");
            string id = default(Guid).ToString(); // 00000000-0000-0000-0000-000000000000

            try
            {
                if (File.Exists(idPath))
                    id = File.ReadAllText(idPath);
                else
                {
                    id = Guid.NewGuid().ToString();
                    if (Directory.Exists(Path.GetDirectoryName(idPath)) == false)
                        Directory.CreateDirectory(Path.GetDirectoryName(idPath));
                    File.WriteAllText(idPath, id);
                }
            }
            catch (Exception e)
            {
                log.Error("Could not read user id.");
                log.Debug(e);
            }

            return id;
        }

        async Task DoDownloadPackage(PackageDef package, FileStream fileStream, CancellationToken cancellationToken)
        {

            try
            {
                var hc = HttpClient;
                {
                    HttpResponseMessage response = null;
                    hc.DefaultRequestHeaders.Add("OpenTAP", PluginManager.GetOpenTapAssembly().SemanticVersion.ToString());

                    var totalSize = -1L;
                    // this retry loop is to robustly to download the package even if the connection is intermittently lost
                    // to test, try
                    // - Switching network interface
                    // - Entering into airplane mode
                    // - Enabling / disabling a VPN connection
                    // It should try to continue for a while unless the cancellation token signals to stop.
                    int maxRetries = 60;
                    for (int retry = 0; retry < maxRetries; retry++)
                    {
                        hc.DefaultRequestHeaders.Range = RangeHeaderValue.Parse($"bytes={fileStream.Position}-");

                        try
                        {
                            if (package.PackageSource is HttpRepositoryPackageDefSource httpSource && string.IsNullOrEmpty(httpSource.DirectUrl) == false)
                            {
                                if (retry == 0)
                                    log.Info($"Downloading package directly from: '{httpSource.DirectUrl}'.");
                                var message = new HttpRequestMessage(HttpMethod.Get, new Uri(httpSource.DirectUrl));

                                try
                                {
                                    response = await hc.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                                    if (response.IsSuccessStatusCode == false)
                                        throw new Exception($"Request to '{httpSource.DirectUrl}' failed with status code: {response.StatusCode}.");
                                }
                                catch (Exception e)
                                {
                                    log.Warning($"Could not download package directly from: '{httpSource.DirectUrl}'. Downloading package normally.");
                                    log.Debug(e);
                                    response = await hc.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                                }
                            }
                            else
                            {
                                var message = new HttpRequestMessage(HttpMethod.Get,
                                    Url + "/" + ApiVersion + "/DownloadPackage" +
                                          $"/{Uri.EscapeDataString(package.Name)}" +
                                          $"?version={Uri.EscapeDataString(package.Version.ToString())}" +
                                          $"&os={Uri.EscapeDataString(package.OS)}" +
                                          $"&architecture={Uri.EscapeDataString(package.Architecture.ToString())}");
                                response = await hc.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                            }

                            if (totalSize < 0)
                                totalSize = response.Content.Headers.ContentLength ?? 1;

                            // Download the package
                            using (var responseStream = await response.Content.ReadAsStreamAsync())
                            {
                                if (response.IsSuccessStatusCode == false)
                                    throw new HttpRequestException($"The download request failed with {response.StatusCode}.");

                                var task = responseStream.CopyToAsync(fileStream, _DefaultCopyBufferSize, cancellationToken);
                                await ConsoleUtils.ReportProgressTillEndAsync(task, "Downloading",
                                    () => fileStream.Position,
                                    () => totalSize,
                                    (header, pos, len) =>
                                    {
                                        ConsoleUtils.printProgress(header, pos, len);
                                        (this as IPackageDownloadProgress).OnProgressUpdate?.Invoke(header, pos, len);
                                    });

                            }

                            break;
                        }
                        catch (Exception ex)
                        {
                            if (ex is IOException)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(1));
                                continue;
                            }
                            if (ex is HttpRequestException)
                            {
                                // This occurs if the initial connection cannot be made.
                                // usually during transitioning to/from VPN connections.
                                await Task.Delay(TimeSpan.FromSeconds(1));
                                continue;
                            }

                            if (response != null)
                            {
                                // The connection was broken while a http request was active.
                                // If the error is transient or we got partial data we can try continuing.

                                var code = response.StatusCode;
                                bool isError = response.IsSuccessStatusCode == false;
                                response.Dispose();
                                response = null;

                                // PartialContent usually happens when 'ex' is IOException.

                                if (code == HttpStatusCode.PartialContent || (isError && HttpUtils.TransientStatusCode(code)))
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(1));
                                    continue;
                                }
                            }

                            if (cancellationToken.IsCancellationRequested == false)
                                log.Error($"Failed to download package {package.Name} from {Url}.");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!(ex is TaskCanceledException))
                {
                    throw;
                }
                log.Error(ex);
            }
        }

        private string downloadPackagesString(string args, string data = null, string contentType = null, string accept = null)
        {
            string xmlText = null;
            try
            {
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, Url + args);
                httpRequestMessage.Headers.Add("Accept", accept ?? "application/xml");
                httpRequestMessage.Headers.Add("OpenTAP", PluginManager.GetOpenTapAssembly().SemanticVersion.ToString());
                if (data != null)
                {
                    httpRequestMessage.Method = HttpMethod.Post;
                    httpRequestMessage.Content = new StringContent(data);
                }
                var response = HttpClient.SendAsync(httpRequestMessage).GetAwaiter().GetResult();
                xmlText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                if (ex is WebException)
                    CheckRepoApiVersion();

                var exception = new WebException("Error communicating with repository at '" + defaultUrl + "'.", ex);

                if (!IsSilent)
                    log.Warning("Error communicating with repository at '{0}' : {1}.", defaultUrl, ex.Message);
                else
                    log.Debug(exception);

                throw;
            }
            return xmlText;
        }

        // The value indicates the next time at which the repo should be tried connected to.
        DateTime nextUpdateAt = DateTime.MinValue;

        static readonly TimeSpan updateRepoVersionHoldOff = TimeSpan.FromSeconds(60);
        readonly object updateVersionLock = new object();
        private void CheckRepoApiVersion()
        {
            string tryDownload(string url)
            {
                try
                {
                    return HttpClient.GetStringAsync(url).Result;
                }
                catch (AggregateException ae)
                {
                    throw ae.InnerException;
                }
            }

            lock (updateVersionLock)
            {
                if (IsInError())
                    return;

                try
                {
                    // Check specific version
                    string data = null;
                    try
                    {
                        data = tryDownload($"{Url}/{ApiVersion}/version");
                    }
                    catch (HttpRequestException ex)
                    {
                        log.Debug("HTTP Exception {0}", ex);
                    }

                    if (string.IsNullOrEmpty(data))
                    {
                        // Url does not exists
                        if (tryDownload(Url) == null)
                            throw new WebException($"Unable to connect to '{defaultUrl}'.");

                        // Check old repo
                        if (tryDownload($"{Url}/2.0/version") != null)
                            throw new NotSupportedException(
                                $"The repository '{defaultUrl}' is only compatible with TAP 8.x or ealier.");
                        throw new NotSupportedException($"'{defaultUrl}' is not a package repository.");
                    }

                    var reader = XmlReader.Create(new StringReader(data));
                    var serializer = new XmlSerializer(typeof(string));
                    if (serializer.CanDeserialize(reader) == false)
                        throw new NotSupportedException($"'{defaultUrl}' is not a package repository.");
                    var version = serializer.Deserialize(reader) as string;
                    if (SemanticVersion.TryParse(version, out _version) &&
                        MinRepoVersion.IsCompatible(_version) == false)
                        throw new NotSupportedException($"The repository '{defaultUrl}' is not supported.",
                            new Exception(
                                $"Repository version '{Version}' is not compatible with min required version '{MinRepoVersion}'."));
                }
                catch
                {
                    log.Warning("Unable to connect to: {0}", Url);
                }
                nextUpdateAt = DateTime.Now + updateRepoVersionHoldOff;
            }
        }

        PackageDef[] packagesFromXml(string xmlText)
        {
            try
            {
                if (string.IsNullOrEmpty(xmlText) || xmlText == "null") return new PackageDef[0];
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xmlText)))
                {
                    var packages = PackageDef.ManyFromXml(stream).ToArray();
                    packages.ForEach(p =>
                    {
                        p.PackageSource = new HttpRepositoryPackageDefSource
                        {
                            RepositoryUrl = Url
                        };
                    });

                    return packages;
                }
            }
            catch (XmlException ex)
            {
                if (!IsSilent)
                    log.Warning("Invalid xml from package repository at '{0}'.", defaultUrl);
                else
                    log.Debug("Invalid xml from package repository at '{0}'.", defaultUrl);
                log.Debug(ex);
                log.Debug("Redirected url '{0}'", Url);
                log.Debug(xmlText);
            }
            catch (Exception ex)
            {
                if (!IsSilent)
                    log.Warning("Error reading from package repository at '{0}'.", defaultUrl);
                else
                    log.Debug("Error reading from package repository at '{0}'.", defaultUrl);
                log.Debug(ex);
                log.Debug("Redirected url '{0}'", Url);
            }
            return new PackageDef[0];
        }

        private PackageDef[] ConvertToPackageDef(IPackageIdentifier[] packages)
        {
            return packages.Select(p => new PackageDef()
            {
                Name = p.Name,
                Version = p.Version,
                Architecture = p.Architecture,
                OS = p.OS
            }).ToArray();
        }

        private IPackageIdentifier[] CheckCompatibleWith(IPackageIdentifier[] compatibleWith)
        {
            if (compatibleWith == null)
                return null;

            var list = compatibleWith.ToList();

            var openTap = compatibleWith.FirstOrDefault(p => p.Name == "OpenTAP");
            if (openTap != null)
            {
                list.AddRange(new[]
                {
                    new PackageIdentifier("Tap", openTap.Version, openTap.Architecture, openTap.OS),
                    new PackageIdentifier("TAP Base", openTap.Version, openTap.Architecture, openTap.OS)
                });
            }

            return list.ToArray();
        }

        #region IPackageRepository Implementation

        /// <summary>
        /// Get the URL of the repository
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Download a package to a specific destination
        /// </summary>
        /// <param name="package"></param>
        /// <param name="destination"></param>
        /// <param name="cancellationToken"></param>
        public void DownloadPackage(IPackageIdentifier package, string destination, CancellationToken cancellationToken)
        {
            var tmpPath = destination + "." + Guid.NewGuid().ToString();
            //Use DeleteOnClose to auto-magically remove the file when the stream or application is closed.
            using (var tmpFile = new FileStream(tmpPath, FileMode.Create, FileAccess.ReadWrite,
                FileShare.Delete | FileShare.Read, 4096, FileOptions.DeleteOnClose))
            {

                try
                {
                    var packageDef = package as PackageDef ?? new PackageDef
                    {
                        Name = package.Name,
                        Version = package.Version,
                        Architecture = package.Architecture,
                        OS = package.OS
                    };

                    DoDownloadPackage(packageDef, tmpFile, cancellationToken).Wait(cancellationToken);

                    if (cancellationToken.IsCancellationRequested == false)
                    {
                        tmpFile.Flush();
                        File.Delete(destination);
                        File.Copy(tmpFile.Name, destination);
                    }
                }
                catch (Exception)
                {
                    if (cancellationToken.IsCancellationRequested == false)
                        log.Warning("Download failed.");
                    throw;
                }
                finally
                {
                    File.Delete(tmpFile.Name);
                }
            }
        }

        /// <summary>
        /// Get the names of the available packages in the repository
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="compatibleWith"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public string[] GetPackageNames(CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith)
        {
            if (IsInError()) return Array.Empty<string>();
            string response;

            var arg = string.Format("/{0}/GetPackageNames", ApiVersion);
            if (compatibleWith == null || compatibleWith.Length == 0)
                response = downloadPackagesString(arg);
            else
            {
                using (Stream stream = new MemoryStream())
                {
                    compatibleWith = CheckCompatibleWith(compatibleWith);
                    PackageDef.SaveManyTo(stream, ConvertToPackageDef(compatibleWith));
                    stream.Seek(0, 0);
                    string data = new StreamReader(stream).ReadToEnd();

                    cancellationToken.ThrowIfCancellationRequested();

                    response = downloadPackagesString(arg, data, "application/xml");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(response)))
                using (var tr = new StreamReader(ms))
                {
                    var root = XElement.Load(tr);
                    return root.Nodes().OfType<XElement>().Select(e => e.Value).ToArray();
                }
            }
            catch (XmlException)
            {
                log.Debug("Redirected url '{0}'", Url);
                log.Debug(response);

                throw new Exception($"Invalid xml from package repository at '{defaultUrl}'.");
            }
        }

        /// <summary>
        /// Get the names of the available packages in the repository with the specified class
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public string[] GetPackageNames(string @class, CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith)
        {
            if (IsInError()) return Array.Empty<string>();
            string response;

            var arg = string.Format("/{0}/GetPackageNames?class={1}", ApiVersion, Uri.EscapeDataString(@class));
            if (compatibleWith == null || compatibleWith.Length == 0)
                response = downloadPackagesString(arg);
            else
            {
                using (Stream stream = new MemoryStream())
                {
                    compatibleWith = CheckCompatibleWith(compatibleWith);
                    PackageDef.SaveManyTo(stream, ConvertToPackageDef(compatibleWith));
                    stream.Seek(0, 0);
                    string data = new StreamReader(stream).ReadToEnd();

                    cancellationToken.ThrowIfCancellationRequested();

                    response = downloadPackagesString(arg, data, "application/xml");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(response)))
                using (var tr = new StreamReader(ms))
                {
                    var root = XElement.Load(tr);
                    return root.Nodes().OfType<XElement>().Select(e => e.Value).ToArray();
                }
            }
            catch (XmlException)
            {
                log.Debug("Redirected url '{0}'", Url);
                log.Debug(response);

                throw new Exception($"Invalid xml from package repository at '{defaultUrl}'.");
            }
        }

        /// <summary>
        /// Get the available versions of packages with name 'packageName' and optionally compatible with a list of packages
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="compatibleWith"></param>
        /// <returns></returns>
        public PackageVersion[] GetPackageVersions(string packageName, CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith)
        {
            // force update version to check for errors.
            Version?.ToString();
            if (IsInError()) return Array.Empty<PackageVersion>();
            string response;
            string arg = string.Format("/{0}/GetPackageVersions/{1}", ApiVersion, Uri.EscapeDataString(packageName));

            if (compatibleWith == null || compatibleWith.Length == 0)
                response = downloadPackagesString(arg);
            else
            {
                using (Stream stream = new MemoryStream())
                {
                    compatibleWith = CheckCompatibleWith(compatibleWith);
                    PackageDef.SaveManyTo(stream, ConvertToPackageDef(compatibleWith));
                    stream.Seek(0, 0);
                    string data = new StreamReader(stream).ReadToEnd();

                    cancellationToken.ThrowIfCancellationRequested();

                    response = downloadPackagesString(arg, data, "application/xml");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            var pkgs = new TapSerializer().DeserializeFromString(response, type: TypeData.FromType(typeof(PackageVersion[]))) as PackageVersion[];
            pkgs.AsParallel().ForAll(p => p.Name = packageName);
            return pkgs;
        }

        /// <summary>
        /// Get the available versions of packages matching 'package' and optionally compatible with a list of packages
        /// </summary>
        /// <param name="package"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="compatibleWith"></param>
        /// <returns></returns>
        public PackageDef[] GetPackages(PackageSpecifier package, CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith)
        {
            if (IsInError()) return Array.Empty<PackageDef>();
            List<string> reqs = new List<string>();
            var endpoint = "/GetPackages";

            if (!string.IsNullOrWhiteSpace(package.Name)) endpoint = "/GetPackage/" + Uri.EscapeDataString(package.Name);

            if (!string.IsNullOrEmpty(package.Version.ToString()))
                reqs.Add(string.Format("version={0}", Uri.EscapeDataString(package.Version.ToString())));
            if (!string.IsNullOrWhiteSpace(package.OS))
                reqs.Add(string.Format("os={0}", Uri.EscapeDataString(package.OS)));
            if (package.Architecture != CpuArchitecture.AnyCPU)
                reqs.Add(string.Format("architecture={0}", Uri.EscapeDataString(package.Architecture.ToString())));

            // Check if package dependencies are compatible
            compatibleWith = CheckCompatibleWith(compatibleWith);
            foreach (var packageIdentifier in compatibleWith)
                reqs.Add(string.Format("compatibleWith={0}", Uri.EscapeDataString($"{packageIdentifier.Name}:{packageIdentifier.Version}")));


            if (reqs.Any())
                endpoint += "?" + string.Join("&", reqs);

            cancellationToken.ThrowIfCancellationRequested();

            return packagesFromXml(downloadPackagesString("/" + ApiVersion + endpoint));
        }

        /// <summary>
        /// Get Client ID
        /// </summary>
        public string UpdateId;

        private string PackageNameHash(string packageName)
        {
            using (System.Security.Cryptography.SHA256 algo = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = algo.ComputeHash(Encoding.UTF8.GetBytes(packageName));
                return BitConverter.ToString(hash).Replace("-", "");

            }
        }

        /// <summary>
        /// Query the repository for updated versions of specified packages
        /// </summary>
        /// <param name="packages"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public PackageDef[] CheckForUpdates(IPackageIdentifier[] packages, CancellationToken cancellationToken)
        {
            if (IsInError()) return Array.Empty<PackageDef>();
            List<PackageDef> latestPackages = new List<PackageDef>();
            bool tempSilent = IsSilent;
            IsSilent = true;

            try
            {
                string response;

                using (Stream stream = new MemoryStream())
                {
                    PackageDef.SaveManyTo(stream, packages.Select(p => new PackageDef()
                    {
                        Name = PackageNameHash(p.Name),
                        Version = p.Version,
                        Architecture = p.Architecture,
                        OS = p.OS
                    }));

                    stream.Seek(0, 0);
                    string data = new StreamReader(stream).ReadToEnd();

                    string arg = string.Format("/{0}/CheckForUpdates?name={1}", ApiVersion, UpdateId);
                    response = downloadPackagesString(arg, data);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (response != null)
                    latestPackages = packagesFromXml(response).ToList();

                cancellationToken.ThrowIfCancellationRequested();
            }
            catch
            {
                log.Debug("Could not check for updates from package repository at '{0}'.", defaultUrl);
            }

            IsSilent = tempSilent;
            return latestPackages.ToArray();
        }
        #endregion

        /// <summary>
        /// Send the GraphQL query string to the repository.
        /// </summary>
        /// <param name="query">A GraphQL query string</param>
        /// <returns>A JObject containing the GraphQL response</returns>
        [Obsolete("Please use SendQuery or SendQueryAsync instead.")]
        public JObject Query(string query)
        {
            var response = downloadPackagesString($"/3.1/query", query, "application/json", "application/json");
            var json = JObject.Parse(response);
            return json;
        }

        /// <summary>
        /// Send the GraphQL query string to the repository.
        /// </summary>
        /// <param name="query">A GraphQL query string</param>
        /// <returns>A JSON string containing the GraphQL response</returns>
        public string QueryGraphQL(string query) =>
            downloadPackagesString($"/3.1/query", query, "application/json", "application/json");
    }
}
