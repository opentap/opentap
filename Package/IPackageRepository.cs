//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using OpenTap.Authentication;

namespace OpenTap.Package
{
    /// <summary>
    /// A client interface for a package repository. Implementations include <see cref="FilePackageRepository"/> and <see cref="HttpPackageRepository"/>.
    /// </summary>
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

    /// <summary>
    /// Represents a version of a package. Objects of this type is returned by<see cref="IPackageRepository.GetPackageVersions"/>.
    /// </summary>
    [DebuggerDisplay("{Name} : {Version.ToString()}")]
    public class PackageVersion : PackageIdentifier, IEquatable<PackageVersion>
    {
        /// <summary>
        /// Initializes a new instance of a PackageVersion.
        /// </summary>
        public PackageVersion(string name, SemanticVersion version, string os, CpuArchitecture architechture, DateTime date, List<string> licenses) : base(name, version, architechture, os)
        {
            this.Date = date;
            this.Licenses = licenses;
        }

        internal PackageVersion()
        {

        }

        /// <summary>
        /// The date that the package was build.
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// License(s) required to use this package.
        /// </summary>
        public List<string> Licenses { get; set; }

        /// <summary>
        /// Compares this PackageVersion with another.
        /// </summary>
        public bool Equals(PackageVersion other)
        {
            return (this as PackageIdentifier).Equals(other);
        }
    }

    internal class PackageVersionSerializerPlugin : TapSerializerPlugin
    {
        public override double Order => 5;

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
            if (expectedType.IsA(typeof(PackageVersion)) == false)
                return false;

            // We want to disable either if:
            // 1: we are serializing a single PackageVersion
            // 2: we are serializing a single collection of PackageVersions
            // In any other case, we don't want to disable dependencies
            bool shouldDisableDependencyWriter()
            {
                if (node.Parent == null)
                    return true;
                if (node.Parent.Name.LocalName == "ArrayOfPackageVersion" ||
                    node.Parent.Name.LocalName == "ListOfPackageVersion")
                    return node.Parent.Parent == null;
                return false;
            }

            if (shouldDisableDependencyWriter() == false)
                return false;

            // ask the TestPlanPackageDependency serializer (the one that writes the 
            // <Package.Dependencies> tag in the bottom of e.g. TestPlan files) to
            // not write the tag for this file.
            var depSerializer = Serializer.GetSerializer<TestPlanPackageDependencySerializer>();
            if (depSerializer != null)
                depSerializer.WritePackageDependencies = false;
            
            // The serialization of this element should be handled by the object serializer
            return false;
        }

