//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using OpenTap.Cli;
using System.Threading;
using Tap.Shared;

namespace OpenTap.Package
{
    /// <summary>
    /// CLI sub command `tap sdk create` that can create a *.TapPackage from a definition in a package.xml file.
    /// </summary>
    [Display("create", Group: "package", Description: "Create a package based on an XML description file.")]
    public class PackageCreateAction : PackageAction
    {
      
        private static readonly char[] IllegalPackageNameChars = {'"', '<', '>', '|', '\0', '\u0001', '\u0002', '\u0003', '\u0004', '\u0005', '\u0006', '\a', '\b', 
            '\t', '\n', '\v', '\f', '\r', '\u000e', '\u000f', '\u0010', '\u0011', '\u0012', '\u0013', '\u0014', '\u0015', '\u0016', '\u0017', '\u0018', 
            '\u0019', '\u001a', '\u001b', '\u001c', '\u001d', '\u001e', '\u001f', ':', '*', '?', '\\'};
        
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
        /// Represents the --project-directory command line argument, which specifies the directory containing the git repository used to get values for version/branch macros.
        /// </summary>
        [CommandLineArgument("project-directory", Description = "The directory containing the git repository.\nUsed to get values for version/branch macros.")]
        public string ProjectDir { get; set; }

        /// <summary>
        /// Represents the --install command line argument. When true, this action will also install the created package.
        /// </summary>
        [CommandLineArgument("install", Description = "Install the created package. It will not overwrite the files \nalready in the target installation (e.g., debug binaries).")]
        public bool Install { get; set; } = false;

        /// <summary>
        /// Obsolete, use Install property instead.
        /// </summary>
        [CommandLineArgument("fake-install", Description = "Install the created package. It will not overwrite files \nalready in the target installation (e.g. debug binaries).")]
        [Browsable(false)]
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

            return Process(OutputPaths, cancellationToken);
        }

        private int Process(string[] OutputPaths, CancellationToken cancellationToken)
        {
            try
            {
                PackageDef pkg = null;
                if (!File.Exists(PackageXmlFile))
                {
                    log.Error("Cannot locate XML file '{0}'", PackageXmlFile);
                    return (int)ExitCodes.ArgumentError;
                }
                if (!Directory.Exists(ProjectDir))
                {
                    log.Error("Project directory '{0}' does not exist.", ProjectDir);
                    return (int)ExitCodes.ArgumentError;
                }
                try
                {
                    var fullpath = Path.GetFullPath(PackageXmlFile);
                    pkg = PackageDefExt.FromInputXml(fullpath, ProjectDir);

                    // Check if package name has invalid characters or is not a valid path
                    var illegalCharacter = pkg.Name.IndexOfAny(IllegalPackageNameChars);
                    if (illegalCharacter >= 0)
                    {
                        log.Error("Package name cannot contain invalid file path characters: '{0}'.", pkg.Name[illegalCharacter]);
                        return (int)PackageExitCodes.InvalidPackageName;
                    }
                    
                    // Check for invalid package metadata
                    const string validMetadataPattern = "^[_a-zA-Z][_a-zA-Z0-9]*";
                    var validMetadataRegex = new Regex(validMetadataPattern);
                    foreach (var metaDataKey in pkg.MetaData.Keys)
                    {
                        var match = validMetadataRegex.Match(metaDataKey);
                        if (match.Success == false || match.Length != metaDataKey.Length)
                        {
                            if (metaDataKey.Length > 0)
                                log.Error($"Found invalid character '{metaDataKey[match.Length]}' in package metadata key '{metaDataKey}' at position {match.Length + 1}.");
                            else
                                log.Error($"Metadata key cannot be empty.");
                            return (int)PackageExitCodes.InvalidPackageDefinition;
                        }
                    }
                }
                catch (AggregateException aex)
                {
                    foreach (var inner in aex.InnerExceptions)
                    {
                        if (inner is FileNotFoundException ex)
                        {
                            log.Error("File not found: '{0}'", ex.FileName);
                        }
                        else
                        {
                            log.Error(inner.ToString());
                        }
                    }
                    log.Error("Caught errors while loading package definition.");
                    return (int)PackageExitCodes.InvalidPackageDefinition;
                }

                var tmpFile = PathUtils.GetTempFileName(".opentap_package_tmp.zip"); ;

                // If user omitted the Version XML attribute or put Version="", lets inform.
                if(string.IsNullOrEmpty(pkg.RawVersion))
                    log.Warning($"Package version is {pkg.Version} due to blank or missing 'Version' XML attribute in 'Package' element");

                using (var str = new FileStream(tmpFile, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose))
                {
                    pkg.CreatePackage(str);
                    if (OutputPaths == null || OutputPaths.Length == 0)
                        OutputPaths = new string[1] {""};

                    foreach (var outputPath in OutputPaths)
                    {
                        var path = outputPath;

                        if (String.IsNullOrEmpty(path) || path.EndsWith(Path.DirectorySeparatorChar.ToString()) || path.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
                        {
                            // Package names support path separators now -- avoid writing the newly created package into a nested folder and
                            // replace the path separators with dots instead
                            var name = pkg.Name.Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.');
                            path = Path.Combine(path, GetRealFilePathFromName(name, pkg.Version.ToString(), DefaultEnding));
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                        ProgramHelper.FileCopy(tmpFile, path);
                        log.Info("OpenTAP plugin package '{0}' containing '{1}' successfully created.", path, pkg.Name);
                    }
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
                log.Error("Caught exception: {0}", ex.Message);
                return (int)PackageExitCodes.PackageCreateError;
            }
            catch (InvalidDataException ex)
            {
                log.Error("Caught invalid data exception: {0}", ex.Message);

                return (int)PackageExitCodes.InvalidPackageDefinition;
            }
            return (int)ExitCodes.Success;
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
