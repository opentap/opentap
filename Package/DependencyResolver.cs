//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    /// <summary>
    /// Finds dependencies for specified packages in Package Repositories
    /// </summary>
    public class DependencyResolver
    {
        /// <summary>
        /// List of all the dependencies to the specified packages
        /// </summary>
        public List<PackageDef> Dependencies = new List<PackageDef>();

        /// <summary>
        /// List of the dependencies to the specified packages that are currently not installed
        /// </summary>
        public List<PackageDef> MissingDependencies = new List<PackageDef>();

        /// <summary>
        /// List of the dependencies to the specified packages that could not be found in the package repositories
        /// </summary>
        public List<PackageDependency> UnknownDependencies = new List<PackageDependency>();

        /// <summary>
        /// Instantiates a new dependency resolver.
        /// </summary>
        /// <param name="packages">The packages to resolve dependencies for.</param>
        /// <param name="installed">The list of installed packages. If set to null it will get the list of the install in the used directory.</param>
        public DependencyResolver(IEnumerable<PackageDef> packages, IEnumerable<PackageDef> installed = null)
        {
            if (installed != null)
                InstalledPackages = installed.ToDictionary(pkg => pkg.Name);
            else
            {
                InstalledPackages = new Dictionary<string, PackageDef>();
                foreach (var pkg in new Installation(Directory.GetCurrentDirectory()).GetPackages())
                    InstalledPackages[pkg.Name] = pkg;
            }

            resolve(packages);
        }

        private void resolve(IEnumerable<PackageDef> packages)
        {
            var firstleveldependencies = packages.SelectMany(pkg => pkg.Dependencies.Select(dep => new { Dependency = dep, Architecture = pkg.Architecture, OS = pkg.OS }));
            Dependencies.AddRange(packages);
            foreach (var dependency in firstleveldependencies)
            {
                GetDependenciesRecursive(dependency.Dependency, dependency.Architecture, dependency.OS);
            }
        }

        private void GetDependenciesRecursive(PackageDependency dependency, CpuArchitecture packageArchitecture, string OS)
        {
            if (Dependencies.Any(p => (p.Name == dependency.Name) &&
                dependency.Version.IsCompatible(p.Version) &&
                ArchitectureHelper.PluginsCompatible(p.Architecture, packageArchitecture)))
                return;
            PackageDef depPkg = GetPackageDefFromInstallation(dependency.Name, dependency.Version, packageArchitecture, OS);
            if (depPkg == null)
            {
                depPkg = GetPackageDefFromRepo(dependency.Name, dependency.Version, packageArchitecture, OS);
                MissingDependencies.Add(depPkg);
            }
            if (depPkg == null)
            {
                UnknownDependencies.Add(dependency);
                return;
            }
            Dependencies.Add(depPkg);
            foreach (var nextLevelDep in depPkg.Dependencies)
            {
                GetDependenciesRecursive(nextLevelDep, packageArchitecture, OS);
            }
        }

        private Dictionary<string, PackageDef> InstalledPackages;

        PackageDef GetPackageDefFromInstallation(string name, VersionSpecifier version, CpuArchitecture pluginArchitecture, string OS)
        {
            if (name.ToLower().EndsWith(".tappackage"))
                name = Path.GetFileNameWithoutExtension(name);
            if(InstalledPackages.ContainsKey(name))
            {
                PackageDef package = InstalledPackages[name];
                // Check that the installed package is compatible with the required package
                if ( version.IsCompatible(package.Version) && 
                    ArchitectureHelper.PluginsCompatible(package.Architecture, pluginArchitecture) && package.IsPlatformCompatible(selectedOS: OS))
                    return package;
            }
            return null;
        }

        private static PackageDef GetPackageDefFromRepo(string name, VersionSpecifier version, CpuArchitecture pluginArchitecture, string OS)
        {
            if (name.ToLower().EndsWith(".tappackage"))
                name = Path.GetFileNameWithoutExtension(name);

            var packages =  PackageRepositoryHelpers.GetPackagesFromAllRepos(new PackageSpecifier(name, version, pluginArchitecture, OS));

            return packages.OrderByDescending(pkg => pkg.Version).FirstOrDefault(pkg => ArchitectureHelper.PluginsCompatible(pkg.Architecture, pluginArchitecture));
        }
    }
}
