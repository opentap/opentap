//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
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
        private string defaultUrl;

        public bool IsSilent;

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
        }

        private async Task DoDownloadPackage(PackageDef package, string destination, CancellationToken cancellationToken)
        {
            bool finished = false;
            Timer timer = null;
            try
            {
                HttpClientHandler hch = new HttpClientHandler();
                hch.UseProxy = true;
                hch.Proxy = WebRequest.GetSystemWebProxy();
                HttpClient hc = new HttpClient(hch);
                
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
                    const int bufferSize = 1024 * 1024; // Try reading 1 MB at a time
                    double offset = 0;
                    var buffer = new byte[bufferSize];
                    bool canRead = true;
                    timer = new Timer(c =>
                    {
                        // Calculate progress
                        Console.Write($"Downloading [{new String('=', (int)((offset / totalSize) * 30))}{new String(' ', 30 - (int)((offset / totalSize) * 30))}] {(offset / totalSize) * 100:0.00}%.\r");
                    }, null, 0, 1000);

                    do
                    {
                        // Start reading from stream
                        var task = responseStream.ReadAsync(buffer, 0, bufferSize, cancellationToken);
                        
                        // Read stream
                        var readLength = await task;
                        
                        // Write to file
                        await fileStream.WriteAsync(buffer, 0, readLength, cancellationToken);
                        
                        canRead = readLength > 0;
                        offset += readLength;
                    } 
                    while (canRead);
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
                // Stop timer
                if(timer != null)
                {
                    timer.Dispose();
                    log.Flush();
                }
                
                if ((!finished || cancellationToken.IsCancellationRequested) && File.Exists(destination))
                    File.Delete(destination);
            }
        }
        
        private string downloadPackagesString(string args, string data = null, string contentType = null)
        {
            string xmlText = null;
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Proxy = WebRequest.GetSystemWebProxy();
                    wc.Headers.Add(HttpRequestHeader.Accept, "application/xml");
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
                using (WebClient wc = new WebClient())
                {
                    wc.Proxy = WebRequest.GetSystemWebProxy();
                    wc.Headers.Add(HttpRequestHeader.Accept, "application/xml");
                    wc.Headers.Add("OpenTAP", PluginManager.GetOpenTapAssembly().SemanticVersion.ToString());

                    try
                    {
                        var xmlText = wc.DownloadString($"{url}/{ApiVersion}/version");
                        url = checkUrl(url, xmlText);
                    }
                    catch
                    {
                        try
                        {
                            var xmlText = wc.DownloadString(url);
                            url = checkUrl(url, xmlText);
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
        
        private string checkUrl(string url, string xmlText)
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
            try
            {
                var data = new WebClient().DownloadString($"{Url}/{ApiVersion}/version");
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(data)))
                {
                    var version = new XmlSerializer(typeof(string)).Deserialize(stream) as string;
                    if (SemanticVersion.TryParse(version, out var semver) && semver.IsCompatible(SemanticVersion.Parse("1.0.0")) == false)
                        throw new Exception();
                }
            }
            catch
            {
                throw new NotSupportedException($"The repository '{this.Url}' is not supported by this version of OpenTAP.");
            }
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

                    var macAddr = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                        .Select(nic => nic.GetPhysicalAddress()).FirstOrDefault();

                    var block = new byte[8];
                    if (macAddr != null)
                        macAddr.GetAddressBytes().CopyTo(block, 0);

                    string arg = string.Format("/{0}/CheckForUpdates?name={1}", ApiVersion, MurMurHash3.Hash(block));
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
    }
}
