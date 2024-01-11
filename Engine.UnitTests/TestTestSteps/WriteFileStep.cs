using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

namespace OpenTap.Engine.UnitTests.TestTestSteps
{
    [Display("Write File", Description: "Writes a string to a file.", Group: "Tests")]
    public class WriteFileStep : TestStep
    {
        [Layout(LayoutMode.Normal, rowHeight: 5)]
        public string String { get; set; }
        public string File { get; set; }
        public override void Run() => System.IO.File.WriteAllText(File, String);
    }

    [Display("Create Directory", Description: "Creates a new directory.", Group: "Tests")]
    public class CreateDirectoryStep : TestStep
    {
        public string Directory { get; set; }
        public override void Run()
        {
            System.IO.Directory.CreateDirectory(Directory);
        }
    }

    [Display("Replace In File", Description: "Replaces some text in a file.", Group: "Tests")]
    public class ReplaceInFileStep : TestStep
    {
        [FilePath]
        [Display("File", Order: 0)]
        public string File { get; set;}
        [Layout(LayoutMode.Normal, rowHeight: 5)]
        [Display("Search For", Order: 1)]
        public string Search { get; set; }
        [Layout(LayoutMode.Normal, rowHeight: 5)]
        [Display("Replace With", Order: 2)]
        public string Replace { get; set; }

        public override void Run()
        {
            var content = System.IO.File.ReadAllText(File);
            content = content.Replace(Search, Replace);
            System.IO.File.WriteAllText(File, content);
        }
    }

    [Display("Expect", Description: "Expects  verdict in the child step.", Group: "Tests")]
    [AllowAnyChild]
    public class ExpectStep : TestStep
    {
        public Verdict ExpectedVerdict { get; set; }
        public override void Run()
        {
            RunChildSteps();
            if (Verdict == ExpectedVerdict)
                Verdict = Verdict.Pass;
            else
            {
                Verdict = Verdict.Fail;
            }
        }
    }

    public enum VersionType
    {
        FileVersion,
        SemanticVersion
    }

    [Display("Read Assembly Version", Group: "Tests")]
    public class ReadAssemblyVersionStep : TestStep
    {
        [FilePath]
        public string File { get; set; }
        
        public string MatchVersion { get; set; }
        public VersionType VersionType { get; set; }

        void CheckFileVersion()
        {
            var semver = GetVersion(File);
            if (semver == null)
            {
                Log.Error("Unable to read version info.");
            }

            if (string.IsNullOrWhiteSpace(MatchVersion) == false)
            {
                if (Equals(semver.ToString(), MatchVersion))
                {
                    UpgradeVerdict(Verdict.Pass);
                }
                else
                {
                    UpgradeVerdict(Verdict.Fail);
                }
            }
            Log.Info("Read Version {0}", semver.ToString());
            
        }

        void CheckSemanticVersion()
        {
            if (!SemanticVersion.TryParse(MatchVersion, out var semver))
            {
                Log.Error($"{MatchVersion} is not a semantic version.");
                UpgradeVerdict(Verdict.Error);
                return;
            }
            
            var s = PluginManager.GetSearcher();
            s.AddAssembly(File, null);

            string normalize(string s)
            {
                return Path.GetFullPath(s).TrimEnd('/', '\\');
            }

            var nf = normalize(File);
            var asm = s.Assemblies.FirstOrDefault(a => normalize(a.Location).Equals(nf, StringComparison.InvariantCultureIgnoreCase));

            if (asm == null)
            {
                Log.Error("Assembly not loaded.");
                UpgradeVerdict(Verdict.Error);
                return;
            }

            var asmVer = asm.SemanticVersion;

            if (Equals(asmVer.ToString(), semver.ToString()))
            {
                UpgradeVerdict(Verdict.Pass);
            }
            else
            {
                UpgradeVerdict(Verdict.Fail);
            }
            Log.Info("Read Version {0}", asmVer.ToString());
        }

