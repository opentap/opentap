//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.IO.Compression;
using Tap.Shared;
using System.Xml.Serialization;
using OpenTap.Cli;

namespace OpenTap.Package
{
    internal static class PackageDefExt
    {
        //
        // Note: There is some code duplication between PackageDefExt and PackageDef, 
        // but usually PackageDefExt does something in addition to what PackageDef does.
        //

        static TraceSource log =  Log.CreateSource("Package");

        private static void EnumeratePlugins(PackageDef pkg, List<AssemblyData> searchedAssemblies)
        {
            foreach (PackageFile def in pkg.Files)
            {
                if (def.Plugins == null || !def.Plugins.Any())
                {
                    if (File.Exists(def.FileName) && IsDotNetAssembly(def.FileName))
                    {
                        try
                        {
                            // if the file is already in its destination dir use that instead.
                            // That file is much more likely to be inside the OpenTAP dir we already searched.
                            string fullPath = Path.GetFullPath(def.FileName);

                            // Find the file in searchedAssemblies using its name+version because
                            // searchedAssemblies will only contain AssemblyInfos with Distinct FullNames
                            AssemblyName name = AssemblyName.GetAssemblyName(fullPath);
                            AssemblyData assembly = searchedAssemblies.FirstOrDefault(a => a.Name == name.Name && a.Version == name.Version);

                            if (assembly != null)
                            {
                                var otherversions = searchedAssemblies.Where(a => a.Name == assembly.Name).ToList();
                                if (otherversions.Count > 1)
                                {
                                    // if SourcePath is set to a location inside the installation folder, the file will
                                    // be there twice at this point. This is expected.
                                    bool isOtherVersionFromCopy =
                                        otherversions.Count == 2 &&
                                        otherversions.Any(a => a.Location == Path.GetFullPath(def.FileName)) &&
                                        otherversions.Any(a => a.Location == Path.GetFullPath(def.RelativeDestinationPath));

                                    if (!isOtherVersionFromCopy)
                                        log.Warning($"Found assembly '{assembly.Name}' multiple times: \n" + string.Join("\n", otherversions.Select(a => "- " + a.Location).Distinct()));
                                }

                                if (assembly.PluginTypes != null)
                                {
                                    foreach (TypeData type in assembly.PluginTypes)
                                    {
                                        if (type.TypeAttributes.HasFlag(TypeAttributes.Interface) || type.TypeAttributes.HasFlag(TypeAttributes.Abstract))
                                            continue;
                                        PluginFile plugin = new PluginFile
                                        {
                                            BaseType = string.Join(" | ", type.PluginTypes.Select(t => t.GetBestName())),
                                            Type = type.Name,
                                            Name = type.GetBestName(),
                                            Description = type.Display != null ? type.Display.Description : "",
                                            Groups = type.Display != null ? type.Display.Group : null,
                                            Collapsed = type.Display != null ? type.Display.Collapsed : false,
                                            Order = type.Display != null ? type.Display.Order : -10000,
                                            Browsable = type.IsBrowsable,
                                        };
                                        def.Plugins.Add(plugin);
                                    }
                                }
                                def.DependentAssemblies = assembly.References.ToList();
                            }
                            else
                            {
                                // This error could be critical since assembly dependencies won't be found.
                                log.Warning($"Could not load plugins for '{fullPath}'");
                            }
                        }
                        catch (BadImageFormatException)
                        {
                            // unable to load file. Ignore this error, it is probably not a .NET dll.
                        }
                    }
                }
            }
        }

        private static bool IsDotNetAssembly(string fullPath)
        {
            try
            {
                if (File.Exists(fullPath))
                {
                    AssemblyName testAssembly = AssemblyName.GetAssemblyName(fullPath);
                    return true;
                }
            }
            catch (Exception)
            {
                
            }
            return false;
        }

        // TypeData has a PluginTypes property which contains the list of plugins
        // which it implements. ITypeData does not. Recursively iterate
        // the basetype hierarchy until we find a TypeData which has populated this property,
        // or until we find an ITypeData which inherits directly from ITapPlugin
        static ITypeData[] tdPluginTypes(ITypeData td)
        {
            if (td.BaseType == null || td.BaseType == td) return Array.Empty<ITypeData>();
            if (td.BaseType.IsA(typeof(ITapPlugin))) return new[] { td };
            if (td is TypeData t)
            {
                // If PluginTypes is null, it could be a dynamic typedata whose
                // basetype has actual pluginTypes (seen with the Python plugin)
                if (t.PluginTypes == null) return tdPluginTypes(td.BaseType);
                // Otherwise if it does have plugin types, we can sim
                return t.PluginTypes.Cast<ITypeData>().ToArray();
            }

            return tdPluginTypes(td.BaseType);
        }
        
