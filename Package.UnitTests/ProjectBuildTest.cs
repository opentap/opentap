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
        public List<string> PropertyGroups { get; set; }
        public List<string> ItemGroups { get; set; }
        public List<string> Imports { get; set; }

        public override string ToString()
        {

            return @"<Project Sdk=""Microsoft.NET.Sdk"" ToolsVersion=""Current"">" + '\n' +
                   string.Join("\n", PropertyGroups) + '\n' +
                   string.Join("\n", ItemGroups) + '\n' +
                   string.Join("\n", Imports) + '\n' +
                   "</Project>\n";
        }

        public CsProj()
        {
            PropertyGroups = new List<string>()
            {
                @"                   
    <PropertyGroup>
        <TargetFrameworkIdentifier></TargetFrameworkIdentifier>
        <TargetFrameworkVersion></TargetFrameworkVersion>
        <TargetFramework>netstandard2.0</TargetFramework>
    </PropertyGroup>"
            };
            ItemGroups = new List<string>();
            
            Imports = new List<string>()
            {
                @"
<Import Project=""..\OpenTap.targets"" />"
            };
        }
    }

    [TestFixture]
    public class ProjectBuildTest
    {
        // Current directory is assumed to be the OutputFolder containing tap.exe etc.
        private string WorkingDirectory => Path.Combine(Directory.GetCurrentDirectory(), "buildTestDir");

        private static string FileRepository => Path.Combine(Directory.GetCurrentDirectory(), "TapPackages");
        private string OutputFile => Path.Combine(WorkingDirectory, "obj", "test.opentap.g.props");
        private string Filename => Path.Combine(WorkingDirectory, "test.csproj");

        public ProjectBuildTest()
        {
            if (Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "OpenTap.targets").Any() == false)
                throw new Exception(
                    $"OpenTap.targets not found in {Directory.GetCurrentDirectory()}. Tests cannot continue.");
        }

        private void Cleanup()
        {
            if (new DirectoryInfo(WorkingDirectory).Exists)
                Directory.Delete(WorkingDirectory, true);
            Directory.CreateDirectory(WorkingDirectory);
        }

        private (string Stdout, string Stderr, int ExitCode) Build()
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = "dotnet",
                    Arguments = $"build {Filename} --verbosity normal",
                    WorkingDirectory = WorkingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(1000);

            return (process.StandardOutput.ReadToEnd(), process.StandardError.ReadToEnd(), process.ExitCode);
        }

        private string GetGeneratedFileContent()
        {
            using (var reader = new StreamReader(OutputFile))
            {
                return reader.ReadToEnd();
            }
        }

        [Test]
        public void NoReferencesTest()
        {
            Cleanup();
            var csProj = new CsProj();
            using (var writer = new StreamWriter(Filename))
                writer.Write(csProj.ToString());

            var result = Build();

            StringAssert.Contains("Got 0 OpenTapPackageReference targets.", result.Stdout);
            Assert.AreEqual(result.ExitCode, 0);
            Assert.False(File.Exists(OutputFile));
        }

        [Test]
        public void OneReferenceDefaultTest()
        {
            Cleanup();
            var csProj = new CsProj();
            csProj.ItemGroups.Add($@"
   <ItemGroup>
	  <OpenTapPackageReference Repository=""{FileRepository}"" Include=""MyPlugin1"" />	  
  </ItemGroup>
");
            using (var writer = new StreamWriter(Filename))
                writer.Write(csProj.ToString());

            var result = Build();

            StringAssert.Contains("Got 1 OpenTapPackageReference targets.", result.Stdout);
            StringAssert.Contains("MyPlugin1 Include=\"**\" Exclude=\"Dependencies/**\"", result.Stdout);
            Assert.AreEqual(result.ExitCode, 0);
            
            Assert.True(File.Exists(OutputFile));
            var Generated = GetGeneratedFileContent();
            StringAssert.Contains("MyPlugin1.dll", Generated);
        }

        [Test]
        public void TwoEqualReferencesDefaultTest()
        {
            Cleanup();
            var csProj = new CsProj();
            csProj.ItemGroups.Add($@"
   <ItemGroup>
	  <OpenTapPackageReference Repository=""{FileRepository}"" Include=""MyPlugin1"" />	  
  </ItemGroup>
");
            
            csProj.ItemGroups.Add($@"
   <ItemGroup>
	  <OpenTapPackageReference Repository=""{FileRepository}"" Include=""MyPlugin2"" />	  
  </ItemGroup>
");
            
            using (var writer = new StreamWriter(Filename))
                writer.Write(csProj.ToString());

            var result = Build();

            StringAssert.Contains("Got 2 OpenTapPackageReference targets.", result.Stdout);
            StringAssert.Contains("MyPlugin1 Include=\"**\" Exclude=\"Dependencies/**\"", result.Stdout);
            StringAssert.Contains("MyPlugin2 Include=\"**\" Exclude=\"Dependencies/**\"", result.Stdout);
            Assert.AreEqual(result.ExitCode, 0);
            
            Assert.True(File.Exists(OutputFile));
            var Generated = GetGeneratedFileContent();
            StringAssert.Contains( "MyPlugin1.dll", Generated);
        }

        [Test]
        public void TwoEqualReferencesSpecifiedTest()
        {
            Cleanup();
            var csProj = new CsProj();
            csProj.ItemGroups.Add($@"
   <ItemGroup>
	  <OpenTapPackageReference Repository=""{FileRepository}"" Include=""MyPlugin1"" IncludeAssemblies=""test1;test2"" ExcludeAssemblies=""test3;test4"" />	  
  </ItemGroup>
");
            
            csProj.ItemGroups.Add($@"
   <ItemGroup>
	  <OpenTapPackageReference Repository=""{FileRepository}"" Include=""MyPlugin2"" IncludeAssemblies=""test1;test2"" ExcludeAssemblies=""test3;test4"" />	  
  </ItemGroup>
");

            using (var writer = new StreamWriter(Filename))
                writer.Write(csProj.ToString());

            var result = Build();

            StringAssert.Contains("Got 2 OpenTapPackageReference targets.", result.Stdout);
            StringAssert.Contains("MyPlugin1 Include=\"test1,test2\" Exclude=\"test3,test4\"", result.Stdout);
            StringAssert.Contains("MyPlugin2 Include=\"test1,test2\" Exclude=\"test3,test4\"", result.Stdout);
            Assert.AreEqual(result.ExitCode, 0);
            
            Assert.False(File.Exists(OutputFile));
        }

        [Test]
        public void TwoDifferentReferencesSpecifiedTest()
        {
            Cleanup();
            var csProj = new CsProj();
            csProj.ItemGroups.Add($@"
    <ItemGroup>
      <OpenTapPackageReference Include=""MyPlugin1"">    
        <Repository>{FileRepository}</Repository> 
        <IncludeAssemblies>test1;test2</IncludeAssemblies> 
        <ExcludeAssemblies>test3;test4</ExcludeAssemblies>
      </OpenTapPackageReference>
    </ItemGroup>
");
            csProj.ItemGroups.Add($@"
   <ItemGroup>
	  <OpenTapPackageReference Repository=""{FileRepository}"" Include=""MyPlugin2""> 
        <IncludeAssemblies>test5;test6</IncludeAssemblies> 
        <ExcludeAssemblies>test7;test8</ExcludeAssemblies>
      </OpenTapPackageReference>
  </ItemGroup>
");

            using (var writer = new StreamWriter(Filename))
                writer.Write(csProj.ToString());

            var result = Build();

            StringAssert.Contains("Got 2 OpenTapPackageReference targets.", result.Stdout);
            StringAssert.Contains("MyPlugin1 Include=\"test1,test2\" Exclude=\"test3,test4\"", result.Stdout);
            StringAssert.Contains("MyPlugin2 Include=\"test5,test6\" Exclude=\"test7,test8\"", result.Stdout);
            Assert.AreEqual(result.ExitCode, 0);
            
            Assert.False(File.Exists(OutputFile));
        }
    }
}