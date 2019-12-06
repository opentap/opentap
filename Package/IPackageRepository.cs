//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace OpenTap.Package
{
    public interface IPackageRepository
    {
        /// <summary>
        /// The url of the repository.
        /// </summary>
        string Url { get; }
        
        /// <summary>
        /// Downloads a package from this repository to a file.
        /// </summary>
        /// <param name="package">The package to download.</param>
        /// <param name="destination">The destination path where the package should be stored.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the download.</param>
        void DownloadPackage(IPackageIdentifier package, string destination, CancellationToken cancellationToken);

        /// <summary>
        /// Get all names of packages.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the download.</param>
        /// <param name="compatibleWith">Any packages that the package to download must be compatible with.</param>
        /// <returns>An array of package names.</returns>
        string[] GetPackageNames(CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith);

        /// <summary>
        /// Returns all package version information about a package.
        /// </summary>
        /// <param name="packageName">The name package to retrieve version info about.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the download.</param>
        /// <param name="compatibleWith">Any packages that the package to download must be compatible with.</param>
        /// <returns>An array of package versions.</returns>
        PackageVersion[] GetPackageVersions(string packageName, CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith);

        /// <summary>
        /// This returns the latest version of a package that matches a number of specified parameters.
        /// If multiple packages have that same version number they all will be returned.
        /// </summary>
        /// <param name="package">A package identifier. If not specified, packages with any name will be returned.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the download.</param>
        /// <param name="compatibleWith">Any packages that the package to download must be compatible with.</param>
        /// <returns>An array of package definitions <see cref="PackageDef"/>.</returns>
        PackageDef[] GetPackages(PackageSpecifier package, CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith);

        /// <summary>
        /// Returns a list of all packages that have an updated version.
        /// </summary>
        /// <param name="packages"></param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the download.</param>
        /// <returns>An array of package definitions <see cref="PackageDef"/>.</returns>
        PackageDef[] CheckForUpdates(IPackageIdentifier[] packages, CancellationToken cancellationToken);
    }

    public class PackageVersion : PackageIdentifier, IEquatable<PackageVersion>
    {
        public PackageVersion(string name, SemanticVersion version, string os, CpuArchitecture architechture, DateTime date, List<string> licenses) : base(name,version,architechture,os)
        {
            this.Date = date;
            this.Licenses = licenses;
        }
        
        internal PackageVersion() 
        {

        }

        public DateTime Date { get; set; }
        public List<string> Licenses { get; set; }

        public bool Equals(PackageVersion other)
        {
            return (this as PackageIdentifier).Equals(other);
        }
    }

    internal class PackageVersionSerializerPlugin : TapSerializerPlugin
    {
        public override double Order { get { return 5; } }

        public override bool Deserialize(XElement node, ITypeData t, Action<object> setter)
        {
            if (t.IsA(typeof(PackageVersion[])))
            {
                var list = new List<PackageVersion>();
                foreach (var element in node.Elements())
                    list.Add(DeserializePackageVersion(element));

                setter(list.ToArray());
                return true;
            }
            if (t.IsA(typeof(PackageVersion)))
            {
                setter(DeserializePackageVersion(node));
                return true;
            }
            return false;
        }

        public override bool Serialize(XElement node, object obj, ITypeData expectedType)
        {
            return false;
        }

        PackageVersion DeserializePackageVersion(XElement node)
        {
            var version = new PackageVersion();
            var elements = node.Elements().ToList();
            var attributes = node.Attributes().ToList();
            foreach (var element in elements)
            {
                if (element.IsEmpty) continue;
                setProp(element.Name.LocalName, element.Value);
            }
            foreach (var attribute in attributes)
            {
                setProp(attribute.Name.LocalName, attribute.Value);
            }

            return version;

            void setProp(string propertyName, string value)
            {
                if (propertyName == "CPU") // CPU was removed in OpenTAP 9.0. This is to support packages created by TAP 8x
                    propertyName = "Architecture";

                var prop = typeof(PackageVersion).GetProperty(propertyName);
                if (prop == null) return;
                if (prop.PropertyType.IsEnum)
                    prop.SetValue(version, Enum.Parse(prop.PropertyType, value));
                else if (prop.PropertyType.HasInterface<IList<string>>())
                {
                    var list = new List<string>();
                    list.Add(value);
                    prop.SetValue(version, list);
                }
                else if (prop.PropertyType == typeof(SemanticVersion))
                {
                    if (SemanticVersion.TryParse(value, out var semver))
                        prop.SetValue(version, semver);
                    else
                        Log.Warning($"Cannot parse version '{value}' of package '{version.Name ?? "Unknown"}'.");
                }
                else if (prop.PropertyType == typeof(DateTime))
                {
                    if (DateTime.TryParse(value, out var date))
                        prop.SetValue(version, date);
                }
                else
                {
                    prop.SetValue(version, value);
                }
            }
        }
    }

    internal static class PackageRepositoryExtension
    {
        public static void DownloadPackage(this IPackageRepository repository, IPackageIdentifier package, string destination)
        {
            repository.DownloadPackage(package, destination, TapThread.Current.AbortToken);
        }

        public static string[] GetPackageNames(this IPackageRepository repository, params IPackageIdentifier[] compatibleWith)
        {
            return repository.GetPackageNames(TapThread.Current.AbortToken, compatibleWith);
        }

        public static PackageVersion[] GetPackageVersions(this IPackageRepository repository, string packageName, params IPackageIdentifier[] compatibleWith)
        {
            return repository.GetPackageVersions(packageName, TapThread.Current.AbortToken, compatibleWith);
        }

        public static PackageDef[] GetPackages(this IPackageRepository repository, PackageSpecifier package, params IPackageIdentifier[] compatibleWith)
        {
            return repository.GetPackages(package, TapThread.Current.AbortToken, compatibleWith);
        }

        public static PackageDef[] CheckForUpdates(this IPackageRepository repository, IPackageIdentifier[] packages)
        {
            return repository.CheckForUpdates(packages, TapThread.Current.AbortToken);
        }
    }

    internal class PackageRepositoryHelpers
    {
        private static TraceSource log = Log.CreateSource("PackageRepository");
        private static VersionSpecifier RequiredApiVersion = new VersionSpecifier(3, 1, 0, "", "", VersionMatchBehavior.Compatible | VersionMatchBehavior.AnyPrerelease); // Required for GraphQL

        internal static List<PackageDef> GetPackageNameAndVersionFromAllRepos(List<IPackageRepository> repositories, PackageSpecifier id, params IPackageIdentifier[] compatibleWith)
        {
            var list = new List<PackageDef>();
            try
            {
                string query = 
                    @"query Query {
                            packages(distinctName:true" + (id != null ? $",version:\"{id.Version}\",os:\"{id.OS}\",architecture:\"{id.Architecture}\"" : "") + @") {
                            name
                            version
                        }
                    }";

                Parallel.ForEach(repositories, repo =>
                {
                    if (repo is HttpPackageRepository httprepo && httprepo.Version != null && RequiredApiVersion.IsCompatible(httprepo.Version))
                    {
                        var json = httprepo.Query(query);
                        lock (list)
                        {
                            foreach (var item in json["packages"])
                                list.Add(new PackageDef() { Name = item["name"].ToString(), Version = SemanticVersion.Parse(item["version"].ToString()) });
                        }
                    }
                    else
                    {
                        var packages = repo.GetPackages(id, compatibleWith);
                        lock (list)
                        {
                            list.AddRange(packages);
                        }
                    }
                });
            }
            catch (AggregateException ex)
            {
                foreach (var inner in ex.InnerExceptions)
                    log.Error(inner);
                log.Error(ex);
            }
            return list;
        }

        internal static List<PackageDef> GetPackagesFromAllRepos(List<IPackageRepository> repositories, PackageSpecifier id, params IPackageIdentifier[] compatibleWith)
        {
            var list = new List<PackageDef>();
            try
            {
                Parallel.ForEach(repositories, repo =>
                {
                    var packages = repo.GetPackages(id, compatibleWith);
                    lock (list)
                    {
                        list.AddRange(packages);
                    }
                });
            }
            catch (AggregateException ex)
            {
                foreach (var inner in ex.InnerExceptions)
                    log.Error(inner);
                log.Error(ex);
            }
            return list;
        }

        internal static List<PackageVersion> GetAllVersionsFromAllRepos(List<IPackageRepository> repositories, string packageName, params IPackageIdentifier[] compatibleWith)
        {
            var list = new List<PackageVersion>();
            try
            {
                Parallel.ForEach(repositories, repo =>
                {
                    var packages = repo.GetPackageVersions(packageName, compatibleWith);
                    lock (list)
                    {
                        list.AddRange(packages);
                    }
                });
            }
            catch (AggregateException ex)
            {
                foreach (var inner in ex.InnerExceptions)
                    log.Error(inner);
                log.Error(ex);
            }
            return list;
        }

        internal static IPackageRepository DetermineRepositoryType(string url)
        {
            if(url.Contains("http://") || url.Contains("https://"))
                return new HttpPackageRepository(url); // avoid throwing exceptions if it looks a lot like a URL.
            if (Uri.IsWellFormedUriString(url, UriKind.Relative) && Directory.Exists(url))
                return new FilePackageRepository(url);
            if (File.Exists(url))
                return new FilePackageRepository(Path.GetDirectoryName(url));
            if (Regex.IsMatch(url ?? "", @"^([A-Z|a-z]:)?(\\|/)"))
                return new FilePackageRepository(url);
            else
                return new HttpPackageRepository(url);
        }
    }
}
