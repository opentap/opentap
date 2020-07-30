using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using OpenTap.Cli;
using OpenTap.Package;

namespace OpenTap.Sdk.New
{
    [Display("package-reference", "Add an OpenTAP Package to a C# Project (.csproj).", Groups: new[] { "sdk", "new" })]
    public class NewPackageReference : ICliAction
    {
        [UnnamedCommandLineArgument("package-name")]
        public string PackageName { get; set; }
        [CommandLineArgument("version", Description = "Version of the package (newest version if not set).")]
        public string Version { get; set; }
        
        [CommandLineArgument("csproj", Description = "C# project file to add the package reference to.")]
        public string Project { get; set; }

        [CommandLineArgument("repository",
            Description = "The package repository to use (default: packages.opentap.io)")]
        public string PackageRepository { get; set; } = "packages.opentap.io";
        
        [CommandLineArgument("additional", Description = "Specify this as an 'additional' package reference, not referencing the DLLs directly.")]
        public bool Additional { get; set; }
        
        [CommandLineArgument("configuration", Description = "In which build configuration this should be used.")]
        public string Configuration { get; set; }

        static TraceSource log = Log.CreateSource("sdk");
        public int Execute(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(PackageName))
            {
                log.Error("Please specify a valid package name.");
                return 1;
            }

            {   // check if the package exists in the repo.
                var repo = new HttpPackageRepository(PackageRepository);
                var spec = new PackageSpecifier(PackageName, Version != null ? VersionSpecifier.Parse(Version) : null);
                var pkg = repo.GetPackages(spec, TapThread.Current.AbortToken).FirstOrDefault();
                if (pkg == null)
                {
                    log.Error("No such package");
                    return 2;
                }
                if (Version == null)
                    Version = pkg.Version.ToString();
                PackageName = pkg.Name;
            }
            
            string referenceType = Additional ? "AdditionalOpenTapPackage" : "OpenTapPackageReference";
            
            string insert =
                $"<{referenceType} Include=\"{PackageName}\" Version=\"{Version}\" Repository=\"{PackageRepository}\"/>";
            
            var csproj = Project ?? get_csproj();
            if (csproj == null)
            {  // error was already printed.
                return 1;
            }

            if (File.Exists(csproj) == false)
            {
                log.Error("C# project files does not exist {0}", csproj);
                return 2;
            }
            
            var document = XDocument.Load(csproj, LoadOptions.PreserveWhitespace);
            var projectXml = document.Element("Project");
            XElement itemGroupXml = null;
            var condition = $"'$(Configuration)' == '{Configuration}'";
            
            var itemGroups = projectXml.Elements("ItemGroup");
            
            // if condition is select take the group matching the conditions.
            if (string.IsNullOrWhiteSpace(Configuration) == false)
                itemGroups = itemGroups.Where(grp => grp.Attribute("Condition")?.Value == condition);
            
            bool needsAddNewElem = true; 
            
            foreach (var grp in itemGroups)
            {
                var existingElem = grp.Elements(referenceType)
                    .FirstOrDefault(elem => elem.Attribute("Include")?.Value == PackageName);
                
                if (existingElem != null)
                {  // the package reference already exists. In this case, lets try to just update set the version.
                    
                    var version = existingElem.Attribute("Version");
                    if (version != null)
                    {
                        if (version.Value == Version)
                        {
                            log.Info("Package {0} version {1} already in the csproj", PackageName, Version);
                            return 0;
                        }
                        version.Value = Version;
                        needsAddNewElem = false;
                        break;
                    }
                }
            }

            if (needsAddNewElem)
            {
                // add a new OpenTapPackageReference or AdditionalOpenTapPackage element.
                // to make the whitespace look right, there is some adding additional whitespace
                
                // Try to find the existing item group used for package references.
                foreach (var grp in itemGroups)
                {
                    if (grp.Elements(referenceType).Any())
                    {
                        itemGroupXml = grp;
                        break;
                    }
                }
                
                if (itemGroupXml == null)
                {
                    itemGroupXml = new XElement("ItemGroup");
                    if (string.IsNullOrWhiteSpace(Configuration) == false)
                    {
                        // e.g Condition="'{Configuration}' == 'Debug'"
                        itemGroupXml.Add(new XAttribute("Condition", condition));
                    }
                    itemGroupXml.Add("\n");
                    itemGroupXml.Add("   ");
                    projectXml.Add("\n");
                    projectXml.Add("   ");
                    projectXml.Add(itemGroupXml);
                    projectXml.Add("\n");
                }

                itemGroupXml.Add("   ");
                itemGroupXml.Add(XElement.Parse(insert));
                itemGroupXml.Add("\n");
                itemGroupXml.Add("   ");
            }

            document.Save(csproj, SaveOptions.DisableFormatting);
            return 0;
        }

        static string get_csproj()
        {
            var csprojFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj").ToArray();
            if (csprojFiles.Length == 0)
            {
                log.Error("Unable to find any csproj file in the current directory. Use --csproj to specify.");
                return null;
            }

            if (csprojFiles.Length > 1)
            {
                log.Error("Directory contains multiple csproj files, please specify using --csproj");
                return null;
            }

            return csprojFiles[0];
        }
    }
}