        private static void EnumerateAdditionalPlugins(PackageDef pkgDef)
        {
            string normalize(string s) => s.ToLowerInvariant().Replace("\\", "/").Replace("//", "/");
            // Create a lookup of all plugin sources based on source file
            var sources = TypeData.GetDerivedTypes<ITapPlugin>().Where(t => t.CanCreateInstance).Select(TypeData.GetTypeDataSource).Where(src => src.GetType() != typeof(AssemblyData))
                .ToLookup(src => normalize(Path.GetFullPath(src.Location)));
            if (sources.Count == 0) return;

            var tapdir = ExecutorClient.ExeDir;
            var workDir = EngineSettings.StartupDir;
            foreach (var file in pkgDef.Files)
            {
                ITypeDataSource[] sourceList = Array.Empty<ITypeDataSource>();
                if (!string.IsNullOrWhiteSpace(file.RelativeDestinationPath))
                    sourceList = sources[normalize(Path.Combine(tapdir, file.RelativeDestinationPath))].ToArray();
                if (sourceList.Length == 0 && !string.IsNullOrWhiteSpace(file.FileName))
                    sourceList = sources[normalize(Path.Combine(tapdir, file.FileName))].ToArray();
                
                // look in the working directory - tap package create is relative to that.
                if (sourceList.Length == 0 && !string.IsNullOrWhiteSpace(file.FileName))
                    sourceList = sources[normalize(Path.Combine(workDir, file.FileName))].ToArray();
                
                if (sourceList.Length == 0) continue;

                // discinct by provider type (if a file has more than one plugin in it from the same provider, that provider is going to be in the list several times)
                sourceList = sourceList.GroupBy(src => src.GetType()).Select(grp => grp.First()).ToArray();
                
                // Iterate all the sources that have generated plugins based on this file
                foreach (var source in sourceList)
                {
                    // Add any relevant dependencies. Note that we only add dependencies on AssemblyData implementations,
                    // It would probably be more correct if PackageFile.DependentAssemblies was called something like
                    // PackageFile.PluginFileDependencies, and had a list of ITypeDataSource instead.
                    var dependencies = source.References.ToArray();
                    file.DependentAssemblies.AddRange(dependencies.OfType<AssemblyData>());
                    file.DependentTypeDataSources.AddRange(dependencies.Where(x => !(x is AssemblyData)));
                    
                    string bestName(ITypeData t, DisplayAttribute display = null)
                    {
                        display ??= t.GetDisplayAttribute();
                        return display?.Name ?? t.Name.Split('.', '+').Last();
                    }
                    
                    foreach (var td in source.Types)
                    {
                        try
                        {
                            if (td.CanCreateInstance == false) continue;
                            void addTypeDataDependencies(ITypeData td2)
                            {
                                if (td2 == null) return;
                                
                                var src = TypeData.GetTypeDataSource(td2);
                                if (src.Location != null)
                                {
                                    if (file.DependentTypeDataSources.Contains(src))
                                        return;
                                    file.DependentTypeDataSources.Add(src);
                                } 
                                addTypeDataDependencies(td2.BaseType);
                            }
                            addTypeDataDependencies(td.BaseType);
                            var display = td.GetDisplayAttribute();
                            var pluginTypes = tdPluginTypes(td);
                            var plug = new PluginFile()
                            {
                                BaseType = string.Join(" | ", pluginTypes.Select(t => bestName(t))),
                                Type = td.Name,
                                Name = bestName(td, display),
                                Description = display?.Description ?? "",
                                Groups = display?.Group,
                                Collapsed = display?.Collapsed ?? false,
                                Order = display?.Order ?? -10000,
                                Browsable = td.GetAttribute<BrowsableAttribute>()?.Browsable ?? true,
                            };
                            file.Plugins.Add(plug);
                        }
                        catch (Exception ex)
                        {
                            log.Error($"Failed to add plugins from '{td.Name}'");
                            log.Debug(ex);
                        }
                    }
                }
                
                // Remove duplicated dependencies (very unlikely)
                file.DependentAssemblies = file.DependentAssemblies.Distinct().ToList();
            }
        }

