//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Tap.Shared;

namespace OpenTap.Package
{
    public class HttpPackageRepository : IPackageRepository
    {
        private static TraceSource log = Log.CreateSource("HttpPackageRepository");
        private const string ApiVersion = "3.0";
        private VersionSpecifier MinRepoVersion = new VersionSpecifier(3, 0, 0, "", "", VersionMatchBehavior.AnyPrerelease | VersionMatchBehavior.Compatible);
        private string defaultUrl;

        public bool IsSilent;
        private SemanticVersion _version;
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

        public HttpPackageRepository(string url)
        {
            url = url.Trim();
            if (Regex.IsMatch(url, "http(s)?://"))
                this.Url = url;
            else
                this.Url = "http://" + url;

            // Trim end to fix redirection. E.g. 'plugins.tap.aalborg.keysight.com:8086/' redirects to 'plugins.tap.aalborg.keysight.com'.
            this.Url = this.Url.TrimEnd('/');
            defaultUrl = this.Url;
            this.Url = CheckUrlRedirect(this.Url);
            
            var macAddr = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                        .Select(nic => nic.GetPhysicalAddress()).FirstOrDefault();
            var block = new byte[8];
            if (macAddr != null)
                macAddr.GetAddressBytes().CopyTo(block, 0);
            string mac = BitConverter.ToString(block).Replace("-", string.Empty);
            string installDir = ExecutorClient.ExeDir;
            UpdateId = String.Format("{0:X8}{0:X8}", MurMurHash3.Hash(mac), MurMurHash3.Hash(installDir));
        }

