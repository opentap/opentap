using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class PdbFixupTest
    {
        [Test]
        public void OpenTapSymbolsFixed()
        {
            var xml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Package Name=""TestPackage""   Version=""1.0.0"" OS=""{OperatingSystem.Current}"" Architecture=""x64"">
    <Files>
         <File Path=""tap.dll"">
            <SetAssemblyInfo Attributes=""Version"" />
        </File>
        <File Path=""OpenTap.dll"">
            <SetAssemblyInfo Attributes=""Version"" />
        </File>
        <File Path=""OpenTap.Package.dll"">
            <SetAssemblyInfo Attributes=""Version"" />
        </File>
        <File Path=""OpenTap.Plugins.BasicSteps.dll"">
            <SetAssemblyInfo Attributes=""Version"" />
        </File>
        <File Path=""OpenTap.Cli.dll"">
            <SetAssemblyInfo Attributes=""Version"" />
        </File>
        <File Path=""tap.pdb""/>
        <File Path=""OpenTap.Package.pdb""/>
        <File Path=""OpenTap.Plugins.BasicSteps.pdb""/>
        <File Path=""OpenTap.Cli.pdb""/>
    </Files>
</Package>
";
            var packageFile = Path.GetTempFileName();
            File.WriteAllText(packageFile, xml);
            var outFile = Path.GetTempFileName();
            
            var files = new string[]
                { "tap", "OpenTap.Package", "OpenTap.Plugins.BasicSteps", "OpenTap.Cli" };

            var create = new PackageCreateAction()
            {
                Install = false,
                OutputPaths = new[] { outFile },
                PackageXmlFile = packageFile
            };
            
            Assert.AreEqual(0, create.Execute(CancellationToken.None), "Failed to create package.");

            var outDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(outDir);

            using (var fs = File.OpenRead(outFile))
            {
                using (var archive = new ZipArchive(fs))
                {
                    archive.ExtractToDirectory(outDir);
                }
            }

            try
            {
                foreach (var file in files)
                {
                    var @base = Path.Combine(outDir, file);
                    var rawAssembly = File.ReadAllBytes(@base + ".dll");
                    var rawSymbols = File.ReadAllBytes(@base + ".pdb");

                    var asmStream = new MemoryStream(rawAssembly);
                    asmStream.Seek(0, SeekOrigin.Begin);
                    var symbolStream = new MemoryStream(rawSymbols);
                    symbolStream.Seek(0, SeekOrigin.Begin);

                    void load()
                    {
                        // This will throw if the symbols don't match
                        AssemblyDefinition.ReadAssembly(asmStream,
                            new ReaderParameters()
                            {
                                InMemory = true, ReadSymbols = true, SymbolStream = symbolStream,
                                ReadingMode = ReadingMode.Immediate
                            });
                    }

                    try
                    {
                        load();
                    }
                    catch
                    {
                        Assert.Fail("Debugging symbols did not match!");
                    }
                }
            }
            finally
            {
                File.Delete(packageFile);
                File.Delete(outFile);
                Directory.Delete(outDir, true);
            }
        }
    }
}