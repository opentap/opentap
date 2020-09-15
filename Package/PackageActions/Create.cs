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
    /// <summary>
    /// CLI sub command `tap sdk create` that can create a *.TapPackage from a definition in a package.xml file.
    /// </summary>
    [Display("create", Group: "package", Description: "Creates a package based on an XML description file.")]
    public class PackageCreateAction : PackageAction
    {
        internal enum ExitCodes
        {
            // Exit code 1 is used by CliActionExecutor (e.g. for errors parsing command line args)
            GeneralPackageCreateError = 2,
            InvalidPackageDefinition = 3,
            FileSystemError = 4,
            InvalidPackageName = 5,
            PackageDependencyError = 6,
            AssemblyDependencyError = 7,
        }

        /// <summary>
        /// The default file extension for OpenTAP packages.
        /// </summary>
        public static string DefaultEnding = "TapPackage";
        /// <summary>
        /// The default file name for the created OpenTAP package. 
        /// Not used anymore, a default file name is now generated from the package name and version.
        /// </summary>
        public static string DefaultFileName = "Package";

        /// <summary>
        /// Represents an unnamed command line argument which specifies the package.xml file that defines the package that should be generated.
        /// </summary>
        [UnnamedCommandLineArgument("PackageXmlFile", Required = true)]
        public string PackageXmlFile { get; set; }

        
        /// <summary>
        /// Represents the --project-directory command line argument which specifies the directory containing the GIT repo used to get values for version/branch macros.
        /// </summary>
        [CommandLineArgument("project-directory", Description = "The directory containing the GIT repo.\nUsed to get values for version/branch macros.")]
        public string ProjectDir { get; set; }

        /// <summary>
        /// Represents the --install command line argument. When true, this action will also install the created package.
        /// </summary>
        [CommandLineArgument("install", Description = "Installs the created package. Will not overwrite files \nalready in the target installation (e.g. debug binaries).")]
        public bool Install { get; set; } = false;

        /// <summary>
        /// Obsolete, use Install property instead.
        /// </summary>
        [CommandLineArgument("fake-install", Visible = false, Description = "Installs the created package. Will not overwrite files \nalready in the target installation (e.g. debug binaries).")]
        public bool FakeInstall { get; set; } = false;

        /// <summary>
        /// Represents the --out command line argument which specifies the path to the output file.
        /// </summary>
        [CommandLineArgument("out", Description = "Path to the output file.", ShortName = "o")]
        public string[] OutputPaths { get; set; }

        /// <summary>
        /// Constructs new action with default values for arguments.
        /// </summary>
        public PackageCreateAction()
        {
            ProjectDir = Directory.GetCurrentDirectory();
        }

        /// <summary>
        /// Executes this action.
        /// </summary>
        public override int Execute(CancellationToken cancellationToken)
        {
            if (PackageXmlFile == null)
                throw new Exception("No packages definition file specified.");

            return Process(OutputPaths);
        }

        private int Process(string[] OutputPaths)
        {
            try
            {
                PackageDef pkg = null;
                if (!File.Exists(PackageXmlFile))
                {
                    log.Error("Cannot locate XML file '{0}'", PackageXmlFile);
                    return (int)ExitCodes.FileSystemError;
                }
                if (!Directory.Exists(ProjectDir))
                {
                    log.Error("Project directory '{0}' does not exist.", ProjectDir);
                    return (int)ExitCodes.FileSystemError;
                }
                try
                {
                    var fullpath = Path.GetFullPath(PackageXmlFile);
                    pkg = PackageDefExt.FromInputXml(fullpath,ProjectDir);

                    // Check if package name has invalid characters or is not a valid path
                    var illegalCharacter = pkg.Name.IndexOfAny(Path.GetInvalidFileNameChars());
                    if (illegalCharacter >= 0)
                    {
                        log.Error("Package name cannot contain invalid file path characters: '{0}'", pkg.Name[illegalCharacter]);
                        return (int)ExitCodes.InvalidPackageName;
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

                // If user omitted the Version XML attribute or put Version="", lets inform.
                if(string.IsNullOrEmpty(pkg.RawVersion))
                    log.Warning($"Package version is {pkg.Version} due to blank or missing 'Version' XML attribute in 'Package' element");

                pkg.CreatePackage(tmpFile);

                if (OutputPaths == null || OutputPaths.Length == 0)
                    OutputPaths = new string[1] { "" };

                foreach (var outputPath in OutputPaths)
                {
                    var path = outputPath;

                    if (String.IsNullOrEmpty(path))
                        path = GetRealFilePathFromName(pkg.Name, pkg.Version.ToString(), DefaultEnding);

                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));

                    ProgramHelper.FileCopy(tmpFile, path);
                    log.Info("OpenTAP plugin package '{0}' containing '{1}' successfully created.", path, pkg.Name);
                }

                if (FakeInstall)
                {
                    log.Warning("--fake-install argument is obsolete, use --install instead");
                    Install = FakeInstall;
                }
                if (Install)
                {
                    var path = PackageDef.GetDefaultPackageMetadataPath(pkg, Directory.GetCurrentDirectory());
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate))
                    {
                        pkg.SaveTo(fs);
                    }
                    log.Info($"Installed '{pkg.Name}' ({Path.GetFullPath(path)})");
                }
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return (int)ExitCodes.GeneralPackageCreateError;
            }
            catch (InvalidDataException ex)
            {
                log.Error("Caught invalid data exception: {0}", ex.Message);

                return (int)ExitCodes.InvalidPackageDefinition;
            }
            return 0;
        }

        /// <summary>
        /// Obsolete. Do not use.
        /// </summary>
        [Obsolete("Will be removed in OpenTAP 10.")]
        public static string GetRealFilePath(string path, string version, string extension)
        {
            return GetRealFilePathFromName(Path.GetFileNameWithoutExtension(path), version, extension);
        }

        internal static string GetRealFilePathFromName(string name, string version, string extension)
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

            return name + toInsert + extension;
        }
    }
}