        private async Task DoDownloadPackage(PackageDef package, string destination, CancellationToken cancellationToken)
        {
            bool finished = false;
            try
            {
                using (HttpClientHandler hch = new HttpClientHandler() { UseProxy = true, Proxy = WebRequest.GetSystemWebProxy() })
                using (HttpClient hc = new HttpClient(hch) { Timeout = Timeout.InfiniteTimeSpan })
                {

                    StringContent content = null;
                    using (Stream stream = new MemoryStream())
                    using (var reader = new StreamReader(stream))
                    {
                        package.SaveTo(stream);
                        stream.Seek(0, 0);
                        string cnt = reader.ReadToEnd().Replace("http://opentap.io/schemas/package", "http://keysight.com/schemas/TAP/Package"); // TODO: remove when server is updated (this is only here for support of the TAP 8.x Repository server that does not yet have a parser that can handle the new name)
                        content = new StringContent(cnt);
                    }

                    // Download plugin
                    var message = new HttpRequestMessage();
                    message.RequestUri = new Uri(Url + "/" + ApiVersion + "/DownloadPackage");
                    message.Content = content;
                    message.Method = HttpMethod.Post;
                    message.Headers.Add("OpenTAP", PluginManager.GetOpenTapAssembly().SemanticVersion.ToString());

                    using (var response = await hc.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    using (var responseStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(destination, FileMode.Create))
                    {
                        if (response.IsSuccessStatusCode == false)
                            throw new HttpRequestException($"The download request failed with {response.StatusCode}.");

                        var totalSize = response.Content.Headers.ContentLength ?? -1L;
                        var task = responseStream.CopyToAsync(fileStream, 4096, cancellationToken);
                        ConsoleUtils.PrintProgressTillEnd(task, "Downloading", () => fileStream.Position, () => totalSize);
                    }
                }

                finished = true;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                if (!(ex is TaskCanceledException))
                {
                    throw;
                }
            }
            finally
            {
                
                if ((!finished || cancellationToken.IsCancellationRequested) && File.Exists(destination))
                    File.Delete(destination);
            }
        }
        
        private string downloadPackagesString(string args, string data = null, string contentType = null, string accept = null)
        {
            string xmlText = null;
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Proxy = WebRequest.GetSystemWebProxy();
                    wc.Headers.Add(HttpRequestHeader.Accept, accept ?? "application/xml");
                    wc.Headers.Add("OpenTAP", PluginManager.GetOpenTapAssembly().SemanticVersion.ToString());

                    if (data != null)
                    {
                        wc.Headers[HttpRequestHeader.ContentType] = contentType ?? "application/x-www-form-urlencoded";
                        xmlText = wc.UploadString(Url + args, "POST", data);
                    }
                    else
                    {
                        xmlText = wc.DownloadString(Url + args);
                    }
                }
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

                throw exception;
            }
            return xmlText;
        }
        private string CheckUrlRedirect(string url)
        {
            try
            {
                using (HttpClient hc = new HttpClient())
                {
                    hc.DefaultRequestHeaders.Add("OpenTAP", PluginManager.GetOpenTapAssembly().SemanticVersion.ToString());
                    hc.DefaultRequestHeaders.Add(HttpRequestHeader.Accept.ToString(), "application/xml");

                    try
                    {
                        var versionUrl = $"{url}/{ApiVersion}/version";
                        var response = hc.GetAsync(versionUrl).Result;

                        // Check for http server redirects
                        url = checkServerRedirect(url, versionUrl, response);

                        // Check client redirects
                        var xmlText = response.Content.ReadAsStringAsync().Result;
                        url = checkClientRedirect(url, xmlText);
                    }
                    catch
                    {
                        try
                        {
                            var xmlText = hc.GetStringAsync(url).Result;
                            url = checkClientRedirect(url, xmlText);
                        }
                        catch
                        {
                            return url;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Debug(ex);
            }
            return url.TrimEnd('/');
        }

        private string checkServerRedirect(string url, string versionUrl, HttpResponseMessage response)
        {
            var redirectedUrl = response.RequestMessage.RequestUri.ToString();
            if (versionUrl != redirectedUrl)
            {
                redirectedUrl = new HttpClient().GetAsync(url).Result.RequestMessage.RequestUri.ToString();
                log.Debug($"Redirected from '{url}' to '{redirectedUrl}'.");
                url = redirectedUrl;
            }

            return url;
        }
        
        private string checkClientRedirect(string url, string xmlText)
        {
            try
            {
                var match = Regex.Match(xmlText, "<meta.*?http-equiv=\\\"refresh\\\".*?>");
                if (match.Success)
                {
                    log.Debug("Found redirect in repository URL. Redirecting to new URL...");
                    match = Regex.Match(match.Value, "url=(.*?)(?:\\\"|')");
                    if (match.Success)
                        url = CheckUrlRedirect(match.Groups[1].Value);
                }
            }
            catch { }

            return url;
        }

        private void CheckRepoApiVersion()
        {
            string tryDownload(string url)
            {
                using (HttpClient hc = new HttpClient())
                {
                    hc.DefaultRequestHeaders.Add("OpenTAP", PluginManager.GetOpenTapAssembly().SemanticVersion.ToString());
                    hc.DefaultRequestHeaders.Add(HttpRequestHeader.Accept.ToString(), "application/xml");
                    try { return hc.GetStringAsync(url).Result; }
                    catch { return null; }
                }
            }

            // Url does not exists
            if (tryDownload(Url) == null)
                throw new WebException($"Unable to connect to '{defaultUrl}'.");

            // Check old repo
            if (tryDownload($"{Url}/2.0/version") != null)
                throw new NotSupportedException($"The repository '{defaultUrl}' is only compatible with TAP 8.x or ealier.");

            // Check specific version
            var data = tryDownload($"{Url}/{ApiVersion}/version");
            if (string.IsNullOrEmpty(data))
                throw new NotSupportedException($"'{defaultUrl}' is not a package repository.");
            var reader = XmlReader.Create(new StringReader(data));
            var serializer = new XmlSerializer(typeof(string));
            if (serializer.CanDeserialize(reader) == false)
                throw new NotSupportedException($"'{defaultUrl}' is not a package repository.");
            var version = serializer.Deserialize(reader) as string;
            if (SemanticVersion.TryParse(version, out _version) && MinRepoVersion.IsCompatible(_version) == false)
                throw new NotSupportedException($"The repository '{defaultUrl}' is not supported.", new Exception($"Repository version '{Version}' is not compatible with min required version '{MinRepoVersion}'."));
        }

        PackageDef[] packagesFromXml(string xmlText)
        {
            try
            {
                if (string.IsNullOrEmpty(xmlText) || xmlText == "null") return new PackageDef[0];
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xmlText)))
                {
                    return PackageDef.ManyFromXml(stream).ToArray();
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
                list.AddRange(new []
                {
                    new PackageIdentifier("Tap", openTap.Version, openTap.Architecture, openTap.OS),
                    new PackageIdentifier("TAP Base", openTap.Version, openTap.Architecture, openTap.OS)
                });
            }
            
            return list.ToArray();
        }

        #region IPackageRepository Implementation
        public string Url { get; set; }

        public void DownloadPackage(IPackageIdentifier package, string destination, CancellationToken cancellationToken)
        {
            if (package is PackageDef)
                DoDownloadPackage(package as PackageDef, destination, cancellationToken).Wait();
            else
            {
                var packageDef = new PackageDef() { Name = package.Name, Version = package.Version, Architecture = package.Architecture, OS = package.OS, Location = Url };
                DoDownloadPackage(packageDef, destination, cancellationToken).Wait();
            }
        }

        public string[] GetPackageNames(CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith)
        {
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
        public string[] GetPackageNames(string @class, CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith)
        {
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

        public PackageVersion[] GetPackageVersions(string packageName, CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith)
        {
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

        public PackageDef[] GetPackages(PackageSpecifier package, CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith)
        {
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

        public string UpdateId;

        public PackageDef[] CheckForUpdates(IPackageIdentifier[] packages, CancellationToken cancellationToken)
        {
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
                        Name = p.Name,
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

        public JObject Query(string query)
        {
            var response = downloadPackagesString($"/3.1/query", query, "application/json", "application/json");
            var json = JObject.Parse(response);
            return json;
        }
    }
}
