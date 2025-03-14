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
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace OpenTap.Package
{
    /// <summary>
    /// Represents a plugin (type that derives from ITapPlugin) in a payload file of an OpenTAP package.
    /// </summary>
    [XmlType("Plugin")]
    public class PluginFile
    {
        /// <summary>
        /// The namespace qualified name of the type.
        /// </summary>
        [XmlAttribute]
        public string Type { get; set; }
        
        /// <summary>
        /// The display name of the plugin base type that Type derives from. E.g. TestStep.
        /// </summary>
        [XmlAttribute]
        public string BaseType { get; set; }
        /// <summary> The display name of the plugin type as specified by its <see cref="DisplayAttribute"/>.</summary>
        public string Name { get; set; }
        /// <summary> Obsolete. Always null. Use Groups instead. </summary>
        [XmlIgnore]
        [Obsolete]
        public string Group { get; set; }
        /// <summary> The display order of the plugin type as specified by its <see cref="DisplayAttribute"/>.</summary>
        public double Order { get; set; }
        /// <summary> The browsable state of the plugin type as specified by a System.ComponentModel.BrowsableAttribute.</summary>
        public bool Browsable { get; set; }
        /// <summary> The description of the plugin type as specified by its <see cref="DisplayAttribute"/>.</summary>
        public string Description { get; set; }
        /// <summary> The collapsed state of the display group to which the plugin belongs as specified by its <see cref="DisplayAttribute"/>.</summary>
        public bool Collapsed { get; set; }
        /// <summary> The array of display groups of the plugin type as specified by its <see cref="DisplayAttribute"/>.</summary>
        public string[] Groups { get; set; }

        /// <summary>
        /// Creates a new PluginFile.
        /// </summary>
        public PluginFile()
        {
            Browsable = true;
        }

        /// <summary>
        /// Obsolete. Use !Browsable instead.
        /// </summary>
        [Obsolete]
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
        /// <summary>
        /// The location of this file.
        /// </summary>
        [XmlIgnore]
        public string FileName
        {
            set => sourcePath = value;
            // When this type is deserialized from an xml file, FileName will 
            // be unset, so we use the value from RelativeDestinationPath
            get => sourcePath ??  RelativeDestinationPath;
        }

    
        string sourcePath;

        /// <summary> Source of the file. Can be different from RelativeDestinationPath. </summary>
        [XmlAttribute("SourcePath")]
        [DefaultValue(null)]
        [XmlIgnore]
        public string SourcePath
        {
            get => sourcePath;
            set => sourcePath = value;
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
        internal List<AssemblyData> DependentAssemblies { get; set; }
        
        /// <summary> Other type data dependency sources (e.g Python type data) </summary>
        [XmlIgnore]
        internal List<ITypeDataSource> DependentTypeDataSources { get; set; }

        internal IEnumerable<ITypeDataSource> AllDependencies => DependentAssemblies.Concat(DependentTypeDataSources);

        /// <summary>
        /// License required by the plugin file.
        /// </summary>
        [XmlAttribute("LicenseRequired")]
        [DefaultValue("")]
        public string LicenseRequired { get; set; } = "";

        /// <summary>
        /// Creates a new instance of PackageFile.
        /// </summary>
        public PackageFile()
        {
            DependentAssemblies = new List<AssemblyData>();
            DependentTypeDataSources = new List<ITypeDataSource>();
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
        public VersionSpecifier Version { get; set; }

        /// <summary>
        /// Returns raw version string.
        /// </summary>
        /// <returns>Raw version string from input. Null if the raw input is not set</returns>
        internal string RawVersion
        {
            get;
            private set;
        }

        /// <summary>
        /// This constructor is only used for serialization.
        /// </summary>
        public PackageDependency(string name, VersionSpecifier version, string rawVersion = null)
        {
            if(version == null)
                throw new ArgumentNullException("version");
            Name = name;
            Version = version;
            RawVersion = rawVersion;
        }

        /// <summary>
        /// Compares this PackageDependency to another object.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is PackageDependency dep)
                return Equals(dep.Name, Name) && Equals(dep.Version, Version);
            return false;
        }

        /// <summary>
        /// Returns the hash code for this PackageDependency.
        /// </summary>
        public override int GetHashCode() =>  (Name ?? "").GetHashCode() * 7489019 + (RawVersion ?? "").GetHashCode() * 41077013;
    }

    /// <summary>
    /// Represents an external command that can be executed as a CLI action
    /// </summary>
    public class ExternalCliAction
    {
      /// <summary>
      /// The name of the CLI action, similar to DisplayAttribute
      /// </summary>
      [XmlAttribute("Name")]
      public string Name { get; set; }

      /// <summary>
      /// The description of the CLI actiom, similar to DisplayAttribute 
      /// </summary>
      [XmlAttribute("Description")]
      public string Description { get; set; }

      /// <summary>
      /// The Groups that the CLI action belongs to, similar to DisplayAttribute 
      /// </summary>
      [XmlAttribute("Groups")]
      public string Groups { get; set; }

      /// <summary>
      /// The binary executable that should be executed
      /// </summary>
      [XmlAttribute("ExeFile")]
      public string ExeFile { get; set; }

      /// <summary>
      /// Any extra arguments that should be applied to the invocation. Arugments supplied by 
      /// the user on the CLI are appended to this value
      /// </summary>
      [XmlAttribute("Arguments")]
      public string Arguments { get; set; }
    }

    /// <summary>
    /// Represents an action/step that can be executed during or after installation of a package.
    /// </summary>
    public class ActionStep
    {
        /// <summary>
        /// Path to an exe file to execute as part of this step.
        /// </summary>
        /// <value></value>
        [XmlAttribute("ExeFile")]
        public string ExeFile { get; set; }

        /// <summary>
        /// A comma separated list of expected exit code integers. Default is "0".
        /// </summary>
        [XmlAttribute(nameof(ExpectedExitCodes))]
        public string ExpectedExitCodes { get; set; } = "0";

        /// <summary>
        /// False; Action stdout and stderr will be forwarded
        /// True; Action stdout and stderr will be suppressed
        /// </summary>
        [XmlAttribute("Quiet")]
        public bool Quiet { get; set; } = false;

        /// <summary>
        /// False; package installation should fail if the executable does not exist.
        /// True; package installation should continue if the executable does not exist.
        /// </summary>
        [XmlAttribute("Optional")]
        public bool Optional { get; set; } = false;

        /// <summary>
        /// Arguments to the exe file.
        /// </summary>
        [XmlAttribute("Arguments")]
        public string Arguments { get; set; }

        /// <summary>
        /// Name of the action in which this step should be executed. E.g. "install".
        /// </summary>
        /// <value></value>
        [XmlAttribute("ActionName")]
        public string ActionName { get; set; }

        /// <summary>
        /// Indicates whether to use the operating system shell to start the process.
        /// </summary>
        [XmlAttribute("UseShellExecute")]
        [DefaultValue(false)]
        public bool UseShellExecute { get; set; }

        /// <summary>
        /// Indicates whether to start the process in a new window.
        /// </summary>
        /// <value></value>
        [XmlAttribute("CreateNoWindow")]
        [DefaultValue(false)]
        public bool CreateNoWindow { get; set; }

        /// <summary>
        /// Creates a new ActionStep with default values.
        /// </summary>
        public ActionStep()
        {
            UseShellExecute = false;
            CreateNoWindow = false;
        }
    }

    /// <summary>
    /// CPU architectures that a package can support.
    /// </summary>
    public enum CpuArchitecture
    {
        /// <summary> Unspecified processor architecture.</summary>
        Unspecified,
        /// <summary> Any processor architecture. </summary>
        AnyCPU,
        /// <summary> An Intel-based 32-bit processor architecture. </summary>
        x86,
        /// <summary> An Intel-based 64-bit processor architecture. </summary>
        x64,
        /// <summary> A 32-bit ARM processor architecture. </summary>
        arm,
        /// <summary> A 64-bit ARM processor architecture. </summary>
        arm64
    }

    /// <summary>
    /// Definition of a package file. Contains basic structural information relating to packages.
    /// </summary>
    [DebuggerDisplay("{Name} ({Version.ToString()})")]
    public class PackageDef : PackageIdentifier
    {
        /// <summary>
        /// Holds additional metadata for a package
        /// </summary>
        public Dictionary<string, string> MetaData { get; } = new Dictionary<string, string>();

        string loadedHash;
        bool hashVerified;
        const int oldHashLength = 40;
        /// <summary>
        /// The hash of the package. This is based on hashes of each payload file as well as metadata in the package definition.
        /// </summary>
        [DefaultValue(null)]
        public string Hash
        {
            get
            {
                // in OpenTAP 9.18 and earlier weak / invalid hash values were calculated.
                // in 9.19, its fixed, but to distinguish a different length of hashes are used.
                // the previous hash length was always 40.
                
                if (!hashVerified && loadedHash != null)
                {
                    hashVerified = true;
                    if (loadedHash.Length == oldHashLength)
                    {
                        var hash2 = ComputeHash();
                        if (hash2 != null)
                            loadedHash = hash2;
                    }
                }
                
                return loadedHash;
            }
            set
            {
                if (loadedHash == value) return;
                loadedHash = value;
                hashVerified = loadedHash?.Length != oldHashLength;
            }
        }

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
        [XmlElement("PackageRepositoryUrl")]
        [DefaultValue(null)]
        [Obsolete("Please use PackageSource instead.")]
        public string Location { get; set; }

        /// <summary>
        /// Information of the source of the package definition. 
        /// </summary>
        [DefaultValue(null)]
        public IPackageDefSource PackageSource { get; set; }
        
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
        /// Name of the owner of the package. There can be multiple owners of a package, in which case this string will have several entries separated with ','.
        /// </summary>
        [DefaultValue(null)]
        public string Owner { get; set; }

        /// <summary>
        /// Link to the package source code. This is intended for open sourced projects.
        /// </summary>
        [DefaultValue(null)]
        public string SourceUrl { get; set; }
        
        /// <summary>
        /// Specific open source license. Must be a SPDX identifier, read more at https://spdx.org/licenses/.
        /// </summary>
        [DefaultValue(null)]
        public string SourceLicense { get; set; }

        /// <summary>
        /// License(s) required to use this package. During package create all '<see cref="PackageFile.LicenseRequired"/>' attributes from '<see cref="Files"/>' will be concatenated into this property.
        /// Bundle packages (<see cref="Class"/> is 'bundle') can use this property to show licenses that are required by the bundle dependencies. 
        /// </summary>
        [XmlAttribute]
        [DefaultValue("")]
        public string LicenseRequired { get; set; } = "";

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
        /// Name of the group that this package belongs to. Groups can be nested in other groups, in which case this string will have several entries separated with '/' or '\'. May be empty or null. UIs may use this information to show a list of packages as a tree structure.
        /// </summary>
        [XmlAttribute]
        public string Group { get; set; }
        
        /// <summary>
        /// A list of keywords that describe the package. Tags are separated by space or comma.
        /// </summary>
        [XmlAttribute]
        public string Tags { get; set; }

        string rawVersion;
        
        /// <summary>
        /// Returns version as a <see cref="SemanticVersion"/>.
        /// </summary>
        /// <returns></returns>
        internal string RawVersion
        {
            get => rawVersion;
            set
            {
                rawVersion = value;
                if (this.Version == null && SemanticVersion.TryParse(value, out var version))
                    Version = version;
            }
        }

        /// <summary>
        /// A list of files contained in this package.
        /// </summary>
        public List<PackageFile> Files { get; set; }

        /// <summary>
        /// Contains definitions of CLI actions which will invoke external commands
        /// </summary>
        public List<ExternalCliAction> ExternalCliActions { get; set; }

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
            ExternalCliActions = new List<ExternalCliAction>();
            PackageActionExtensions = new List<ActionStep>();
            OS = "Windows";
            Architecture = CpuArchitecture.AnyCPU;
            
            if (string.IsNullOrWhiteSpace(FileType))
                FileType = "tappackage";
            if (string.IsNullOrWhiteSpace(Class))
                Class = "package";
        }

        /// <summary>
        /// Returns a string representation of this PackageDef containing name and version.
        /// </summary>
        /// <returns></returns>
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
            var filesElement = root.Element(xns.GetName("Files"));
            if (filesElement != null)
            {
                var fileElements = filesElement.Elements(xns.GetName("File"));
                foreach (var file in fileElements)
                {
                    var plugins = file.Element(xns.GetName("Plugins"));
                    if (plugins == null) continue;

                    var pluginElements = plugins.Elements(xns.GetName("Plugin"));
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
            using var writer = XmlWriter.Create(stream);
            using var _ = TypeData.WithTypeDataCache();
            
            writer.WriteStartDocument();
            writer.WriteStartElement("ArrayOfPackages");
            // Write fragments because we manually insert the start and end of the document.
            // This way, if the stream is outgoing from the process, we avoid having to store all the document
            // in memory. This can be useful as 'packages' may come from a stream itself.
            var serializer = new TapSerializer { WriteFragments = true };
            
            // added batching as a speculative performance improvement.
            foreach (PackageDef package in packages.Batch(32))
            {
                try
                {
                    serializer.Serialize(writer, package);
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }
            
            writer.WriteEndElement();
            writer.Flush();
        }

        /// <summary>
        /// Reads a stream of XML into a list of PackageDef objects.
        /// </summary>
        public static IEnumerable<PackageDef> ManyFromXml(Stream stream)
        {
            var root = XElement.Load(stream);
            List<PackageDef> packages = new List<PackageDef>();

            Parallel.ForEach(root.Nodes(), node =>
            {
                using (Stream str = new MemoryStream())
                {
                    if (node is XElement nodeElement)
                    {
                        nodeElement.Save(str);
                        str.Seek(0, 0);
                        var package = FromXml(str);
                        if (package != null)
                        {
                            lock (packages)
                            {
                                packages.Add(package);
                            }
                        }
                    }
                    else
                    {
                        throw new XmlException("Invalid XML");
                    }
                }
            });

            return packages;
        }

        internal static bool TryFromPackage(string path, out PackageDef package)
        {
            try 
            {
                package = FromPackage(path);
                return true;
            }
            catch 
            {
                package = null;
                return false;
            }
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
#pragma warning disable 618
            pkgDef.Location = Path.GetFullPath(path);
#pragma warning restore 618
            pkgDef.PackageSource = new FilePackageDefSource
            {
                PackageFilePath = Path.GetFullPath(path)
            };
            
            return pkgDef;
        }

        /// <summary>
        /// Constructs a PackageDef objects to represent each package inside a *.TapPackages file.
        /// </summary>
        public static List<PackageDef> FromPackages(string path)
        {
            var packageList = new List<PackageDef>();

            if (Path.GetExtension(path).ToLower() != ".tappackages")
            {
                packageList.Add(FromPackage(path));
                return packageList;
            }

            try
            {
                using (var zip = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read))
                {
                    foreach (var part in zip.Entries)
                    {
                        FileSystemHelper.EnsureDirectoryOf(part.FullName);
                        var instream = part.Open();
                        using (var outstream = File.Create(part.FullName))
                        {
                            var task = instream.CopyToAsync(outstream, 4096, TapThread.Current.AbortToken);
                            ConsoleUtils.PrintProgressTillEnd(task, "Decompressing", () => outstream.Position, () => part.Length);
                        }
                        
                        var package = FromPackage(part.FullName);
                        packageList.Add(package);

                        if (File.Exists(part.FullName))
                            File.Delete(part.FullName);
                    }
                }
            }
            catch (InvalidDataException)
            {
                log.Error($"Could not unpackage '{path}'.");
                throw;
            }

            return packageList;
        }

        /// <summary>
        /// Throws InvalidDataException if the xml in the file does not conform to the schema.
        /// </summary>
        public static void ValidateXml(string path)
        {
            var package = FromXml(path);
            if (string.IsNullOrWhiteSpace(package.Name))
                throw new InvalidDataException("Package Name cannot be empty.");
            if (package.Version == null && package.RawVersion == null)
                throw new InvalidDataException("Package Version cannot be empty.");
            if (string.IsNullOrWhiteSpace(package.OS))
                throw new InvalidDataException("Package OS cannot be empty.");
            if (package.Architecture == CpuArchitecture.Unspecified)
                throw new InvalidDataException("Package Architecture cannot be unspecified.");
        }

        /// <summary>
        /// Constructs a PackageDef objects to represent the package definition in the given xml file.
        /// </summary>
        public static PackageDef FromXml(string path)
        {
            using var stream = File.OpenRead(path);
            return FromXml(stream);
        }
        
        /// <summary>
        /// Returns the XML schema for a package definition XML file.
        /// </summary>
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
        /// Relative path to the directory holding OpenTAP Package definition files
        /// </summary>
        public const string PackageDefDirectory = "Packages";
        /// <summary>
        /// Absolute path to the directory representing the OpenTAP installation dir for system-wide packages
        /// </summary>
        public static string SystemWideInstallationDirectory { get => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Keysight", "Test Automation"); }

        /// <summary>
        /// File name for package definition files inside packages.
        /// </summary>
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

            // don't use Path.Combine, as that might create \ which makes the package unable to install on linux
            return String.Join("/", installationDir, PackageDef.PackageDefDirectory, name, PackageDef.PackageDefFileName); 
        }

        /// <summary>
        /// Perform a BFS search to find all package xml's that are not descendants of a .OpenTapIgnore file
        /// </summary>
        /// <param name="packageDir"></param>
        /// <returns></returns>
        private static IEnumerable<string> FindPackageDefinitions(string packageDir)
        {
            var queue = new Queue<string>();
            var results = new List<string>();
            queue.Enqueue(packageDir);

            while (queue.Any())
            {
                var dir = queue.Dequeue();
                try
                {
                    var files = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);

                    if (files.Any(f =>
                        string.Equals(Path.GetFileName(f), ".OpenTapIgnore", StringComparison.InvariantCulture)))
                        continue;

                    var packageXml = files.FirstOrDefault(f =>
                        string.Equals(Path.GetFileName(f), "package.xml", StringComparison.InvariantCulture));

                    if (packageXml != null)
                        results.Add(packageXml);

                    foreach (var subdir in Directory.GetDirectories(dir))
                        queue.Enqueue(subdir);

                }
                catch (Exception ex)
                {
                    log.Warning($"Failed reading content of '{dir}'.");
                    log.Debug(ex);
                }
            }

            return results;
        }


        internal static List<string> GetPackageMetadataFilesInTapInstallation(string tapPath)
        {
            List<string> metadatas = new List<string>();

            // this way of searching for package.xml files will find them both in their 8.x 
            // location (Package Definitions/<PkgName>.package.xml) and in their new 9.x
            // location (Packages/<PkgName>/package.xml)

            // Add 9.x packages in "Packages" folder
            string packageDir = Path.GetFullPath(Path.Combine(tapPath, PackageDefDirectory));

            if (Directory.Exists(packageDir))
            {
                var packageDefinitions = FindPackageDefinitions(packageDir);
                metadatas.AddRange(packageDefinitions);
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

        /// <summary>
        /// Computes the hash/signature of the package based on its definition. 
        /// This method relies on hashes of each file. If those are not already part of the definition (they are normally computed when the package is created), this method will try to compute them based on files on the disk.
        /// </summary>
        /// <returns>A base64 encoded SHA1 hash of relevant fields in the package definition</returns>
        public string ComputeHash()
        {
            using MemoryStream str = new MemoryStream();
            using (TextWriter wtr = new StreamWriter(str, Encoding.Default, 4096, true))
            {
                wtr.Write(this.Name);
                wtr.Write(this.Version);
                wtr.Write(this.OS);
                wtr.Write(this.Architecture);
                wtr.Write(this.Date);
                wtr.Write(this.Description);
                wtr.Write(string.Join("", this.Dependencies.OrderBy(d => d.Name).Select(d => d.Name + d.Version)));
                foreach (PackageFile file in this.Files.OrderBy(f => f.FileName))
                {
                    FileHashPackageAction.Hash fileHash =
                        file.CustomData.OfType<FileHashPackageAction.Hash>().FirstOrDefault();
                    if (fileHash != null)
                        wtr.Write(fileHash.Value);
                    else
                        wtr.Write(file.FileName);
                }
            }

            str.Seek(0, SeekOrigin.Begin);
            using var algorithm = SHA1.Create();
            var bytes = algorithm.ComputeHash(str);
            return Utils.Base64UrlEncode(bytes);
        }

        internal PackageSpecifier GetSpecifier() => new PackageSpecifier(Name, Version.AsExactSpecifier(), Architecture, OS);
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
                var opentapPackage = Installation.Current.GetOpenTapPackage();
                if (opentapPackage != null)
                    currentArchitecture = opentapPackage.Architecture;

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
            if (plugin == CpuArchitecture.AnyCPU || host == CpuArchitecture.Unspecified) return true; // TODO: Figure out if this should be allowed in the long term

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