        PackageVersion DeserializePackageVersion(XElement node)
        {
            var version = new PackageVersion();
            
            void setProp(string propertyName, string value)
            {
                if (propertyName == "CPU") // CPU was removed in OpenTAP 9.0. This is to support packages created by TAP 8x
                    propertyName = "Architecture";

                var prop = typeof(PackageVersion).GetProperty(propertyName);
                if (prop == null) return;
                if (prop.PropertyType.IsEnum)
                    prop.SetValue(version, Enum.Parse(prop.PropertyType, value));
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

            void addProp(string propName, string value)
            {
                var prop = typeof(PackageVersion).GetProperty(propName);
                if (prop == null) return;
                if (prop.PropertyType.HasInterface<IList<string>>())
                {
                    // Instantiate the list if it is not set. Add the element to the list.
                    var list = prop.GetValue(version);
                    if (!(list is IList<string> lst))
                    {
                        lst = new List<string>();
                        prop.SetValue(version, lst);
                    }
                    // Add the value
                    lst.Add(value);
                }
            }

            var elements = node.Elements().ToList();
            var attributes = node.Attributes().ToList();
            foreach (var element in elements)
            {
                if (element.IsEmpty) continue;
                // 'Licenses' is a list and can have multiple values
                // Add each license element to the license list
                if (element.Name.LocalName == "Licenses" && element.HasElements)
                {
                    foreach (var childEle in element.Elements())
                    {
                        addProp(element.Name.LocalName, childEle.Value);
                    }
                }
                // Otherwise try to assign the property
                else
                {
                    setProp(element.Name.LocalName, element.Value);
                }
            }
            foreach (var attribute in attributes)
            {
                setProp(attribute.Name.LocalName, attribute.Value);
            }

            return version;
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

        static void ParallelTryForEach<TSource>(IEnumerable<TSource> source, Action<TSource> body)
        {
            try
            {
                Parallel.ForEach(source, body);
            }
            catch (AggregateException ex)
            {
                foreach (var inner in ex.InnerExceptions)
                {
                    log.Info(inner.Message);
                    log.Debug(inner);
                }
            }
        }

        internal static List<PackageDef> GetPackageNameAndVersionFromAllRepos(List<IPackageRepository> repositories,
            PackageSpecifier id, params IPackageIdentifier[] compatibleWith)
        {
            var list = new List<PackageDef>();

            string query =
                @"query Query {
                            packages(distinctName:true" +
                (id != null ? $",version:\"{id.Version}\",os:\"{id.OS}\",architecture:\"{id.Architecture}\"" : "") +
                @") {
                            name
                            version
                        }
                    }";

            ParallelTryForEach(repositories, repo =>
            {
                if (repo is HttpPackageRepository httprepo && httprepo.Version != null &&
                    RequiredApiVersion.IsCompatible(httprepo.Version))
                {
                    var jsonString = httprepo.QueryGraphQL(query);
                    var json = JObject.Parse(jsonString);
                    lock (list)
                    {
                        foreach (var item in json["packages"])
                            list.Add(new PackageDef()
                            {
                                Name = item["name"].ToString(),
                                Version = SemanticVersion.Parse(item["version"].ToString()),
                                PackageSource = new HttpRepositoryPackageDefSource() { RepositoryUrl = httprepo.Url }
                            });
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
            return list;
        }

        internal static List<PackageDef> GetPackagesFromAllRepos(List<IPackageRepository> repositories,
            PackageSpecifier id, params IPackageIdentifier[] compatibleWith)
        {
            var list = new List<PackageDef>();

            ParallelTryForEach(repositories, repo =>
            {
                var packages = repo.GetPackages(id, compatibleWith);
                lock (list)
                {
                    list.AddRange(packages);
                }
            });

            return list;
        }

        internal static List<PackageVersion> GetAllVersionsFromAllRepos(List<IPackageRepository> repositories,
            string packageName, params IPackageIdentifier[] compatibleWith)
        {
            var list = new List<PackageVersion>();
            ParallelTryForEach(repositories, repo =>
            {
                var packages = repo.GetPackageVersions(packageName, compatibleWith);
                lock (list)
                {
                    list.AddRange(packages);
                }
            });
            return list;
        }

        /// <summary>
        /// Returns FilePackageRepository if either of the following is true:
        /// - Url is explicitly defined with file:/// 
        /// - The url is relative and directory exists
        /// - Starts with classic windows absolute path like 'C:/'
        /// - Starts with '\\'
        /// Otherwise returns HttpPackageRepository
        /// </summary>
        /// <param name="url">url to be determined to be file path or http path</param>
        /// <returns>Determined repository type</returns>
        internal static IPackageRepository DetermineRepositoryType(string url)
        {
            if (registeredRepositories.TryGetValue(url, out var repo))
                return repo;
            if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out Uri uri))
            {
                if(uri.IsAbsoluteUri)
                {
                    switch(uri.Scheme)
                    {
                        case "http":
                        case "https":
                            return new HttpPackageRepository(url);
                        case "file":
                            return new FilePackageRepository(uri.AbsolutePath);
                        default:
                            throw new NotSupportedException($"Scheme {uri.Scheme} is not supported as a package repository ({url}).");
                    }
                }
                else
                {
                    if (AuthenticationSettings.Current.BaseAddress != null)
                        return DetermineRepositoryType(new Uri(new Uri(AuthenticationSettings.Current.BaseAddress), url).AbsoluteUri);
                    else
                        return new FilePackageRepository(Path.GetFullPath(url)); // GetFullPath throws ArgumentException if url contains illigal path chars
                }
            }
            else
                throw new NotSupportedException($"Unable to determine repository type of '{url}'. Try specifying a scheme using 'http://' or 'file:///'.");
        }

        static Dictionary<string, IPackageRepository> registeredRepositories = new Dictionary<string, IPackageRepository>();

        internal static void RegisterRepository(IPackageRepository repo)
        {
            registeredRepositories[repo.Url] = repo;
        }
    }
}