        public override void Run()
        {
            if (false == System.IO.File.Exists(File))
                throw new FileNotFoundException("File does not exist", File);

            switch (VersionType)
            {
                case VersionType.FileVersion:
                    CheckFileVersion();
                    break;
                case VersionType.SemanticVersion:
                    CheckSemanticVersion();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static SemanticVersion GetVersion(string path)
        {
            var searcher = new PluginSearcher();
            searcher.Search(new[] {path});
            Log.Debug("Searching {0}", path);
            var asm = searcher.Assemblies.First();
            using (FileStream file = new FileStream(asm.Location, FileMode.Open, FileAccess.Read))
            using (PEReader header = new PEReader(file, PEStreamOptions.LeaveOpen))
            {
                var CurrentReader = header.GetMetadataReader();
                Log.Debug("Opened file");
                foreach (CustomAttributeHandle attrHandle in CurrentReader.GetAssemblyDefinition().GetCustomAttributes())
                {
                    
                    CustomAttribute attr = CurrentReader.GetCustomAttribute(attrHandle);
                    Log.Info("attribute: {0}",attr.Constructor.Kind);
                    if (attr.Constructor.Kind == HandleKind.MemberReference)
                    {
                        var ctor = CurrentReader.GetMemberReference((MemberReferenceHandle) attr.Constructor);
                        string attributeFullName = "";
                        if (ctor.Parent.Kind == HandleKind.TypeDefinition)
                        {
                            var def = CurrentReader.GetTypeDefinition((TypeDefinitionHandle)ctor.Parent);
                            attributeFullName = string.Format("{0}.{1}", CurrentReader.GetString(def.Namespace), CurrentReader.GetString(def.Name));
                        }
                        if (ctor.Parent.Kind == HandleKind.TypeReference)
                        {
                            var r = CurrentReader.GetTypeReference((TypeReferenceHandle)ctor.Parent);
                            attributeFullName = string.Format("{0}.{1}", CurrentReader.GetString(r.Namespace), CurrentReader.GetString(r.Name));
                        }
                        Log.Info("Found attribute: {0}", attributeFullName);

                        if (attributeFullName == typeof(System.Reflection.AssemblyInformationalVersionAttribute).FullName)
                        {
                            var valueString = attr.DecodeValue(new CustomAttributeTypeProvider());
                            if (SemanticVersion.TryParse(valueString.FixedArguments[0].Value.ToString(), out SemanticVersion infoVer))
                                return infoVer;
                        }
                    }
                }
            }
            Log.Warning("No Version found");
            return null;
        }

        struct CustomAttributeTypeProvider : ICustomAttributeTypeProvider<TypeData>
        {
            public TypeData GetPrimitiveType(PrimitiveTypeCode typeCode){return null;}
            public TypeData GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind){return null;}
            public TypeData GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind){return null;}
            public TypeData GetSZArrayType(TypeData elementType){return null;}
            public TypeData GetSystemType(){return null;}
            public bool IsSystemType(TypeData type){throw new System.NotImplementedException();}
            public TypeData GetTypeFromSerializedName(string name){return null;}
            public PrimitiveTypeCode GetUnderlyingEnumType(TypeData type){throw new System.NotImplementedException();}
        }
    }

    [Display("Remove Directory", "Removes a directory.", "Tests")]
    public class RemoveDirectory : TestStep
    {
        [DirectoryPath]
        public string Path { get; set; }
        public override void Run()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
    }

    
    [AllowAnyChild]
    [Display("Run On OS", "Runs the child steps on a specific OS.", "Tests")]
    public class RunOnOs : TestStep
    {

        public string[] AvailableOperatingSystems => new []{ "MacOS", "Linux", "Windows"};

        [AvailableValues(nameof(AvailableOperatingSystems))]
        public string OperatingSystem { get; set; } = "Windows";
        
        public RunOnOs()
        {
            this.Name = "Run On {OperatingSystem}";
        }
        
        public override void Run()
        {
            if (OpenTap.OperatingSystem.Current.Name == OperatingSystem)
            {
                RunChildSteps();
            }
            else
            {
                Log.Info("Skipping Child Steps On {0}", OpenTap.OperatingSystem.Current);
            }
        }
    }

    [Display("Find File Step", "Finds a file in a dir. Pass when found, fail if not.", "Tests")]
    public class FindFileStep : TestStep
    {
        [DirectoryPath] public MacroString SearchDir { get; set; } = new MacroString {Text = "."};

        public string Regex { get; set; } = "\\.zip";

        public override void Run()
        {
            var dir = SearchDir.Expand(this.PlanRun);
            var regex = new Regex(Regex);
            foreach(var file in Directory.GetFiles(dir))
            {
                
                if (regex.IsMatch(file))
                {
                    Log.Debug("Matched file: {0}", file);
                    UpgradeVerdict(Verdict.Pass);
                    return;
                }
            }
            UpgradeVerdict(Verdict.Fail);
        }
    }
}