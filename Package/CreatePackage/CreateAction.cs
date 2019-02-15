//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OpenTap.Cli;
using OpenTap.Package;
using System.Text.RegularExpressions;
using System.Threading;

namespace OpenTap.Package
{
    public enum Obfuscator
    {
        [Display("None")]
        None,
        [Display("Dotfuscator")]
        Dotfuscator,
        [Display("Obfuscar")]
        Obfuscar,
        [Display("Any")]
        Any
    }

    [Display("create", Group: "package", Description: "Creates a package based on an XML description file.")]
    public class PackageCreateAction : PackageAction
    {
        public static string DefaultEnding = "TapPackage";
        public static string DefaultFileName = "Package";

        [UnnamedCommandLineArgument("PackageXmlFile", Required = true)]
        public string PackageXmlFile { get; set; }
        
        [CommandLineArgument("project-directory", Description = "The directory containing the GIT repo.\nUsed to get values for version/branch macros.")]
        public string ProjectDir { get; set; }

        [CommandLineArgument("obfuscator", Description = "Specify which .Net obfuscator to use.")]
        public Obfuscator Obfuscator { get; set; }

        [CommandLineArgument("out", Description = "Path to the output file.", ShortName = "o")]
        public string[] OutputPaths { get; set; }

        [CommandLineArgument("prerelease", Description = "Set type of prerelease", ShortName = "p")]
        public string PreRelease { get; set; }

        public PackageCreateAction()
        {
            Obfuscator = Obfuscator.Dotfuscator;
            ProjectDir = Directory.GetCurrentDirectory();
        }

        public override int Execute(CancellationToken cancellationToken)
        {
            if (PackageXmlFile == null)
                throw new Exception("No packages definition file specified.");

            if (PreRelease != null)
            {
                if (PreRelease.StartsWith(".") || PreRelease.EndsWith(".") || Regex.IsMatch(PreRelease, "[^0-9A-Za-z-.]"))
                    throw new ArgumentException("Pre Release tag must be a series of dot separated identifies that contain only letters, numbers and hyphens '[0-9A-Za-z-]'.");
            }

            if ((OutputPaths == null) || (OutputPaths.Length == 0))
                return Process(null);

            foreach (var OutputPath in OutputPaths)
            {
                var result = Process(OutputPath);

                if (result != 0)
                    return result;
            }
            return 0;
        }

        private int Process(string OutputPath)
        {
            try
            {
                PackageDef pkg = null;
                if (!File.Exists(PackageXmlFile))
                {
                    log.Error("Cannot locate XML file '{0}'", PackageXmlFile);
                    return 4;
                }
                if(!Directory.Exists(ProjectDir))
                {
                    log.Error("Project directory '{0}' does not exist.", ProjectDir);
                    return 4;
                }
                try
                {
                    pkg = PackageDefExt.FromXmlFile(PackageXmlFile, expandVersion: false);
                    
                    // Check if package name has invalid characters or is not a valid path
                    var illegalCharacter = pkg.Name.IndexOfAny(Path.GetInvalidFileNameChars());
                    if (illegalCharacter >= 0)
                    {
                        log.Error("Package name cannot contain invalid file path characters: '{0}'", pkg.Name[illegalCharacter]);
                        return 5;
                    }
                    
                    if (OutputPath == "xml")
                        OutputPath = PackageDef.GetPackageDefinitionInstallPath(pkg);

                    if (Path.GetExtension(OutputPath) == ".xml")
                    {
                        pkg.updateVersion(ProjectDir, PreRelease);
                        if (pkg.Version == null)
                            log.Warning("Package version is empty");
                        else
                            log.Info("Package version is {0}", pkg.Version);

                        pkg.PruneInformation();

                        if(!String.IsNullOrWhiteSpace(Path.GetDirectoryName(OutputPath)))
                            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)); // if PackageDef.PackageDefDirectory does not exist, the line below can fail
                        using (var stream = File.Create(OutputPath))
                            pkg.SaveTo(stream);
                        return 0; // return now, outputting only the transformed xml
                    }
                }
                catch (AggregateException aex)
                {

                    foreach (var ex in aex.InnerExceptions)
                    {
                        if (ex is FileNotFoundException)
                        {
                            log.Error("File not found: '{0}'", ((FileNotFoundException)ex).FileName);
                        }
                        else
                        {
                            log.Error(ex.ToString());
                        }

                    }
                    log.Error("Caught errors while loading package definition.");
                    return 4;
                }
                
                var tmpFile = Path.GetTempFileName();
                
                pkg.CreatePackage(tmpFile, ProjectDir, false, PreRelease, Obfuscator);

                if (String.IsNullOrEmpty(OutputPath))
                    OutputPath = GetRealFilePath(pkg.Name, pkg.Version.ToString(), DefaultEnding);

                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(OutputPath)));

                Utils.FileCopy(tmpFile, OutputPath);
                log.Info("TAP plugin package '{0}' containing '{1}' successfully created.", OutputPath, pkg.Name);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }
            catch (InvalidDataException ex)
            {
                log.Error("Caught invalid data exception: {0}", ex.Message);

                return 3;
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                log.Debug(ex);
                return 4;
            }
            return 0;
        }

        public static string GetRealFilePath(string path, string version, string extension)
        {
            string toInsert;
            if (String.IsNullOrEmpty(version))
            {
                toInsert = "."; //just separate with '.'   
            }
            else
            {
                toInsert = "." + version + "."; // insert version between ext and path
            }

            return Path.GetFileNameWithoutExtension(path) + toInsert + extension;
        }
    }
}