        /// <summary>
        /// Load from an XML package definition file. 
        /// This file is not expected to have info about the plugins in it, so this method will enumerate the plugins inside each dll by loading them.
        /// </summary>
        /// <param name="xmlFilePath">The Package Definition xml file. Usually named package.xml</param>
        /// <param name="projectDir">Directory used byt GitVersionCalculator to expand any $(GitVersion) macros in the XML file.</param>
        /// <returns></returns>
        public static PackageDef FromInputXml(string xmlFilePath, string projectDir)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var evaluator = new PackageXmlPreprocessor(xmlFilePath, projectDir);
                var xmlDoc = evaluator.Evaluate();
                var evaluated = Path.GetTempFileName();
                xmlDoc.Save(evaluated);
                xmlFilePath = evaluated;
                log.Debug(sw, $"Package preprocessing completed.");
            }
            catch (Exception ex)
            {
                log.Warning(ex.Message);
                log.Debug($"Unexpected error while evaluating package xml. Continuing in spite of errors.");
                log.Debug(ex);
            }

            PackageDef.ValidateXml(xmlFilePath);
            var pkgDef = PackageDef.FromXml(xmlFilePath);
            if(pkgDef.Files.Any(f => f.HasCustomData<UseVersionData>() && f.HasCustomData<SetAssemblyInfoData>()))
                throw new InvalidDataException("A file cannot specify <SetAssemblyInfo/> and <UseVersion/> at the same time.");

            pkgDef.Files = expandGlobEntries(pkgDef.Files);

            var excludeAdd = pkgDef.Files.Where(file => file.IgnoredDependencies != null).SelectMany(file => file.IgnoredDependencies).Distinct().ToList();

            List<Exception> exceptions = new List<Exception>();
            foreach (PackageFile item in pkgDef.Files)
            {
                string fullPath = Path.GetFullPath(item.FileName);
                if (!File.Exists(fullPath))
                {
                    string fileName = Path.GetFileName(item.FileName);
                    if (File.Exists(fileName) && item.SourcePath == null)
                    {
                        // this is to support building everything to the root folder. This way the developer does not have to specify SourcePath.
                        log.Warning("Specified file '{0}' was not found, using file '{1}' as source instead. Consider setting SourcePath to remove this warning.", item.FileName,fileName);
                        item.SourcePath = fileName;
                    }
                    else
                        exceptions.Add(new FileNotFoundException("Missing file for package.", fullPath));
                }
            }
            if (exceptions.Count > 0)
                throw new AggregateException("Missing files", exceptions);
            
            pkgDef.Date = DateTime.UtcNow;

