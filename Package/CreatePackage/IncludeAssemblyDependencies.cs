using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenTap.Cli;

namespace OpenTap.Package
{
    [Display("Include Assembly Dependencies")]
    class IncludeAssemblyDependencies : ICustomPackageAction
    {
        private static TraceSource log = Log.CreateSource(nameof(IncludeAssemblyDependencies));

        /// <summary>
        /// This should run pretty late because other ICustomPackageActions could have potentially modified the assemblies, thereby
        /// adding or removing assembly references. It should still run before FilePackageHash (1001) and Sign (1000).
        /// </summary>
        /// <returns></returns>
        public int Order() => 999;

        public PackageActionStage ActionStage => PackageActionStage.Create;

        public bool Execute(PackageDef package, CustomPackageActionArgs customActionArgs)
        {
            var excludeAdd = package.Files.Where(file => file.IgnoredDependencies != null).SelectMany(file => file.IgnoredDependencies).Distinct().ToList();
            var searcher = new PluginSearcher(PluginSearcher.Options.IncludeSameAssemblies);
            searcher.Search(Directory.GetCurrentDirectory());
            List<AssemblyData> searchedFiles = searcher.Assemblies.ToList();
            findDependencies(package, excludeAdd, searchedFiles);

            return true;
        }

        internal static void findDependencies(PackageDef pkg, List<string> excludeAdd, List<AssemblyData> searchedFiles)
        {
            var searcher = new PackageDefExt.PackageAssemblyCache(searchedFiles);

            // First update the pre-entered dependencies
            bool foundNew = false;
            var notFound = new HashSet<string>();

            // find the current installation
            var currentInstallation = Installation.Current;
            if (!currentInstallation.IsInstallationFolder) // if there is no installation in the current folder look where tap is executed from
                currentInstallation = new Installation(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            var installed = currentInstallation.GetPackages().Where(p => p.Name != pkg.Name).ToList();
            VerifyPackageDependencies(pkg, installed);

            // Find additional dependencies
            do
            {
                foundNew = false;

                // Find everything we already know about
                var offeredByDependencies = AssembliesOfferedBy(installed, pkg.Dependencies, false, searcher).ToList();
                var offeredByThis = searcher.GetPackageAssemblies(pkg)
                    .Where(f => f != null)
                    .ToList();

                var anyOffered = offeredByDependencies.Concat(offeredByThis).ToList();

                // Find our dependencies and subtract the above two lists
                var dependentAssemblyNames = pkg.Files
                    .SelectMany(fs => fs.DependentAssemblies)
                    .Where(r => r.Name != "mscorlib") // Special case. We should not bundle the framework assemblies.
                    .Where(r => !anyOffered.Any(of => AssemblyRefUtils.IsCompatibleReference(of, r)))
                    .Distinct().Where(x => !excludeAdd.Contains(x.Name)).ToList();

                // If there's anything left we start resolving
                if (dependentAssemblyNames.Any())
                {
                    // First look in installed packages
                    var packageCandidates = new Dictionary<PackageDef, int>();
                    foreach (var f in installed)
                    {
                        var candidateAsms = searcher.GetPackageAssemblies(f)
                            .Where(asm => dependentAssemblyNames.Any(dep => (dep.Name == asm.Name))).ToList();

                        // Don't consider a package that only matches assemblies in the Dependencies subfolder
                        candidateAsms.RemoveAll(asm => asm.Location.Contains("Dependencies")); // TODO: less lazy check for Dependencies subfolder would be good.

                        if (candidateAsms.Count > 0)
                            packageCandidates[f] = candidateAsms.Count;
                    }

                    // Look at the most promising candidate (i.e. the one containing most assemblies with the same names as things we need)
                    PackageDef candidatePkg = packageCandidates.OrderByDescending(k => k.Value).FirstOrDefault().Key;

                    if (candidatePkg != null)
                    {
                        foreach (AssemblyData candidateAsm in searcher.GetPackageAssemblies(candidatePkg))
                        {
                            var requiredAsm = dependentAssemblyNames.FirstOrDefault(dep => dep.Name == candidateAsm.Name);
                            if (requiredAsm != null)
                            {
                                if (OpenTap.Utils.Compatible(candidateAsm.Version, requiredAsm.Version))
                                {
                                    log.Info($"Satisfying assembly reference to {requiredAsm.Name} by adding dependency on package {candidatePkg.Name}");
                                    if (candidateAsm.Version != requiredAsm.Version)
                                    {
                                        log.Warning($"Version of {requiredAsm.Name} in {candidatePkg.Name} is different from the version referenced in this package ({requiredAsm.Version} vs {candidateAsm.Version}).");
                                        log.Warning($"Consider changing your version of {requiredAsm.Name} to {candidateAsm.Version} to match that in {candidatePkg.Name}.");
                                    }

                                    foundNew = true;
                                }
                                else
                                {
                                    var depender = pkg.Files.FirstOrDefault(f => f.DependentAssemblies.Contains(requiredAsm));
                                    if (depender == null)
                                        log.Error(
                                            $"This package require assembly {requiredAsm.Name} in version {requiredAsm.Version} while that assembly is already installed through package '{candidatePkg.Name}' in version {candidateAsm.Version}.");
                                    else
                                        log.Error(
                                            $"{Path.GetFileName(depender.FileName)} in this package require assembly {requiredAsm.Name} in version {requiredAsm.Version} while that assembly is already installed through package '{candidatePkg.Name}' in version {candidateAsm.Version}.");
                                    //log.Error($"Please align the version of {requiredAsm.Name} to ensure interoperability with package '{candidate.Key.Name}' or uninstall that package.");
                                    throw new ExitCodeException((int)PackageExitCodes.AssemblyDependencyError,
                                        $"Please align the version of {requiredAsm.Name} ({candidateAsm.Version} vs {requiredAsm.Version})  to ensure interoperability with package '{candidatePkg.Name}' or uninstall that package.");
                                }
                            }
                        }

                        if (foundNew)
                        {
                            log.Info("Adding dependency on package '{0}' version {1}", candidatePkg.Name, candidatePkg.Version);

                            PackageDependency pd = new PackageDependency(candidatePkg.Name, new VersionSpecifier(candidatePkg.Version, VersionMatchBehavior.Compatible));
                            pkg.Dependencies.Add(pd);
                        }
                    }
                    else
                    {
                        // No installed package can offer any of the remaining referenced assemblies.
                        // add them as payload in this package in the Dependencies subfolder
                        foreach (var unknown in dependentAssemblyNames)
                        {
                            var foundAsms = searchedFiles.Where(asm => (asm.Name == unknown.Name) && OpenTap.Utils.Compatible(asm.Version, unknown.Version)).ToList();
                            var foundAsm = foundAsms.FirstOrDefault();

                            if (foundAsm != null)
                            {
                                AddFileDependencies(pkg, unknown, foundAsm);
                                searcher.Clear(pkg);
                                foundNew = true;
                            }
                            else if (!notFound.Contains(unknown.Name))
                            {
                                log.Debug("'{0}' could not be found in any of {1} searched assemblies, or is already added.", unknown.Name, searchedFiles.Count);
                                notFound.Add(unknown.Name);
                            }
                        }
                    }
                }
            } while (foundNew);
        }

        private static void VerifyPackageDependencies(PackageDef pkg, List<PackageDef> installed)
        {
            // check versions of any hardcoded dependencies against what is currently installed
            foreach (PackageDependency dep in pkg.Dependencies)
            {
                var installedPackage = installed.FirstOrDefault(ip => ip.Name == dep.Name);
                if (installedPackage != null)
                {
                    if (dep.Version == null)
                    {
                        dep.Version = new VersionSpecifier(installedPackage.Version, VersionMatchBehavior.Compatible);
                        log.Info("A version was not specified for package dependency {0}. Using installed version ({1}).", dep.Name, dep.Version);
                    }
                    else
                    {
                        if (!dep.Version.IsCompatible(installedPackage.Version))
                            throw new ExitCodeException((int)PackageExitCodes.PackageDependencyError,
                                $"Installed version of {dep.Name} ({installedPackage.Version}) is incompatible with dependency specified in package definition ({dep.Version}).");
                    }
                }
                else
                {
                    throw new ExitCodeException((int)PackageExitCodes.PackageDependencyError,
                        $"Package dependency '{dep.Name}' specified in package definition is not installed. Please install a compatible version first.");
                }
            }
        }

        private static void AddFileDependencies(PackageDef pkg, AssemblyData dependency, AssemblyData foundAsm)
        {
            var depender = pkg.Files.FirstOrDefault(f => f.DependentAssemblies.Contains(dependency));
            if (depender == null)
                log.Warning("Adding dependent assembly '{0}' to package. It was not found in any other packages.", Path.GetFileName(foundAsm.Location));
            else
                log.Info($"'{Path.GetFileName(depender.FileName)}' depends on '{dependency.Name}' version '{dependency.Version}'. Adding dependency to package, it was not found in any other packages.");

            var destPath = string.Format("Dependencies/{0}.{1}/{2}", Path.GetFileNameWithoutExtension(foundAsm.Location), foundAsm.Version.ToString(), Path.GetFileName(foundAsm.Location));
            pkg.Files.Add(new PackageFile { SourcePath = foundAsm.Location, RelativeDestinationPath = destPath, DependentAssemblies = foundAsm.References.ToList() });

            // Copy the file to the actual directory so we can rely on it actually existing where we say the package has it.
            if (!File.Exists(destPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                ProgramHelper.FileCopy(foundAsm.Location, destPath);
            }
        }

        internal static IEnumerable<AssemblyData> AssembliesOfferedBy(List<PackageDef> packages, IEnumerable<PackageDependency> refs, bool recursive, PackageDefExt.PackageAssemblyCache offeredFiles)
        {
            var files = new HashSet<AssemblyData>();
            var referenced = new HashSet<PackageDependency>();
            var toLookat = new Stack<PackageDependency>(refs);

            while (toLookat.Any())
            {
                var dep = toLookat.Pop();

                if (referenced.Add(dep))
                {
                    var pkg = packages.Find(p => (p.Name == dep.Name) && dep.Version.IsCompatible(p.Version));

                    if (pkg != null)
                    {
                        if (recursive)
                            pkg.Dependencies.ForEach(toLookat.Push);

                        offeredFiles.GetPackageAssemblies(pkg).ToList().ForEach(f => files.Add(f));
                    }
                }
            }

            return files;
        }

        private static class AssemblyRefUtils
        {
            public static bool IsCompatibleReference(AssemblyData asm, AssemblyData reference)
            {
                return (asm.Name == reference.Name) && OpenTap.Utils.Compatible(asm.Version, reference.Version);
            }
        }
    }
}