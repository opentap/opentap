//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using Tap.Shared;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace OpenTap.Package
{
    public static class PackageDefExt
    {
        //
        // Note: There is some code duplication between PackageDefExt and PackageDef, 
        // but usually PackageDefExt does something in addition to what PackageDef does.
        //

        static TraceSource log =  OpenTap.Log.CreateSource("Package");

        private static void EnumeratePlugins(this PackageFile def)
        {
            if (def.Plugins == null || !def.Plugins.Any())
            {
                if (File.Exists(def.FileName) && IsAssembly(def.FileName))
                {
                    try
                    {
                        string fullPath = Path.GetFullPath(def.FileName);
                        if(File.Exists(def.RelativeDestinationPath))
                            // if the file is already in its destination dir use that inatead.
                            // That file is much more likely to be inside the OpenTAP dir we already searched.
                            fullPath = Path.GetFullPath(def.RelativeDestinationPath);
                        string dir = Path.GetDirectoryName(fullPath);

                        if (!PluginManager.DirectoriesToSearch.Any(sd => dir.StartsWith(sd)))
                        {
                            log.Debug($"DirectoriesToSearch did not contain {dir}. Searching again.");
                            PluginManager.DirectoriesToSearch.Add(dir);
                            PluginManager.SearchAsync(); // start a (new) search
                        }

                        var searchedAssemblies = PluginManager.GetSearchedAssemblies();
                        AssemblyData assembly = searchedAssemblies.FirstOrDefault(a => PathUtils.AreEqual(a.Location, fullPath));

                        if (assembly == null && PluginManager.DirectoriesToSearch.Any(sd => fullPath.StartsWith(sd)))
                        {
                            log.Debug($"Assembly '{fullPath}' was not found. Searching again.");

                            // The file is in the search dir but was not searched, 
                            // it was probably added after we searched, try searching again
                            PluginManager.SearchAsync(); // start a (new) search
                            searchedAssemblies = PluginManager.GetSearchedAssemblies();
                            assembly = searchedAssemblies.FirstOrDefault(a => PathUtils.AreEqual(a.Location, fullPath));
                        }
                        if(assembly != null)
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

                            if(assembly.PluginTypes != null)
                            {
                                foreach (TypeData type in assembly.PluginTypes)
                                {
                                    if (type.Attributes.HasFlag(TypeAttributes.Interface) || type.Attributes.HasFlag(TypeAttributes.Abstract))
                                        continue;
                                    PluginFile plugin = new PluginFile
                                    {
                                        BaseType = string.Join(" | ", type.PluginTypes.Select(t => t.GetBestName())),
                                        Type = type.FullName,
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
                            def.DependentAssemblyNames = assembly.References.ToList();
                        }
                        else
                        {
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

        private static bool IsAssembly(string fullPath)
        {
            try
            {
                AssemblyName testAssembly = AssemblyName.GetAssemblyName(fullPath);
                return true;
            }

            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Load from an XML package definition file. 
        /// This file is not expected to have info about the plugins in it, so this method will enumerate the plugins inside each dll by loading them.
        /// </summary>
        public static PackageDef FromXmlFile(string xmlFilePath, bool findDependencies = true, string ProjectDirectory = null, bool expandVersion = true)
        {
            PackageDef.ValidateXml(xmlFilePath);
            var pkgDef = PackageDef.FromXmlFile(xmlFilePath);
            if(pkgDef.Files.Any(f => f.HasCustomData<UseVersionData>() && f.HasCustomData<SetAssemblyInfoData>() && !String.IsNullOrEmpty(f.GetCustomData<SetAssemblyInfoData>().Attributes)))
                throw new InvalidDataException("A file cannot specify <SetAssemblyInfo/> and <UseVersion/> at the same time.");

            var excludeAdd = pkgDef.Files.Where(file => file.IgnoredDependencies != null).SelectMany(file => file.IgnoredDependencies).Distinct().ToList();

            List<Exception> exceptions = new List<Exception>();
            if (findDependencies || (pkgDef.Version == null && pkgDef.Files.Any(pf => pf.HasCustomData<UseVersionData>()))) // if this is an xml file that has already been through Tap.Package to have its dependencies, version, etc. set, then there is no need for the files to be there now.
            {
                foreach (PackageFile item in pkgDef.Files)
                {
                    string fullPath = Path.GetFullPath(item.FileName);
                    if (!File.Exists(fullPath))
                    {
                        string fileName = Path.GetFileName(item.FileName);
                        if (File.Exists(fileName))
                        {
                            // this is to support building everything to the root folder. This way the developer does not have to specify SourcePath.
                            log.Info("Specified file '{0}' was not found, using file '{1}' as source instead. Consider setting SourcePath to remove this warning.", item.FileName,fileName);
                            item.SourcePath = fileName;
                        }
                        else
                            exceptions.Add(new FileNotFoundException("", fullPath));
                    }
                }
                if (exceptions.Count > 0) throw new AggregateException("Missing files", exceptions);
            }

            if (expandVersion)
            {
                pkgDef.updateVersion(ProjectDirectory);
            }
            pkgDef.Date = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture);
            
            if (findDependencies)
            {
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
            }

            // Enumerate plugins if this has not already been done.
            if (!pkgDef.Files.SelectMany(pfd => pfd.Plugins).Any())
            {
                foreach (PackageFile item in pkgDef.Files)
                {
                    item.EnumeratePlugins();
                }
            }

            if (findDependencies)
            {
                pkgDef.findDependencies(excludeAdd);
            }

            return pkgDef;
        }
        
        private static string TryReplaceMacro(string text, string ProjectDirectory, string prereleaseOverride)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Find macro
            var match = Regex.Match(text, @"(.*)(?:\$\()(.*)(?:\))(.*)");
            if (match == null || match.Groups.Count < 2)
                return text;

            // Replace macro
            if (match.Groups[2].ToString() == "GitVersion")
            {
                using (GitVersionCalulator calc = new GitVersionCalulator(ProjectDirectory))
                {
                    SemanticVersion version = calc.GetVersion();
                    text = match.Groups[1].ToString() + version + match.Groups[3].ToString();
                }
            }
            else
                throw new NotSupportedException(string.Format("The macro \"{0}\" is not supported.", match.Groups[2].ToString()));

            // Find "pre-processor funcions"
            match = Regex.Match(text, @"(.*)(?:\$\()(.*?),(.*)(?:\))(.*)"); // With text = "Rolf$(ConvertFourValue,1.0.0.69)Asger", this regex should return 4 matching groups: "Rolf", "ConvertFourValue", "1.0.0.69" and "Asger".

            if (match == null || match.Groups.Count < 4 || !match.Groups[2].Success || !match.Groups[3].Success)
                return text;

            var plugins = PluginManager.GetPlugins<IVersionConverter>();
            Type converter = plugins.FirstOrDefault(pt => pt.GetDisplayAttribute().Name == match.Groups[2].Value);
            if(converter != null)
            {
                IVersionConverter cvt = (IVersionConverter)Activator.CreateInstance(converter);
                SemanticVersion convertedVersion = cvt.Convert(match.Groups[3].Value);
                text = match.Groups[1].Value + convertedVersion + match.Groups[4].Value;
                log.Warning("The version was converted from {0} to {1} using the converter '{2}'.", match.Groups[3].Value, convertedVersion, converter.GetDisplayAttribute().Name);
            }
            else
                throw new Exception(string.Format("No IVersionConverter found named \"{0}\". Valid ones are: {1}", match.Groups[2].Value, 
                    String.Join(", ", plugins.Select(p => $"\"{p.GetDisplayAttribute().Name}\""))));
            return text;
        }

        internal static void updateVersion(this PackageDef pkg, string ProjectDirectory, string prereleaseOverride = null)
        {
            // Replace macro if possible
            pkg.RawVersion = TryReplaceMacro(pkg.RawVersion, ProjectDirectory, prereleaseOverride);

            
            if (pkg.RawVersion == null)
            {
                foreach (var file in pkg.Files.Where(file => file.HasCustomData<UseVersionData>()))
                {
                    pkg.Version = PluginManager.GetSearchedAssemblies().FirstOrDefault(a => Path.GetFullPath(a.Location) == Path.GetFullPath(file.FileName)).SemanticVersion;
                    break;
                }
            }
            else
            {
                if (String.IsNullOrWhiteSpace(pkg.RawVersion))
                    pkg.Version = new SemanticVersion(0, 0, 0, null, null);
                else if (SemanticVersion.TryParse(pkg.RawVersion, out var semver))
                {
                    pkg.Version = semver;
                }
                else
                {
                    throw new FormatException("The version string in the package is not a valid semantic version.");
                }
            }
        }

        internal static IEnumerable<AssemblyData> AssembliesOfferedBy(List<PackageDef> packages, IEnumerable<PackageDependency> refs, bool recursive, Memorizer<PackageDef, List<AssemblyData>> offeredFiles)
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

                        offeredFiles[pkg].ToList().ForEach(f => files.Add(f));
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

        internal static void findDependencies(this PackageDef pkg, List<string> excludeAdd)
        {
            bool foundNew = false;

            var notFound = new HashSet<string>();

            var searcher = new PluginSearcher();
            searcher.Search(Directory.GetCurrentDirectory());
            // First update the pre-entered dependencies
            var installed = new Installation(Directory.GetCurrentDirectory()).GetPackages().Where(p => p.Name != pkg.Name).ToList();
            
            var searchedFiles = searcher.Assemblies.ToList();
            List<string> brokenPackageNames = new List<string>();
            var packageAssemblies = new Memorizer<PackageDef, List<AssemblyData>>(pkgDef => pkgDef.Files.SelectMany(f =>
            {
                var asms = searchedFiles.Where(sf => PathUtils.AreEqual(f.FileName, sf.Location)).ToList();
                if (asms.Count == 0 && (Path.GetExtension(f.FileName).Equals(".dll", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(f.FileName).Equals(".exe", StringComparison.OrdinalIgnoreCase)))
                {
                    if (!brokenPackageNames.Contains(pkgDef.Name))
                    {
                        brokenPackageNames.Add(pkgDef.Name);
                        log.Warning($"Package '{pkgDef.Name}' is not installed correctly?  Referenced file '{f.FileName}' was not found.");
                    }
                }
                return asms;
            }).ToList());

            var missingPackage = new List<string>();

            // TODO: figure out if this is ever needed
            //foreach (var dep in pkg.Dependencies)
            //{
            //    if (dep.Version == null)
            //    {
            //        var current = installed.FirstOrDefault(ip => ip.Name == dep.Name);

            //        if (current == null)
            //            missingPackage.Add(dep.Name);
            //        else
            //            dep.Version = new VersionSpecifier(current.Version, VersionMatchBehavior.Compatible);
            //    }
            //}

            if (missingPackage.Any())
                throw new Exception(string.Format("A number of packages could not be found while updating package dependency versions: {0}", string.Join(", ", missingPackage)));

            // Find additional dependencies
            do
            {
                foundNew = false;

                // Find everything we already know about
                var offeredByDependencies = AssembliesOfferedBy(installed, pkg.Dependencies, false, packageAssemblies).ToList();
                var offeredByThis = packageAssemblies[pkg]
                    .Where(f => f != null)
                    .ToList();

                var anyOffered = offeredByDependencies.Concat(offeredByThis).ToList();

                // Find our dependencies and subtract the above two lists
                var dependentAssemblyNames = pkg.Files
                    .SelectMany(fs => fs.DependentAssemblyNames)
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
                        packageCandidates[f] = packageAssemblies[f]
                            .Count(asm => dependentAssemblyNames.Any(dep => (dep.Name == asm.Name) && OpenTap.Utils.Compatible(asm.Version, dep.Version)));
                    }

                    var candidate = packageCandidates.OrderByDescending(k => k.Value).FirstOrDefault();

                    if (packageCandidates.Any() && (candidate.Value > 0))
                    {
                        var offeredFiles = packageAssemblies[candidate.Key]
                            .Where(asm => dependentAssemblyNames.Any(dep => (dep.Name == asm.Name && OpenTap.Utils.Compatible(asm.Version, dep.Version))))
                            .Select(ad => ad.Name).Distinct()
                            .ToList();
                        log.Info("Adding dependency on package '{0}' version {1}", candidate.Key.Name, candidate.Key.Version);
                        log.Info("It offers: " + string.Join(", ", offeredFiles));

                        PackageDependency pd = new PackageDependency(candidate.Key.Name, new VersionSpecifier(candidate.Key.Version, VersionMatchBehavior.Compatible));
                        pkg.Dependencies.Add(pd);

                        foundNew = true;
                    }
                    else
                    {
                        // Otherwise add them if we can find any that are compatible
                        foreach (var unknown in dependentAssemblyNames)
                        {
                            var foundAsms = searchedFiles.Where(asm => (asm.Name == unknown.Name) && OpenTap.Utils.Compatible(asm.Version, unknown.Version)).ToList();
                            var foundAsm = foundAsms.FirstOrDefault();

                            if (foundAsm != null)
                            {
                                var depender = pkg.Files.FirstOrDefault(f => f.DependentAssemblyNames.Contains(unknown));
                                if (depender == null)
                                    log.Warning("Adding dependent assembly '{0}' to package. It was not found in any other packages.", Path.GetFileName(foundAsm.Location));
                                else
                                    log.Info($"'{Path.GetFileName(depender.FileName)}' dependents on '{unknown.Name}' version '{unknown.Version}'. Adding dependency to package, it was not found in any other packages.");

                                var destPath = string.Format("Dependencies/{0}.{1}/{2}", Path.GetFileNameWithoutExtension(foundAsm.Location), foundAsm.Version.ToString(), Path.GetFileName(foundAsm.Location));
                                pkg.Files.Add(new PackageFile { FileName = foundAsm.Location, RelativeDestinationPath = destPath, DependentAssemblyNames = foundAsm.References.ToList() });

                                // Copy the file to the actual directory so we can rely on it actually existing where we say the package has it.
                                if (!File.Exists(destPath))
                                {
                                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                                    ProgramHelper.FileCopy(foundAsm.Location, destPath);
                                }

                                packageAssemblies.Invalidate(pkg);

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

        /// <summary>
        /// Creates a *.TapPlugin package file from the definition in this PackageDef.
        /// </summary>
        /// <param name="path">Output file path.</param>
        /// <param name="path">Project directory.</param>
        /// <param name="skipObfuscation">Override to disable all obfuscation.</param>
        static public void CreatePackage(this PackageDef pkg, string path, string projectDir, bool skipObfuscation = false, string prereleaseOverride = null)
        {
            foreach (PackageFile file in pkg.Files)
            {
                if (!File.Exists(file.FileName))
                {
                    log.Error("{0}: File '{1}' not found", pkg.Name, file.FileName);
                    throw new InvalidDataException();
                }
            }

            if (skipObfuscation)
            {
                foreach (PackageFile file in pkg.Files)
                {
                    file.RemoveCustomData<DotfuscatorData>();
                    file.RemoveCustomData<ObfuscarData>();
                }
            }

            string tempDir = Path.GetTempPath() + Path.GetRandomFileName();
            Directory.CreateDirectory(tempDir);

            try
            {
                log.Info("Updating package version.");
                pkg.updateVersion(projectDir,prereleaseOverride);
                log.Info("Package version is {0}", pkg.Version);
                
                UpdateVersionInfo(tempDir, pkg.Files, pkg.Version);

                // License Inject
                // Obfuscate
                // Sign
                CustomPackageActionHelper.RunCustomActions(pkg, PackageActionStage.Create, new CustomPackageActionArgs(tempDir, false));

                log.Info("Creating TAP package.");
                pkg.Compress(path, pkg.Files);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Display("SetAssemblyInfo")]
        public class SetAssemblyInfoData : ICustomPackageData
        {
            [XmlAttribute]
            public string Attributes { get; set; }
        }

        private static void UpdateVersionInfo(string tempDir, List<PackageFile> files, SemanticVersion version)
        {
            var features = files.Where(f => f.HasCustomData<SetAssemblyInfoData>()).SelectMany(f => f.GetCustomData<SetAssemblyInfoData>().Attributes.Split(',').Select(str => str.Trim().ToLower())).Distinct().ToHashSet();

            if (!features.Any())
                return;
            var timer = Stopwatch.StartNew();
            SetAsmInfo.UpdateMethod updateMethod;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                updateMethod = SetAsmInfo.UpdateMethod.ILDasm;
            else
                updateMethod = SetAsmInfo.UpdateMethod.Mono;
            foreach (var file in files)
            {
                if (!file.HasCustomData<SetAssemblyInfoData>())
                    continue;

                var toSet = file.GetCustomData<SetAssemblyInfoData>().Attributes.Split(',').Select(str => str.Trim().ToLower()).Distinct().ToHashSet();

                if (!toSet.Any())
                    continue;

                log.Debug("Updating version info for '{0}'", file.FileName);

                try
                {
                    using (File.OpenWrite(file.FileName))
                    {

                    }
                }
                catch
                {
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
                    file.FileName = tempName;
                }

                SemanticVersion fVersion = null;
                Version fVersionShort = null;

                if (toSet.Contains("version"))
                {
                    fVersion = version;
                    fVersionShort = new Version(version.ToString(3));
                }

                SetAsmInfo.SetAsmInfo.SetInfo(file.FileName, fVersionShort, fVersionShort, fVersion, updateMethod);
                file.RemoveCustomData<SetAssemblyInfoData>();
            }
            log.Info(timer,"Updated assembly version info using {0} method.", updateMethod);
        }

        internal static void PruneInformation(this PackageDef def)
        {
            foreach (var file in def.Files)
            {
                if(!string.IsNullOrWhiteSpace(file.LicenseRequired))
                    file.LicenseRequired = LicenseBase.FormatFriendly(file.LicenseRequired);
            }
        }

        /// <summary>
        /// Compresses the files to a zip package.
        /// </summary>
        /// <param name="outputPath"></param>
        /// <returns>An instance of TapPackage representing the file.</returns>
        static private void Compress(this PackageDef pkg, string outputPath, IEnumerable<PackageFile> inputPaths)
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (String.IsNullOrWhiteSpace(dir) == false)
            {
                Directory.CreateDirectory(dir);
            }
            using (var zip = new System.IO.Compression.ZipArchive(File.Open(outputPath, FileMode.Create), System.IO.Compression.ZipArchiveMode.Create))
            {
                foreach (PackageFile file in inputPaths)
                {
                    var relFileName = file.RelativeDestinationPath.Replace('\\', '/'); // Use forward slash as directory separators
                    var ZipPart = zip.CreateEntry(relFileName, System.IO.Compression.CompressionLevel.Optimal);
                    byte[] B = File.ReadAllBytes(file.FileName);
                    using (var str = ZipPart.Open())
                        str.Write(B, 0, B.Length);
                }

                // add the metadata xml file:

                var metaPart = zip.CreateEntry(String.Join("/", PackageDef.PackageDefDirectory, pkg.Name, PackageDef.PackageDefFileName), System.IO.Compression.CompressionLevel.Optimal);
                using(var str = metaPart.Open())
                    pkg.SaveTo(str);
            }
        }
    }
}
