using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace OpenTap.Package.UnitTests
{
    internal class CsProj
    {
        private string Filename => "test.csproj";
        public List<string> PropertyGroups { get; set; }
        public List<string> ItemGroups { get; set; }
        public List<string> Imports { get; set; }
        public ProjectBuildTest ProjectBuildTest { get; set; }
        string OutDir => Path.Combine(ProjectBuildTest.WorkingDirectory, "bin");

        public override string ToString()
        {
            return @"<Project Sdk=""Microsoft.NET.Sdk"" ToolsVersion=""Current"">" + '\n' +
                   string.Join("\n", PropertyGroups) + '\n' +
                   string.Join("\n", ItemGroups) + '\n' +
                   string.Join("\n", Imports) + '\n' +
                   "</Project>\n";
        }

        string[] _linesCache;
        public int GetLineNo(string str)
        {
            if (_linesCache == null)
                _linesCache = this.ToString().Split('\n');
                
            return _linesCache.IndexWhen(l => l.Contains(str)) + 1;
        }

        public CsProj(ProjectBuildTest caller)
        {
            ProjectBuildTest = caller;
            PropertyGroups = new List<string>()
            {$@"                   
    <PropertyGroup>
        <TargetFrameworkIdentifier></TargetFrameworkIdentifier>
        <TargetFrameworkVersion></TargetFrameworkVersion>
        <TargetFramework>netstandard2.0</TargetFramework>
        <OutputPath>{Directory.GetCurrentDirectory()}</OutputPath>

    </PropertyGroup>"
            };
            ItemGroups = new List<string>();
            Imports = new List<string>()
            {
                $@"
<Import Project=""{caller.TargetsFile}"" />"
            };
        }
        
        public (string Stdout, string Stderr, int ExitCode) Build()
        {
            using (var writer = new StreamWriter(Path.Combine(ProjectBuildTest.WorkingDirectory, Filename)))
                writer.Write(this.ToString());            

            if (OperatingSystem.Current != OperatingSystem.Windows)
            {
                // // The MSBuild generator is sometimes too eager to skip steps on Linux -- make sure targets are run
                if (File.Exists(ProjectBuildTest.OutputFile))
                    File.Delete(ProjectBuildTest.OutputFile);
            }
            
            var process = new Process
            {
                StartInfo =
                {
                    FileName = "dotnet",
                    Arguments = $"build {Filename} --verbosity normal",
                    WorkingDirectory = ProjectBuildTest.WorkingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            var timeLimit = TimeSpan.FromSeconds(30);
            var stdout = "";
            var stderr = "";

            process.Start();

            while (!process.HasExited && (DateTime.Now - process.StartTime) < timeLimit)
            {
                TapThread.Sleep(TimeSpan.FromSeconds(1));
                
                stdout += process.StandardOutput.ReadToEnd();
                stderr += process.StandardError.ReadToEnd();
            }
            
            return (stdout, stderr, process.ExitCode);
        }
        
        public string GetGeneratedFileContent()
        {
            using (var reader = new StreamReader(ProjectBuildTest.OutputFile))
            {
                return reader.ReadToEnd();
            }
        }
    }

    
    public class ProjectBuildTest
    {
        #region Broken test packages that should be disabled temporarily
        private string packagesDir =>
            Path.Combine(Path.GetDirectoryName(typeof(ProjectBuildTest).Assembly.Location), "Packages");
        private string missingDep1 => Path.Combine(packagesDir, "CheckDependencies_MissingDep", "package.xml");
        private string missingDep2 => Path.Combine(packagesDir, "CheckDependencies_MissingDep", "_package.xml");
        private string allDeps1 => Path.Combine(packagesDir, "CheckDependencies_AllDepsInstalled ", "package.xml");
        private string allDeps2 => Path.Combine(packagesDir, "CheckDependencies_AllDepsInstalled ", "_package.xml");

        [OneTimeSetUp]
        public void SetUp()
        {
            if (File.Exists(missingDep1))
                File.Move(missingDep1, missingDep2, true);
            if (File.Exists(allDeps1))
                File.Move(allDeps1, allDeps2, true);

        }

        [OneTimeTearDown]
        public void TearDown()
        {
            if (File.Exists(missingDep2))
                File.Move(missingDep2, missingDep1, true);
            if (File.Exists(allDeps2))
                File.Move(allDeps2, allDeps1, true);
        }

        #endregion

        // Current directory is assumed to be the OutputFolder containing tap.exe etc.
        public string WorkingDirectory { get; }
        public string OutputFile => Path.Combine(WorkingDirectory, "obj", "test.opentap.g.props");
        public string TargetsFile { get; }

        private static string FileRepository => Path.Combine(Directory.GetCurrentDirectory(), "TapPackages");

        public ProjectBuildTest()
        {
            WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "buildTestDir");
            var packages = Directory.EnumerateFiles(FileRepository, "*.TapPackage").ToList();
            var str = string.Join("\n", packages);
            
            StringAssert.Contains("MyPlugin4", str);
            StringAssert.Contains("MyPlugin5", str);
            
            var targetsFile = new FileInfo("OpenTap.targets");
            if (targetsFile.Exists == false)
                throw new Exception(
                    $"OpenTap.targets not found in {Directory.GetCurrentDirectory()}. Tests cannot continue.");
            TargetsFile = targetsFile.FullName;

            if (Directory.Exists(WorkingDirectory) == false)
                Directory.CreateDirectory(WorkingDirectory);
        }

        [Test]
        public void NoReferencesTest()
        {
            var result = new CsProj(this).Build();

            StringAssert.Contains("Got 0 OpenTapPackageReference targets.", result.Stdout);
            Assert.AreEqual(result.ExitCode, 0);
            Assert.True(File.Exists(OutputFile));
        }

        [Test]
        public void OneReferenceDefaultTest()
        {
            var csProj = new CsProj(this);
            csProj.ItemGroups.Add($@"
   <ItemGroup>
	  <OpenTapPackageReference Include=""MyPlugin4"" Repository=""{FileRepository}"" />	  
  </ItemGroup>
");
            csProj.ItemGroups.Add($@"
   <ItemGroup>
	  <OpenTapPackageReference Include=""MyPlugin4"" Repository=""{FileRepository}"" />	  
  </ItemGroup>
");
            csProj.PropertyGroups.Add($@"
    <PropertyGroup>
        <PlatformTarget>x86</PlatformTarget> 
    </PropertyGroup>
            ");

            var result = csProj.Build();

            if (OpenTap.OperatingSystem.Current == OperatingSystem.Windows)
            {

                int lineNo =
                    csProj.GetLineNo(
                        $@"<OpenTapPackageReference Include=""MyPlugin4"" Repository=""{FileRepository}"" />");

                StringAssert.Contains($"test.csproj({lineNo}): warning OpenTAP Reference: Duplicate entry detected.",
                    result.Stdout);

                StringAssert.Contains("Got 1 OpenTapPackageReference targets.", result.Stdout);
                StringAssert.Contains("MyPlugin4 Include=\"**\" Exclude=\"Dependencies/**\"", result.Stdout);
                StringAssert.Contains("Skipped duplicate entries", result.Stdout);
                Assert.AreEqual(result.ExitCode, 0);

                Assert.True(File.Exists(OutputFile));
                var Generated = csProj.GetGeneratedFileContent();
                StringAssert.Contains("MyPlugin4.dll", Generated);
            }
            else
            {   // Expected error message on 
                StringAssert.Contains("x86 builds are not supported on Unix.", result.Stdout);
            }
        }

        [Test]
        public void TwoEqualReferencesDefaultTest()
        {
            var csProj = new CsProj(this);
            csProj.ItemGroups.Add($@"
   <ItemGroup>
	  <OpenTapPackageReference Repository=""{FileRepository}"" Include=""MyPlugin4"" />	  
  </ItemGroup>
");

            csProj.ItemGroups.Add($@"
   <ItemGroup>
	  <OpenTapPackageReference Repository=""{FileRepository}"" Include=""MyPlugin5"" />	  
  </ItemGroup>
");

            var result = csProj.Build();

            StringAssert.Contains("Got 2 OpenTapPackageReference targets.", result.Stdout);
            StringAssert.Contains("MyPlugin4 Include=\"**\" Exclude=\"Dependencies/**\"", result.Stdout);
            StringAssert.Contains("MyPlugin5 Include=\"**\" Exclude=\"Dependencies/**\"", result.Stdout);
            Assert.AreEqual(result.ExitCode, 0);

            Assert.True(File.Exists(OutputFile));
            var Generated = csProj.GetGeneratedFileContent();
            StringAssert.Contains("MyPlugin4.dll", Generated);
            StringAssert.Contains("MyPlugin5.dll", Generated);
        }

        [Test]
        public void TwoEqualReferencesSpecifiedTest()
        {
            var csProj = new CsProj(this);
            csProj.ItemGroups.Add($@"
   <ItemGroup>
	  <OpenTapPackageReference Repository=""{FileRepository}"" Include=""MyPlugin4"" IncludeAssemblies=""test1;test2"" ExcludeAssemblies=""test3;test4"" />	  
  </ItemGroup>
");

            csProj.ItemGroups.Add($@"
   <ItemGroup>
	  <OpenTapPackageReference Repository=""{FileRepository}"" Include=""MyPlugin5"" IncludeAssemblies=""test1;test2"" ExcludeAssemblies=""test3;test4"" />	  
  </ItemGroup>
");

            var result = csProj.Build();

            StringAssert.Contains("Got 2 OpenTapPackageReference targets.", result.Stdout);
            StringAssert.Contains("MyPlugin4 Include=\"test1,test2\" Exclude=\"test3,test4\"", result.Stdout);
            StringAssert.Contains("MyPlugin5 Include=\"test1,test2\" Exclude=\"test3,test4\"", result.Stdout);
            Assert.AreEqual(result.ExitCode, 0);

            Assert.True(File.Exists(OutputFile)); 
        }

        [Test]
        public void TwoDifferentReferencesSpecifiedTest()
        {
            var csProj = new CsProj(this);
            csProj.ItemGroups.Add($@"
   <ItemGroup>
	  <OpenTapPackageReference Repository=""{FileRepository}"" Include=""MyPlugin5""> 
        <IncludeAssemblies>test5;test6</IncludeAssemblies> 
        <ExcludeAssemblies>test7;test8</ExcludeAssemblies>
      </OpenTapPackageReference>
  </ItemGroup>
");
            csProj.ItemGroups.Add($@"
    <ItemGroup>
      <OpenTapPackageReference Include=""MyPlugin4"">    
        <Repository>{FileRepository}</Repository> 
        <IncludeAssemblies>test1;test2</IncludeAssemblies> 
        <ExcludeAssemblies>test3;test4</ExcludeAssemblies>
      </OpenTapPackageReference>
    </ItemGroup>
");

            var result = csProj.Build();

            int index1 =
                csProj.GetLineNo($@"<OpenTapPackageReference Repository=""{FileRepository}"" Include=""MyPlugin5"">");
            int index2 = csProj.GetLineNo(@"<OpenTapPackageReference Include=""MyPlugin4"">");

            StringAssert.Contains($"test.csproj({index1}): warning OpenTAP Reference: No references added from package 'MyPlugin5'", result.Stdout);
            StringAssert.Contains($"test.csproj({index2}): warning OpenTAP Reference: No references added from package 'MyPlugin4'", result.Stdout);

            StringAssert.Contains("Got 2 OpenTapPackageReference targets.", result.Stdout);
            StringAssert.Contains("MyPlugin4 Include=\"test1,test2\" Exclude=\"test3,test4\"", result.Stdout);
            StringAssert.Contains("MyPlugin5 Include=\"test5,test6\" Exclude=\"test7,test8\"", result.Stdout);
            Assert.AreEqual(result.ExitCode, 0);

            Assert.True(File.Exists(OutputFile));
        }

        [Test]
        // Package not available on Linux
        [Platform(Exclude="Unix,Linux,MacOsX")]
        [Ignore("This requires OpenTAP to be installed as a package, which is an odd requirement for a unittest for the project")]
        public void VSSDKTest()
        {
            var csProj = new CsProj(this);
            csProj.ItemGroups.Add($@"
    <ItemGroup>
<OpenTapPackageReference Include=""Visual Studio SDK"" Version=""1.0.12+79946776"" IncludeAssemblies=""**""  ExcludeAssemblies=""**/*Install**;**stdole**""/> 
    </ItemGroup>
            ");
            var result = csProj.Build();

            StringAssert.Contains("Got 1 OpenTapPackageReference targets.", result.Stdout);
            StringAssert.Contains(@"Visual Studio SDK Include=""**"" Exclude=""**/*Install**,**stdole**""",
                result.Stdout);

            Assert.AreEqual(result.ExitCode, 0);

            Assert.True(File.Exists(OutputFile));
            var Generated = csProj.GetGeneratedFileContent();

            StringAssert.DoesNotContain("OpenTap.VSSdk.Installer.dll", Generated);
            StringAssert.DoesNotContain("stdole.dll", Generated);

            StringAssert.Contains("OpenTap.VSSdk.Debugger.dll", Generated);
            StringAssert.Contains("EnvDTE.dll", Generated);
        }
        [Test]
        // Package not available on Linux
        [Platform(Exclude="Unix,Linux,MacOsX")]
        public void OSIntegration()
        {
            var csProj = new CsProj(this);
            csProj.ItemGroups.Add($@"
    <ItemGroup>
<OpenTapPackageReference Include=""OSIntegration"" /> 
    </ItemGroup>
            ");
            csProj.PropertyGroups.Add($@"
    <PropertyGroup>
<PlatformTarget>x86</PlatformTarget> 
    </PropertyGroup>
            ");
            
            var result = csProj.Build();

            // Verify environment variable is set
            StringAssert.Contains("Skipping OS Integration (OPENTAP_DEBUG_INSTALL environment variable is set).", result.Stdout);
            Assert.AreEqual(0, result.ExitCode, result.Stdout);
            Assert.True(File.Exists(OutputFile));
        }

        [Test]
        public void TestPackageInstallFailedContainsLineNumber()
        {
            var csProj = new CsProj(this);
            csProj.PropertyGroups.Add($@"
<PropertyGroup>
<Version1>1.2.3</Version1>
</PropertyGroup>
");
            csProj.ItemGroups.Add($@"
<ItemGroup>
<AdditionalOpenTapPackage Include=""Bad Package"" Version=""$(Version1)"" />
</ItemGroup>
");
            var result = csProj.Build().Stdout;

            var index1 = csProj.GetLineNo($"(Version1)");

            StringAssert.Contains($"test.csproj({index1}): error OpenTAP Install: Unable to resolve package 'Bad Package'.", result);
        }
    }
}
