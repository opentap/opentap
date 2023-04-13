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
using OpenTap.Repository.Client;
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
        private HttpClient client;
        private HttpClient HttpClient => client ??= GetHttpClient(Url);
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
        }

        /// <summary>
        /// Initialize a http repository with the given URL
        /// </summary>
        /// <param name="url"></param>

        public HttpPackageRepository(string url)
        {
            Url = url.TrimEnd('/');
            UpdateId = Installation.Current.Id;
            RepoClient = new RepoClient(Url);
            
            if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri) &&
                !string.IsNullOrWhiteSpace(uri.Authority))
            {
                var domain = uri.Authority;
                foreach (var token in AuthenticationSettings.Current.Tokens)
                {
                    if (token.Domain == domain)
                    {
                        RepoClient.AddAuthentication(new BearerTokenAuthentication(token.AccessToken));
                        break;
                    }
                }
            }
        }

        Action<string, long, long> IPackageDownloadProgress.OnProgressUpdate { get; set; }

        async Task DoDownloadPackage(PackageDef package, FileStream fileStream, CancellationToken cancellationToken)
        {
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
                bool transient = false;
                try
                {
                    var range = RangeHeaderValue.Parse($"bytes={fileStream.Position}-");
                    var path = $"/Packages/{package.Name}";

                    // Download the package
                    using var responseStream = RepoClient.DownloadObjectRange(path, package.Version?.ToString(),
                        package.Architecture.ToString(), package.OS, range, cancellationToken);
                    // In case the download is interrupted, we will assume the error is transient if we got an initial response.
                    transient = true;
                    if (totalSize < 0) totalSize = responseStream.Length;

                    var task = responseStream.CopyToAsync(fileStream, _DefaultCopyBufferSize, cancellationToken);
                    await ConsoleUtils.ReportProgressTillEndAsync(task, "Downloading",
                        () => fileStream.Position,
                        () => totalSize,
                        (header, pos, len) =>
                        {
                            ConsoleUtils.printProgress(header, pos, len);
                            (this as IPackageDownloadProgress).OnProgressUpdate?.Invoke(header, pos, len);
                        });

                    break;
                }
                catch (Exception ex) when (ex is IOException || ex is HttpRequestException || transient)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
                catch (Exception)
                {
                    if (cancellationToken.IsCancellationRequested == false)
                        log.Error($"Failed to download package {package.Name} from {Url}.");
                    throw;
                }
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
            lock (updateVersionLock)
            {
                if (IsInError())
                    return;

                try
                {
                    var version = RepoClient.Version(CancellationToken.None);
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
                if (string.IsNullOrEmpty(xmlText) || xmlText == "null") return Array.Empty<PackageDef>();
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
        private string defaultUrl => Url;

        private RepoClient RepoClient { get; }

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

        private void Auth()
        {
            if (!Uri.TryCreate(Url, UriKind.RelativeOrAbsolute, out var uri)) return;
            if (uri.IsAbsoluteUri)
            {
                var domain = uri.Authority;
                foreach (var token in AuthenticationSettings.Current.Tokens)
                {

                    if (token.Domain == domain)
                    {
                        RepoClient.AddAuthentication(new BearerTokenAuthentication(token.AccessToken));
                    }
                }
            }
            else
            {
                foreach (var token in AuthenticationSettings.Current.Tokens)
                {
                    RepoClient.AddAuthentication(new BearerTokenAuthentication(token.AccessToken));
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

            var parameters = new Dictionary<string, object>()
            {
                ["type"] = "TapPackage",
                ["distinctName"] = true,
                ["directory"] = "/Packages/",
            };

            var packages = RepoClient.Query(parameters, cancellationToken, "Name");
            return packages.Select(p => p["Name"] as string).ToArray();
        }

        /// <summary>
        /// Get the names of the available packages in the repository with the specified class
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public string[] GetPackageNames(string @class, CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith)
        {
            if (IsInError()) return Array.Empty<string>();
            var parameters = new Dictionary<string, object>()
            {
                ["type"] = @class,
                ["distinctName"] = true,
                ["directory"] = "/Packages/",
            };

            var packages = RepoClient.Query(parameters, cancellationToken, "Name");
            return packages.Select(p => p["Name"] as string).ToArray();
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

            var parameters = new Dictionary<string, object>()
            {
                ["type"] = "TapPackage",
                ["name"] = packageName,
                ["directory"] = "/Packages/",
            };

            var packageVersions = RepoClient.Query(parameters, cancellationToken, "Name", "Version", "OS", "Architecture", "LicenseRequired", "Date");

            return packageVersions.Select(p =>
            {
                var name = p["Name"] as string;
                Enum.TryParse(p["Architecture"] as string, out CpuArchitecture arch);
                var version = SemanticVersion.Parse(p["Version"] as string);
                var os = p["OS"] as string;
                var date = (DateTime)p["Date"];
                var licenses = new List<string>() { p["LicenseRequired"] as string };
                return new PackageVersion(name, version, os, arch, date, licenses);

            }).ToArray();
        }

        /// <summary>
        /// Get the available versions of packages matching 'package' and optionally compatible with a list of packages
        /// </summary>
        /// <param name="package"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="compatibleWith"></param>
        /// <returns></returns>
        public PackageDef[] GetPackages(PackageSpecifier package, CancellationToken cancellationToken,
            params IPackageIdentifier[] compatibleWith)
        {
            if (IsInError()) return Array.Empty<PackageDef>();

            var parameters = new Dictionary<string, object>()
            {
                ["type"] = "TapPackage",
                ["directory"] = "/Packages/",
                ["distinctName"] = true,
            };

            if (!string.IsNullOrWhiteSpace(package.Name))
            {
                parameters["name"] = package.Name;
            }

            if (package.Version != VersionSpecifier.AnyRelease)
                parameters["version"] = package.Version.ToString();
            if (!string.IsNullOrWhiteSpace(package.OS))
                parameters["os"] = package.OS;
            if (package.Architecture != CpuArchitecture.AnyCPU)
                parameters["architecture"] = package.Architecture.ToString();

            var packages = RepoClient.Query(parameters, cancellationToken, "PackageDef");
            return packages.Select(p => p["PackageDef"] as string).Where(xml => !string.IsNullOrWhiteSpace(xml))
                .Select(xml =>
                {
                    using var ms = new MemoryStream(Encoding.UTF8.GetBytes(xml));
                    var pkg = PackageDef.FromXml(ms);
                    if (!(pkg.PackageSource is HttpRepositoryPackageDefSource))
                    {
                        // The repository can return a FilePackageDefSource instead of a HttpRepositoryPackageDefSource.
                        // We need to overwrite the incorrect information in order to be able to download the package.
                        pkg.PackageSource = new HttpRepositoryPackageDefSource { RepositoryUrl = Url };
                    }

                    return pkg;
                })
                .ToArray();
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

                    string arg = $"/{ApiVersion}/CheckForUpdates?name={UpdateId}";
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
        public string QueryGraphQL(string query) => downloadPackagesString($"/3.1/query", query, "application/json", "application/json");
        internal string QueryGraphQL4(string query) => downloadPackagesString($"/4.0/query", query, "application/json", "application/json");
        
        /// <summary>  Creates a display friendly string of this. </summary>
        public override string ToString() =>  $"[HttpPackageRepository: {Url}]";
    }
}
