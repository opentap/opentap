//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace OpenTap.Package
{
    [XmlType("Plugin")]
    public class PluginFile
    {
        [XmlAttribute]
        public string Type { get; set; }
        [XmlAttribute]
        public string BaseType { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }
        public double Order { get; set; }
        public bool Browsable { get; set; }
        public string Description { get; set; }
        public bool Collapsed { get; set; }
        public string[] Groups { get; set; }

        public PluginFile()
        {
            Browsable = true;
        }

        public bool ShouldSerializeBrowsable()
        {
            return !Browsable;
        }
    }

    /// <summary>
    /// Information about a file in a package. 
    /// </summary>
    [XmlType("File")]
    [DebuggerDisplay("{FileName} ({RelativeDestinationPath})")]
    public class PackageFile
    {
        private string _FileName = null;
        /// <summary>
        /// The filename of this file.
        /// </summary>
        [XmlIgnore]
        public string FileName
        {
            get
            {
                if (_FileName == null)
                    // When this type is deserialized from an xml file, FileName will 
                    // be unset, so we use the value from RelativeDestinationPath
                    return RelativeDestinationPath;
                else
                    return _FileName;
            }
            set { _FileName = value; }
        }

        [XmlAttribute("SourcePath")]
        [DefaultValue(null)]
        public string SourcePath
        {
            get
            {
                return null;
            }
            set
            {
                if (value != null)
                    FileName = value;
            }
        }

        /// <summary>
        /// Relative location of file ( to OpenTAP folder).
        /// </summary>
        [XmlAttribute("Path")]
        public string RelativeDestinationPath { get; set; }

        /// <summary>
        /// The contained plugin types.
        /// </summary>
        [DefaultValue(null)]
        public List<PluginFile> Plugins { get; set; }

        /// <summary>
        /// Dependencies to ignore. This should contain a list of assembly names.
        /// </summary>
        [XmlElement(ElementName = "IgnoreDependency")]
        public List<string> IgnoredDependencies { get; set; }

        /// <summary>
        /// Custom data meant for consumption by <see cref="ICustomPackageAction"/> plugins.
        /// </summary>
        public List<ICustomPackageData> CustomData { get; set; }

        /// <summary>
        /// Dependent assemblies.
        /// </summary>
        [XmlIgnore]
        internal List<AssemblyData> DependentAssemblyNames { get; set; }

        /// <summary>
        /// License required by the plugin file.
        /// </summary>
        [XmlAttribute("LicenseRequired")]
        [DefaultValue(null)]
        public string LicenseRequired { get; set; }

        /// <summary>
        /// Creates a new instance of PackageFile.
        /// </summary>
        public PackageFile()
        {
            DependentAssemblyNames = new List<AssemblyData>();
            Plugins = new List<PluginFile>();
            IgnoredDependencies = new List<string>();
            CustomData = new List<ICustomPackageData>();
        }
    }

    /// <summary>
    /// Represents a dependency on a package.
    /// </summary>
    [DebuggerDisplay("{Name} ({Version})")]
    public class PackageDependency
    {
        /// <summary>
        /// Name of the package to which this dependency reffers.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Specifying requirements to the version of the package. Never null.
        /// </summary>
        public VersionSpecifier Version { get; private set; }

        /// <summary>
        /// This constructor is only used for serialization.
        /// </summary>
        public PackageDependency(string name, VersionSpecifier version)
        {
            if(version == null)
                throw new ArgumentNullException("version");
            Name = name;
            Version = version;
        }
    }

    public class ActionStep
    {
        [XmlAttribute("ExeFile")]
        public string ExeFile { get; set; }

        [XmlAttribute("Arguments")]
        public string Arguments { get; set; }

        [XmlAttribute("ActionName")]
        public string ActionName { get; set; }

        [XmlAttribute("UseShellExecute")]
        [DefaultValue(false)]
        public bool UseShellExecute { get; set; }

        [XmlAttribute("CreateNoWindow")]
        [DefaultValue(false)]
        public bool CreateNoWindow { get; set; }

        public ActionStep()
        {
            UseShellExecute = false;
            CreateNoWindow = false;
        }
    }

    public enum CpuArchitecture
    {
        Unspecified,

        AnyCPU,

        x86,
        x64,
        arm,
        arm64
    }

    /// <summary>
    /// Definition of a package file. Contains basic structural information relating to packages.
    /// </summary>
    [DebuggerDisplay("{Name} ({Version})")]
    public class PackageDef : PackageIdentifier
    {
        /// <summary>
        /// A description of this package.
        /// </summary>
        [DefaultValue(null)]
        public string Description { get; set; }

        /// <summary>
        /// A list of other packages that this package depends on.
        /// </summary>
        public List<PackageDependency> Dependencies { get; set; } = new List<PackageDependency>();

        /// <summary>
        /// If this package originates from a package repository. This is the URL of that repository. Otherwise null
        /// </summary>
        [XmlElement("PackageRepositoryUrl")] // TODO: This is only for testing, the repo server needs to be updated to also include the 'Location' element in the xml.
        [DefaultValue(null)]
        public string Location { get; set; }
        
        /// <summary>
        /// A link to get more information.
        /// </summary>
        [XmlAttribute]
        [DefaultValue(null)]
        public string InfoLink { get; set; }

        /// <summary>
        /// The date that the package was build. Defaults to DateTime.MinValue if no date is specified in package.xml
        /// </summary>
        [XmlAttribute]
        public DateTime Date { get; set; }

        /// <summary>
        /// The file type of this package. Either 'application' or 'tappackage'. Default is 'tappackage'.
        /// </summary>
        [XmlAttribute]
        [DefaultValue("tappackage")]
        public string FileType { get; set; }

        /// <summary>
        /// The package class, this can be either 'package', 'bundle' or 'solution'.
        /// </summary>
        [XmlAttribute]
        [DefaultValue("package")]
        public string Class { get; set; }

        internal bool IsBundle()
        {
            return Class.ToLower() == "bundle" || Class.ToLower() == "solution";
        }

        internal bool IsSystemWide()
        {
            return Class.ToLower() == "system-wide";
        }

        /// <summary>
        /// Returns version as a <see cref="SemanticVersion"/>.
        /// </summary>
        /// <returns></returns>
        internal string RawVersion
        {
            get;
            set;
        }
        
        /// <summary>
        /// A list of files contained in this package.
        /// </summary>
        public List<PackageFile> Files { get; set; }

        /// <summary>
        /// Contains steps that can be executed for this plugin during, or after installation.
        /// </summary>
        public List<ActionStep> PackageActionExtensions { get; set; }

        /// <summary>
        /// Creates a new packagedef.
        /// </summary>
        internal PackageDef()
        {
            Files = new List<PackageFile>();
            PackageActionExtensions = new List<ActionStep>();
            OS = "Windows";
            Architecture = CpuArchitecture.AnyCPU;
            
            if (string.IsNullOrWhiteSpace(FileType))
                FileType = "tappackage";
            if (string.IsNullOrWhiteSpace(Class))
                Class = "package";
        }

        public override string ToString()
        {
            return String.Format("{0}|{1}", Name, Version);
        }
        
        /// <summary>
        /// Loads package definition from a file.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static PackageDef FromXml(Stream stream)
        {
            stream = ConvertXml(stream);

            var serializer = new TapSerializer();
            return (PackageDef)serializer.Deserialize(stream, type: TypeData.FromType(typeof(PackageDef)));
        }

        static Stream ConvertXml(Stream stream)
        {
            var root = XElement.Load(stream);

            var xns = root.GetDefaultNamespace();
            var filesElement = root.Element(xns + "Files");
            if (filesElement != null)
            {
                var fileElements = filesElement.Elements(xns + "File");
                foreach (var file in fileElements)
                {
                    var plugins = file.Element(xns + "Plugins");
                    if (plugins == null) continue;

                    var pluginElements = plugins.Elements(xns + "Plugin");
                    foreach (var plugin in pluginElements)
                    {
                        if (!plugin.HasElements && !plugin.IsEmpty)
                        {
                            plugin.SetAttributeValue("Type", plugin.Value);
                            var value = plugin.Value;
                            plugin.Value = "";
                        }
                    }
                }
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(root.ToString()));
        }
        
        /// <summary>
        /// Writes this package definition to a file.
        /// </summary>
        /// <param name="stream"></param>
        public void SaveTo(Stream stream)
        {
            new TapSerializer().Serialize(stream, this);
        }

        /// <summary>
        /// Writes this package definition to a file.
        /// </summary>
        public static void SaveManyTo(Stream stream, IEnumerable<PackageDef> packages)
        {
            XDocument xdoc = new XDocument();
            var root = new XElement("ArrayOfPackages");
            xdoc.Add(root);
            foreach (PackageDef package in packages)
            {
                using (Stream str = new MemoryStream())
                {
                    try
                    {
                        package.SaveTo(str);
                        str.Seek(0, 0);
                        var pkgElement = XElement.Load(str);
                        root.Add(pkgElement);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }
                }
            }
            xdoc.Save(stream);
        }

        public static IEnumerable<PackageDef> ManyFromXml(Stream stream)
        {
            var root = XElement.Load(stream);
            List<PackageDef> packages = new List<PackageDef>();
            foreach (XNode node in root.Nodes())
            {
                using (Stream str = new MemoryStream())
                {
                    if (node is XElement)
                    {
                        (node as XElement).Save(str);
                        str.Seek(0, 0);
                        packages.Add(PackageDef.FromXml(str));
                    }
                    else
                    {
                        throw new XmlException("Invalid XML");
                    }
                }
            }
            return packages;
        }

        /// <summary>
        /// Constructs a PackageDef object to represent a TapPackage package that has already been created.
        /// </summary>
        /// <param name="path">Path to a *.TapPackage file</param>
        public static PackageDef FromPackage(string path)
        {
            string metaFilePath = GetMetadataFromPackage(path);

            PackageDef pkgDef;
            using (Stream metaFileStream = new MemoryStream(1000))
            {
                if (!PluginInstaller.UnpackageFile(path, metaFilePath, metaFileStream))
                    throw new Exception("Failed to extract package metadata from package.");
                metaFileStream.Seek(0, SeekOrigin.Begin);
                pkgDef = PackageDef.FromXml(metaFileStream);
            }
            //pkgDef.updateVersion();
            pkgDef.Location = Path.GetFullPath(path);
            return pkgDef;
        }


        public static List<PackageDef> FromPackages(string path)
        {
            var packageList = new List<PackageDef>();

            if (Path.GetExtension(path).ToLower() != ".tappackages")
            {
                packageList.Add(FromPackage(path));
                return packageList;
            }

            using (var zip = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read))
            {
                foreach (var part in zip.Entries)
                {
                    FileSystemHelper.EnsureDirectory(part.FullName);
                    var deflate_stream = part.Open();
                    using (var fileStream = File.Create(part.FullName))
                    {
                        deflate_stream.CopyTo(fileStream);
                    }
                    
                    var package = FromPackage(part.FullName);
                    packageList.Add(package);

                    if (File.Exists(part.FullName))
                        File.Delete(part.FullName);
                }
            }

            return packageList;
        }

        /// <summary>
        /// Throws InvalidDataException if the xml in the file does not conform to the schema.
        /// </summary>
        public static void ValidateXml(string path)
        {
            ValidateXmlDefinitionFile(path, false);
        }

        public static PackageDef FromXml(string path)
        {
            using (var stream = File.OpenRead(path))
                return PackageDef.FromXml(stream);
        }

        public static XmlSchemaSet GetXmlSchema()
        {
            // Get the schema from the embedded resource:
            var assembly = typeof(OpenTap.Package.Installer).Assembly;
            var resourceName = "OpenTap.Package.PackageSchema.xsd";
            XmlSchema schema;
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                schema = XmlSchema.Read(stream, null);
            }
            XmlSchemaSet schemas = new XmlSchemaSet();
            schemas.Add(schema);
            return schemas;
        }

        static TraceSource log =  OpenTap.Log.CreateSource("Package");

        /// <summary>
        /// Used by ValidateXmlDefinitionFile to write errors to the console formatted so that the PackageTask can parse them.
        /// </summary>
        private static void PrintError(string message, int lineNumber, int linePosition, string path)
        {
            log.Error("{0}({1},{2}): error: {3}", path, lineNumber, linePosition, message);
        }

        /// <summary>
        /// Throws InvalidDataException if the xml in the file does not conform to the schema. Also prints an error to stderr
        /// </summary>
        private static void ValidateXmlDefinitionFile(string xmlFilePath, bool verbose = true)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.Schemas = GetXmlSchema();
            settings.ValidationType = ValidationType.Schema;
            List<XmlSchemaException> errors = new List<XmlSchemaException>();
            settings.ValidationEventHandler += (sender, e) =>
            {
                throw new InvalidDataException("Line " + e.Exception.LineNumber + ": " + e.Exception.Message);
            };
            XmlReader reader = XmlReader.Create(xmlFilePath, settings);

            try
            {
                XDocument.Load(reader);
            }
            catch (Exception ex)
            {
                if (verbose)
                {
                    if (ex is XmlException)
                    {
                        XmlException xex = (XmlException)ex;
                        PrintError(ex.Message, xex.LineNumber, xex.LinePosition, xmlFilePath);
                    }
                    else
                        PrintError(ex.Message, 0, 0, xmlFilePath);
                }
                throw new InvalidDataException(ex.Message);
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        /// Relative path to the directory holding OpenTAP Package definition files
        /// </summary>
        public const string PackageDefDirectory = "Packages";
        /// <summary>
        /// Absolute path to the directory representing the OpenTAP installation dir for system-wide packages
        /// </summary>
        public static string SystemWideInstallationDirectory { get => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Keysight", "Test Automation"); }
        public const string PackageDefFileName = "package.xml";

        internal static string GetDefaultPackageMetadataPath(PackageDef pkg, string target)
        {
            string installationRootDir = target;
            if (pkg.IsSystemWide())
                installationRootDir = PackageDef.SystemWideInstallationDirectory;
            return GetDefaultPackageMetadataPath(pkg.Name, installationRootDir);
        }

        internal static string GetDefaultPackageMetadataPath(string name, string installationDir = null)
        {
            if (installationDir == null)
                installationDir = FileSystemHelper.GetCurrentInstallationDirectory();

            return String.Join("/", installationDir, PackageDef.PackageDefDirectory, name, PackageDef.PackageDefFileName); // don't use Path.Combine, as that might create \ which makes the package unable to install on linux
        }

        internal static List<string> GetPackageMetadataFilesInTapInstallation(string tapPath)
        {
            List<string> metadatas = new List<string>();

            // this way of seaching for package.xml files will find them both in their 8.x 
            // location (Package Definitions/<PkgName>.package.xml) and in their new 9.x
            // location (Packages/<PkgName>/package.xml)

            // Add 9.x packages in "Packages" folder
            string packageDir = Path.GetFullPath(Path.Combine(tapPath, PackageDefDirectory));

            Directory.CreateDirectory(packageDir);
            foreach (var dir in Directory.EnumerateDirectories(packageDir).Select(s => new DirectoryInfo(s).Name))
            {
                if (File.Exists(GetDefaultPackageMetadataPath(dir, tapPath)))
                    metadatas.Add(GetDefaultPackageMetadataPath(dir, tapPath));
            }

            // Add backwards compatibility by adding packages in "Package Definitions" folder
            var packageDir8x = Path.GetFullPath(Path.Combine(tapPath, "Package Definitions"));
            if (Directory.Exists(packageDir8x))
                metadatas.AddRange(Directory.GetFiles(packageDir8x, "*.package.xml"));

            return metadatas;
        }

        internal static List<string> GetSystemWidePackages()
        {
            List<string> metadatas = new List<string>();

            // Add 9.x packages in "Packages" folder
            var systemWidePackageDir = Path.Combine(PackageDef.SystemWideInstallationDirectory, PackageDef.PackageDefDirectory);
            if (Directory.Exists(systemWidePackageDir))
                metadatas.AddRange(Directory.GetFiles(systemWidePackageDir, "*" + PackageDef.PackageDefFileName, SearchOption.AllDirectories));

            // Add backwards compatibility by adding packages in "Package Definitions" folder
            var systemWidePackageDir8x = Path.Combine(PackageDef.SystemWideInstallationDirectory, "Package Definitions");
            if (Directory.Exists(systemWidePackageDir8x))
                metadatas.AddRange(Directory.GetFiles(systemWidePackageDir8x, "*.package.xml"));

            return metadatas;
        }

        internal static string GetMetadataFromPackage(string path)
        {
            var filesInPackage = PluginInstaller.FilesInPackage(path);
            string metaFilePath = PluginInstaller.FilesInPackage(path)
                .Where(p => p.Contains(PackageDef.PackageDefDirectory) && p.EndsWith(PackageDef.PackageDefFileName))
                .OrderBy(p => p.Length).FirstOrDefault(); // Find the xml file in the most top level
            if (String.IsNullOrEmpty(metaFilePath))
            {
                // for TAP 8.x support, we could remove when 9.0 is final, and packages have been migrated.
                metaFilePath = PluginInstaller.FilesInPackage(path).FirstOrDefault(p => (p.Contains("package/") || p.Contains("Package Definitions/")) && p.EndsWith("package.xml"));
                if (String.IsNullOrEmpty(metaFilePath))
                    throw new IOException("No metadata found in package " + path);
            }

            return metaFilePath;
        }
    }

    // helper class to ignore namespaces when de-serializing
    internal class NamespaceIgnorantXmlTextReader : XmlTextReader
    {
        public NamespaceIgnorantXmlTextReader(System.IO.Stream stream) : base(stream) { this.Namespaces = false; }

        public override string NamespaceURI
        {
            get { return ""; }
        }
    }

    /// <summary>
    /// Helper methods used to determine CpuArchitecture and compatibility between them.
    /// </summary>
    public class ArchitectureHelper
    {
        private static CpuArchitecture hostPlatform = CpuArchitecture.Unspecified;

        static ArchitectureHelper()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // This is a workaround for a bug in RuntimeInformation that calls a non-existing method when run on the .NET4.6.2 Framework.

                var def_value = Environment.Is64BitOperatingSystem ? "AMD64" : "x86";
                var str = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE", EnvironmentVariableTarget.Machine) ?? def_value;

                switch (str.ToLower())
                {
                    case "arm": hostPlatform = CpuArchitecture.arm; break;
                    case "arm64": hostPlatform = CpuArchitecture.arm64; break;
                    case "amd64": hostPlatform = CpuArchitecture.x64; break;
                    case "x86": hostPlatform = CpuArchitecture.x86; break;
                }
            }
            else
                switch (RuntimeInformation.OSArchitecture)
                {
                    case Architecture.Arm: hostPlatform = CpuArchitecture.arm; break;
                    case Architecture.Arm64: hostPlatform = CpuArchitecture.arm64; break;
                    case Architecture.X64: hostPlatform = CpuArchitecture.x64; break;
                    case Architecture.X86: hostPlatform = CpuArchitecture.x86; break;
                }
        }

        /// <summary>
        /// Returns the CPU architecture of the host OS.
        /// </summary>
        public static CpuArchitecture HostArchitecture { get { return hostPlatform; } }

        /// <summary>
        /// Returns the architecture that the OpenTAP was compiled for, or the best guess it can give based on the current host architecture and process state.
        /// </summary>
        public static CpuArchitecture GuessBaseArchitecture
        {
            get
            {
                // Try to find the architecture of the base install
                var currentArchitecture = Environment.Is64BitProcess ? CpuArchitecture.x64 : CpuArchitecture.x86; // Assume we are on x86/x86_64

                // If we aren't on x86 then just use the host architecture since they are not compatible.
                if ((HostArchitecture == CpuArchitecture.arm) || (HostArchitecture == CpuArchitecture.arm64)) currentArchitecture = HostArchitecture;

                // And finally try to use the actual information in the package xml.
                var installDir = Path.GetDirectoryName(typeof(PluginManager).Assembly.Location);
                if(File.Exists(PackageDef.GetDefaultPackageMetadataPath("OpenTap", installDir))){
                    currentArchitecture = PackageDef.FromXml(PackageDef.GetDefaultPackageMetadataPath("OpenTap", installDir)).Architecture;
                }

                return currentArchitecture;
            }
        }

        /// <summary>
        /// Returns true if a host OS can support a plugin with a given CPU architecture.
        /// </summary>
        /// <param name="host">The architecture of the host.</param>
        /// <param name="plugin">The architecture of the plugin.</param>
        /// <returns></returns>
        public static bool CompatibleWith(CpuArchitecture host, CpuArchitecture plugin)
        {
            if (plugin == CpuArchitecture.AnyCPU || (host == CpuArchitecture.Unspecified)) return true; // TODO: Figure out if this should be allowed in the long term
            if (plugin == CpuArchitecture.AnyCPU) return true;

            //if ((host == CpuArchitecture.x64) && (plugin == CpuArchitecture.x86)) return true;

            return (host == plugin);
        }

        /// <summary>
        /// Returns true if the architectures of two plugins are compatible.
        /// </summary>
        /// <param name="plugin1">The architecture of one of the plugins.</param>
        /// <param name="plugin2">The architecture of the other plugin.</param>
        /// <returns>True, if those two plugins can be used together.</returns>
        public static bool PluginsCompatible(CpuArchitecture plugin1, CpuArchitecture plugin2)
        {
            if (plugin1 == CpuArchitecture.AnyCPU) return true;
            if (plugin2 == CpuArchitecture.AnyCPU) return true;

            return plugin1 == plugin2;
        }
    }
}