            // Copy to output directory first
            foreach(var file in pkgDef.Files)
            {
                if (file.RelativeDestinationPath != file.FileName)
                    try
                    {
                        var destPath = Path.GetFullPath(file.RelativeDestinationPath);
                        if (!File.Exists(destPath))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                            ProgramHelper.FileCopy(file.FileName, destPath);
                        }
                    }
                    catch
                    {
                        // Catching here. The files might be used by themselves
                    }
            }

            var searcher = new PluginSearcher(PluginSearcher.Options.IncludeSameAssemblies);
            searcher.Search(Directory.GetCurrentDirectory());
            List<AssemblyData> assemblies = searcher.Assemblies.ToList();

            // Enumerate plugins if this has not already been done.
            if (!pkgDef.Files.SelectMany(pfd => pfd.Plugins).Any())
            {
                // Enumerate plugin types from C# assemblies
                EnumeratePlugins(pkgDef, assemblies);
                // Enumerate plugin types from other ITypeDataSource implementations
                EnumerateAdditionalPlugins(pkgDef);
            }

            pkgDef.findDependenciesAndHandleOverrides(excludeAdd, assemblies);

            if (exceptions.Count > 0)
                throw new AggregateException("Conflicting dependencies", exceptions);

            return pkgDef;
        }

        internal static List<PackageFile> expandGlobEntries(List<PackageFile> fileEntries)
        {
            List<PackageFile> newEntries = new List<PackageFile>();
            foreach (PackageFile fileEntry in fileEntries)
            {
                // Make SourcePath and RelativeDestinationPath Linux friendly
                if(fileEntry.SourcePath != null && fileEntry.SourcePath.Contains('\\'))
                {
                    log.Info($"File path ({fileEntry.SourcePath}) in package definition contains `\\`, please consider replacing with `/`.");
                    fileEntry.SourcePath = fileEntry.SourcePath.Replace('\\', '/');
                }
                if(fileEntry.RelativeDestinationPath.Contains('\\'))
                {
                    log.Info($"File path ({fileEntry.RelativeDestinationPath}) in package definition contains `\\`, please consider replacing with `/`.");
                    fileEntry.RelativeDestinationPath = fileEntry.RelativeDestinationPath.Replace('\\', '/');
                }

                if (fileEntry.FileName.Contains('*') || fileEntry.FileName.Contains('?'))
                {
                    string[] segments = fileEntry.FileName.Split('/');
                    int fixedSegmentCount = 0;
                    while (fixedSegmentCount < segments.Length)
                    {
                        if (segments[fixedSegmentCount].Contains('*')) break;
                        if (segments[fixedSegmentCount].Contains('?')) break;
                        fixedSegmentCount++;
                    }
                    string fixedDir = ".";
                    if(fixedSegmentCount > 0)
                        fixedDir = String.Join("/", segments.Take(fixedSegmentCount));

                    IEnumerable<string> files = null;
                    if (Directory.Exists(fixedDir))
                    {
                        if (fixedSegmentCount == segments.Length - 1)
                            files = Directory.GetFiles(fixedDir, segments.Last());
                        else
                        {
                            files = Directory.GetFiles(fixedDir, segments.Last(), SearchOption.AllDirectories)
                                                      .Select(f => f.StartsWith(".") ? f.Substring(2) : f); // remove leading "./". GetFiles() adds these chars only when fixedDir="." and they confuse the Glob matching below.
                            var options = new DotNet.Globbing.GlobOptions();
                            if(OperatingSystem.Current == OperatingSystem.Windows)
                                options.Evaluation.CaseInsensitive = false;
                            else
                                options.Evaluation.CaseInsensitive = true;
                            var globber = DotNet.Globbing.Glob.Parse(fileEntry.FileName, options);
                            List<string> matches = new List<string>();
                            foreach (string file in files)
                            {
                                if (globber.IsMatch(file))
                                    matches.Add(file);
                            }
                            files = matches;
                        }
                    }
                    if (files != null && files.Any())
                    {
                        newEntries.AddRange(files.Select(f => new PackageFile
                        {
                            RelativeDestinationPath = f.Replace('\\', '/'),
                            LicenseRequired = fileEntry.LicenseRequired,
                            IgnoredDependencies = fileEntry.IgnoredDependencies,
                            // clone the list to ensure further modifications happens
                            // a the expected place.
                            CustomData = fileEntry.CustomData.ToList() 
                        }));
                    }
                    else
                    {
                        log.Warning("Glob pattern '{0}' did not match any files.", fileEntry.FileName);
                    }
                }
                else
                {
                    newEntries.Add(fileEntry);
                }
            }
            return newEntries;
        }

        internal static IEnumerable<AssemblyData> AssembliesOfferedBy(List<PackageDef> packages, IEnumerable<PackageDependency> refs, bool recursive, PackageAssemblyCache offeredFiles)
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

        internal class PackageAssemblyCache
        {
            readonly List<string> brokenPackageNames = new List<string>();
            readonly List<AssemblyData> searchedFiles;
            readonly Memorizer<PackageDef, List<AssemblyData>> packageAssemblies;
            public PackageAssemblyCache(List<AssemblyData> searchedFiles)
            {
                this.searchedFiles = searchedFiles;
                packageAssemblies = new Memorizer<PackageDef, List<AssemblyData>>(getPackageAssemblies);
            }

            public IEnumerable<AssemblyData> GetPackageAssemblies(PackageDef pkgDef) => packageAssemblies[pkgDef];
            List<AssemblyData> getPackageAssemblies(PackageDef pkgDef)
            {
                List<AssemblyData> output = new List<AssemblyData>();
                foreach(var f in pkgDef.Files)
                {
                    var asms = searchedFiles.Where(sf => PathUtils.AreEqual(f.FileName, sf.Location))
                        .Where(sf => IsDotNetAssembly(sf.Location)).ToList();
                    if (asms.Count == 0 && (Path.GetExtension(f.FileName).Equals(".dll", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(f.FileName).Equals(".exe", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (File.Exists(f.FileName) && !PathUtils.DecendsFromOpenTapIgnore(f.FileName))
                        {
                            // If the pluginSearcher found assemblies that are located somewhere not expected by the package definition, the package might appear broken.
                            // But if the file found by the pluginSearcher is the same as the one expected by the package definition we should not count it as broken.
                            // This could cause a package to not be added as a dependencies. 
                            // E.g. when debugging and the OpenTAP.Cli.dll is both in the root build dir and in "Packages/OpenTAP"
                            var asmsIdenticalFilename = searchedFiles.Where(sf => Path.GetFileName(f.FileName) == Path.GetFileName(sf.Location));
                            var asmsIdentical = asmsIdenticalFilename.Where(sf => PathUtils.CompareFiles(f.FileName, sf.Location));
                            output.AddRange(asmsIdentical);
                            continue;
                        }

                        if (!brokenPackageNames.Contains(pkgDef.Name) && IsDotNetAssembly(f.FileName))
                        {
                            brokenPackageNames.Add(pkgDef.Name);
                            log.Warning($"Package '{pkgDef.Name}' is not installed correctly?  Referenced file '{f.FileName}' was not found.");
                        }
                    }
                    output.AddRange(asms);
                }
                return output;
            }

            public void Clear(PackageDef pkg) => packageAssemblies.Invalidate(pkg);
        }
        
        private static void VerifyDependenciesInstalled(this PackageDef pkg)
        {
            var currentInstallation = Installation.Current;
            var installed = currentInstallation.GetPackages().Where(p => p.Name != pkg.Name).ToList();
            // check versions of any hardcoded dependencies against what is currently installed
            foreach(PackageDependency dep in pkg.Dependencies)
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
                            throw new ExitCodeException((int)PackageExitCodes.PackageDependencyError, $"Installed version of {dep.Name} ({installedPackage.Version}) is incompatible with dependency specified in package definition ({dep.Version}).");
                    }
                }
                else
                {
                    throw new ExitCodeException((int)PackageExitCodes.PackageDependencyError, 
                        $"Package dependency '{dep.Name}' specified in package definition is not installed. Please install a compatible version first.");
                }
            }
            
        }

        internal static void findDependenciesAndHandleOverrides(this PackageDef pkgDef, List<string> excludeAdd,
            List<AssemblyData> searchedFiles)
        { 
            // The dependencies of a package greatly influence dependency resolution.
            // This is because a package depending on e.g. 'OpenTAP' is treated as if it provides all the DLLs provided by OpenTAP.
            // This is useful because a dependency on e.g. `OpenTap.dll' can be resolved by adding a dependency on OpenTAP,
            // but it has the side effect of allowing a package to provide its version of any dll provided by OpenTAP.
            // As a workaround for this, we clear manual dependencies before resolution, and re-add them afterwards.
            var manualDependencies = pkgDef.Dependencies.ToArray();
            pkgDef.Dependencies.Clear();
            pkgDef.findDependencies(excludeAdd, searchedFiles);
            foreach (var md in manualDependencies)
            {
                var existing = pkgDef.Dependencies.FirstOrDefault(x => x.Name == md.Name);
                if (existing == null)
                {
                    pkgDef.Dependencies.Add(md);
                }
                else
                {
                    existing.Version = md.Version;
                }
            } 
            pkgDef.VerifyDependenciesInstalled();
        } 
        
        internal static void findDependencies(this PackageDef pkg, List<string> excludeAdd, List<AssemblyData> searchedFiles)
        {
            var searcher = new PackageAssemblyCache(searchedFiles);
            
            // First update the pre-entered dependencies            
            bool foundNew = false;
            var notFound = new HashSet<string>();

            var currentInstallation = Installation.Current;
            var installed = currentInstallation.GetPackages().Where(p => p.Name != pkg.Name).ToList();

            {
                
                // add dependencies which are from different type data sources than AssemblyData (.NET).
                foreach (var file in pkg.Files)
                {
                    foreach (var dep in file.DependentTypeDataSources)
                    {
                        var pkg2 = currentInstallation.FindPackageContainingFile(dep.Location);
                        if (pkg2 == null)
                        {
                            log.Debug($"Dependency without a source package dependency: {dep.Name}, depender: {file.FileName}");
                            continue;
                        }
                        if (pkg.Dependencies.Any(dep => dep.Name == pkg2.Name))
                        {
                            continue;
                        }
                        if (pkg.Name == pkg2.Name) // don't have the package depend on itself. This can happen if the package.xml getting created is already in a location as if it was installed (e.g. ./Packages/<pluginname>/package.xml)
                            continue;
                        PackageDependency pd = new PackageDependency(pkg2.Name, new VersionSpecifier(pkg2.Version, VersionMatchBehavior.Compatible));
                        pkg.Dependencies.Add(pd);
                    }
                }
            }
            
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
                        // Check if the package depends on the package being built.
                        // Circular dependencies are not supported, so don't add it.
                        // in the best of worlds, we'd recurse to find if there is a dependency
                        // further up the tree, but that seems like a very unlikely situation, so 
                        // we skip that for now.
                        if (f.Dependencies.Any(dep => dep.Name == pkg.Name))
                            continue;
                        
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
                        foreach(AssemblyData candidateAsm in searcher.GetPackageAssemblies(candidatePkg))
                        {
                            var requiredAsm = dependentAssemblyNames.FirstOrDefault(dep => dep.Name == candidateAsm.Name);
                            if (requiredAsm != null)
                            {
                                if (candidateAsm.Version == null)
                                    throw new Exception($"Assembly {candidateAsm} version is null");
                                if (requiredAsm.Version == null)
                                    throw new Exception($"Assembly {requiredAsm} version is null");
							    if(OpenTap.Utils.Compatible(candidateAsm.Version, requiredAsm.Version))
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
                                        log.Error($"This package require assembly {requiredAsm.Name} in version {requiredAsm.Version} while that assembly is already installed through package '{candidatePkg.Name}' in version {candidateAsm.Version}.");
                                    else
                                        log.Error($"{Path.GetFileName(depender.FileName)} in this package require assembly {requiredAsm.Name} in version {requiredAsm.Version} while that assembly is already installed through package '{candidatePkg.Name}' in version {candidateAsm.Version}.");
                                    //log.Error($"Please align the version of {requiredAsm.Name} to ensure interoperability with package '{candidate.Key.Name}' or uninstall that package.");
                                    throw new ExitCodeException((int)PackageExitCodes.AssemblyDependencyError, 
                                                                $"Please align the version of {requiredAsm.Name} ({candidateAsm.Version} vs {requiredAsm.Version})  to ensure interoperability with package '{candidatePkg.Name}' or uninstall that package.");
                                }
                            }
                        }
                        if (foundNew)
                        {
                            // Check if a circular dependency gets added into the package.
                            // This should only happen in case of an error. Continuing will cause undefined behavior.
                            var circularDependency = candidatePkg.Dependencies.FirstOrDefault(x => x.Name == pkg.Name);
                            if (circularDependency != null)
                                throw new Exception("A package cannot depend on another package that depends on it. Circular dependencies are not supported.");

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
            }
            while (foundNew);
        }

        private static void AddFileDependencies(PackageDef pkg, AssemblyData dependency, AssemblyData foundAsm)
        {
            var depender = pkg.Files.FirstOrDefault(f => f.DependentAssemblies.Contains(dependency));
            var destPath = string.Format("Dependencies/{0}.{1}/{2}", Path.GetFileNameWithoutExtension(foundAsm.Location), foundAsm.Version.ToString(), Path.GetFileName(foundAsm.Location));
            if (pkg.Files.Any(x => x.RelativeDestinationPath == destPath))
                return;//throw new Exception("File already added to package: " + destPath);
            if (depender == null)
                log.Warning("Adding dependent assembly '{0}' to package. It was not found in any other packages.", Path.GetFileName(foundAsm.Location));
            else
                log.Info($"'{Path.GetFileName(depender.FileName)}' depends on '{dependency.Name}' version '{dependency.Version}'. Adding dependency to package, it was not found in any other packages.");


            pkg.Files.Add(new PackageFile { SourcePath = foundAsm.Location, RelativeDestinationPath = destPath, DependentAssemblies = foundAsm.References.ToList() });

            // Copy the file to the actual directory so we can rely on it actually existing where we say the package has it.
            if (!File.Exists(destPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                ProgramHelper.FileCopy(foundAsm.Location, destPath);
            }
        }

        /// <summary>
        /// Creates a *.TapPackage file from the definition in this PackageDef.
        /// </summary>
        public static void CreatePackage(this PackageDef pkg, FileStream str)
        {
            foreach (PackageFile file in pkg.Files)
            {
                if (!File.Exists(file.FileName))
                {
                    log.Error("{0}: File '{1}' not found", pkg.Name, file.FileName);
                    throw new InvalidDataException();
                }
            }

            if (pkg.Files.Any(s => s.HasCustomData<MissingPackageData>()))
            {
                bool resolved = true;
                foreach (var file in pkg.Files)
                {
                    while (file.HasCustomData<MissingPackageData>())
                    {
                        MissingPackageData missingPackageData = file.GetCustomData<MissingPackageData>().FirstOrDefault();
                        if (missingPackageData.TryResolve(out ICustomPackageData customPackageData))
                        {
                            file.CustomData.Add(customPackageData);
                        }
                        else
                        {
                            resolved = false;
                            log.Error($"Missing plugin to handle XML Element '{missingPackageData.XmlElement.Name.LocalName}' on file {file.FileName}. (Line {missingPackageData.GetLine()})");
                        }
                        file.CustomData.Remove(missingPackageData);
                    }
                }
                if (!resolved)
                    throw new ArgumentException("Missing plugins to handle XML elements specified in input package.xml...");
            }

            
            string tempDir = Path.GetTempPath() + Path.GetRandomFileName();
            Directory.CreateDirectory(tempDir);
            log.Debug("Using temporary folder at '{0}'", tempDir);
            try
            {
                UpdateVersionInfo(tempDir, pkg.Files, pkg.Version);

                // License Inject
                // Obfuscate
                // Sign
                CustomPackageActionHelper.RunCustomActions(pkg, PackageActionStage.Create, new CustomPackageActionArgs(tempDir, false));

                // Concat license required from all files. But only if the property has not manually been set.
                if (string.IsNullOrEmpty(pkg.LicenseRequired))
                {
                    var licenses = pkg.Files.Select(f => f.LicenseRequired).Where(l => string.IsNullOrWhiteSpace(l) == false).ToList();
                    pkg.LicenseRequired = string.Join(", ", licenses.Distinct().Select(l => LicenseBase.FormatFriendly(l, false)).ToList());
                }
                
                log.Info("Creating OpenTAP package.");
                pkg.Compress(str, pkg.Files);
                
                // The filestream is opened with the 'DeleteOnClose' flag. If we throw here,
                // the file pointed to by the stream will be deleted.
                if (!VerifyArchiveIntegrity(str))
                    throw new ExitCodeException((int)PackageExitCodes.InvalidPackageArchive,
                        "Failed to verify the integrity of the created package.");
            }
            finally
            {
                FileSystemHelper.DeleteDirectory(tempDir);
            }
        }

        [Display("SetAssemblyInfo")]
        public class SetAssemblyInfoData : ICustomPackageData
        {
            [XmlAttribute]
            public string Attributes { get; set; }

            internal string[] Features => Attributes.ToLower()
                .Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Distinct().ToArray();
        }

        private static void UpdateVersionInfo(string tempDir, List<PackageFile> files, SemanticVersion version)
        {
            var timer = Stopwatch.StartNew();

            var pdbMap = new Dictionary<string, PackageFile>();
            foreach (var file in files.GroupBy(f => Path.GetFileNameWithoutExtension(f.FileName)))
            {
                var symbols = file.ToArray().FirstOrDefault(f => Path.GetExtension(f.FileName).Equals(".pdb", StringComparison.OrdinalIgnoreCase));
                if (symbols != null) pdbMap[file.Key] = symbols;
            }

            foreach (var file in files)
            {
                var data = file.GetCustomData<SetAssemblyInfoData>().ToArray();
                if (!data.Any(d => d.Features.Contains("version"))) continue;

                log.Debug(timer, "Updating version info for '{0}'", file.FileName);

                // Assume we can't open the file for writing (could be because we are trying to modify TPM or the engine), and copy to the same filename in a subdirectory
                var versionedOutput = Path.Combine(tempDir, "Versioned");

                var origFilename = Path.GetFileName(file.FileName);
                var tempName = Path.Combine(versionedOutput, origFilename);

                int i = 1;
                while (File.Exists(tempName))
                {
                    tempName = Path.Combine(versionedOutput, origFilename + i.ToString());
                    i++;
                }


                Directory.CreateDirectory(Path.GetDirectoryName(tempName));
                ProgramHelper.FileCopy(file.FileName, tempName);
                file.SourcePath = tempName;

                var includePdb = true;

                var basename = Path.GetFileNameWithoutExtension(file.SourcePath);
                if (pdbMap.TryGetValue(basename, out var pdbFile) &&
                    Path.GetFileName(pdbFile.FileName) is string symbolsFile && File.Exists(symbolsFile))
                {
                    var pdbTempName = Path.ChangeExtension(tempName, "pdb");
                    File.Copy(symbolsFile, pdbTempName);
                    pdbFile.SourcePath = pdbTempName;
                }
                else
                {
                    // The pdb file is not part of the package -- don't include it
                    includePdb = false;
                }

                var fVersion = version;
                var fVersionShort = new Version(version.ToString(3));

                SetAsmInfo.SetAsmInfo.SetInfo(file.FileName, fVersionShort, fVersionShort, fVersion, includePdb);
                file.RemoveCustomData<SetAssemblyInfoData>();
            }
            log.Info(timer,"Updated assembly version info using Mono method.");
        }

        private static bool VerifyArchiveIntegrity(FileStream fs)
        {
            var sw = Stopwatch.StartNew();
            log.Debug($"Verifying package integrity...");
            // Ensure the stream is fully written to the disk
            fs.Flush();

            {   // Verify that we can read the package definition
                try
                {
                    PackageDef.FromPackage(fs.Name);
                }
                catch (Exception ex)
                {
                    log.Error($"Error verifying package integrity.");
                    log.Debug(ex);
                    return false;
                }
            }

            log.Debug(sw, "Verified metadata integrity.");

            // Rewind the stream
            var files = new HashSet<string>();
            fs.Seek(0, SeekOrigin.Begin);
            // Verify that we can extract the archive without errors
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read, true))
            {
                foreach (var part in zip.Entries)
                {
                    try
                    {
                        if (!files.Add(part.FullName))
                        {
                            log.Error($"Error verifying archive integrity. " + 
                                      $"Archive contains a duplicate entry '{part.FullName}'." +
                                      $"Please retry the package creation. " +
                                      $"If the error persists, consider creating an issue.");
                            return false;
                        }
                        // Read the stream to the end to verify it can be extracted
                        part.Open().CopyTo(Stream.Null);
                    }
                    catch (InvalidDataException)
                    {
                        log.Error($"Error verifying package integrity. " +
                                  $"Archive entry '{part.FullName}' is corrupted. " +
                                  $"Please retry the package creation. " +
                                  $"If the error persists, consider creating an issue.");
                        return false;
                    }
                }
            }

            log.Debug(sw, $"Verified archive integrity.");
            return true;
        }

        /// <summary>
        /// Compresses the files to a zip package.
        /// </summary>
        private static void Compress(this PackageDef pkg, FileStream outStream, IEnumerable<PackageFile> inputPaths)
        {
            using (var zip = new System.IO.Compression.ZipArchive(outStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (PackageFile file in inputPaths)
                {
                    var sw = Stopwatch.StartNew();
                    var relFileName = file.RelativeDestinationPath;
                    var ZipPart = zip.CreateEntry(relFileName, System.IO.Compression.CompressionLevel.Optimal);
                    ZipPart.FixUnixPermissions();
                    
                    using (var instream = File.OpenRead(file.FileName))
                    {
                        using (var outstream = ZipPart.Open())
                        {
                            var compressTask = instream.CopyToAsync(outstream, 2048, TapThread.Current.AbortToken);
                            ConsoleUtils.PrintProgressTillEnd(compressTask, "Compressing", () => instream.Position, () => instream.Length);
                        }
                    }
                    log.Debug(sw, "Compressed '{0}'", file.FileName);
                }

                // add the metadata xml file:

                var metaPart = zip.CreateEntry(String.Join("/", PackageDef.PackageDefDirectory, pkg.Name, PackageDef.PackageDefFileName), System.IO.Compression.CompressionLevel.Optimal);
                metaPart.FixUnixPermissions();
                using(var str = metaPart.Open())
                    pkg.SaveTo(str);
            }
            outStream.Flush();
        }
    }
}
