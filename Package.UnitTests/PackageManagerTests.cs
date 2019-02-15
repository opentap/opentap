//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using NUnit.Framework;
using OpenTap.Package;
using System.Collections.Concurrent;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using OpenTap;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;
using System.Diagnostics;

namespace OpenTap.Package.UnitTests
{
    [SetUpFixture]
    public class TapTestInit
    {
        [OneTimeSetUp]
        public static void AssemblyInit()
        {
            EngineSettings.LoadWorkingDirectory(System.IO.Path.GetDirectoryName(typeof(TestStep).Assembly.Location));
            PluginManager.SearchAsync().Wait();
            // this should put the log file just outside the project folder
            SessionLogs.Initialize(string.Format("..\\..\\..\\..\\Tap.PackageManager.UnitTests {0}.TapLog", DateTime.Now.ToString("HH-mm-ss.fff")));
        }

        [OneTimeTearDown]
        public static void AssemblyCleanup()
        {
            SessionLogs.Flush();
        }
    }

    [TestFixture]
    public class RepositoryManagerTest
    {
        static TraceSource log =  OpenTap.Log.CreateSource("Test");
        [Test]
        public void FileRepositoryManagerTest()
        {
            var manager = new FilePackageRepository(Path.Combine(Directory.GetCurrentDirectory(), "TapPackages"));
            RepositoryManagerReceivePackageList(manager);
            TestDownload(manager);
        }

        [Test]
        public void HttpRepositoryManagerTest()
        {
            var manager = new HttpPackageRepository("http://packages.opentap.keysight.com/");
            RepositoryManagerReceivePackageList(manager);
            TestDownload(manager);
        }
        
        public static void RepositoryManagerReceivePackageList(IPackageRepository manager)
        {
            var tap = new PackageIdentifier("OpenTAP", SemanticVersion.Parse("7.4"), CpuArchitecture.Unknown, "Windows");
            
            // ReceivePackageList.
            var allPackages = manager.GetPackages(new PackageSpecifier(), tap);
            Assert.IsTrue(allPackages.Length > 0);
            
            #region GetPackages
            log.Info("GetPackages - STARTED");

            // Get packages running in developer mode.
            tap.Version = SemanticVersion.Parse("7.3.0-Development");
            var bag = manager.GetPackages(new PackageSpecifier(), tap);
            Assert.IsTrue(bag.Length > 0, "args(7.3 Development)");
            log.Info("args(7.3 Development) - SUCCESS");

            // Get packages running in release mode without build version.
            tap.Version = PluginManager.GetOpenTapAssembly().SemanticVersion;
            bag = manager.GetPackages(new PackageSpecifier(), tap);
            Assert.IsTrue(bag.Length > 0, "args(TapEngine)");
            log.Info("args(TapEngine) - SUCCESS");

            log.Info("GetPackages - COMPLETED");
            #endregion
        }

        public static void TestDownload(IPackageRepository manager)
        {
            var tap = new PackageIdentifier("OpenTAP", SemanticVersion.Parse("7.5.0-Development"), CpuArchitecture.Unknown, "Windows");
            
            // Download package.
            var bag = manager.GetPackages(new PackageSpecifier(), tap);
            var package = bag[0];
            var path = Path.Combine(Directory.GetCurrentDirectory(), package.Name + ".TapPackage");
            manager.DownloadPackage(package, path, new System.Threading.CancellationToken());
            Assert.IsTrue(File.Exists(path));
            log.Info("DownloadPackage - args() - SUCCESS");

            if (manager is FilePackageRepository)
            {
                // Download to same folder.
                manager.DownloadPackage(package, path, new System.Threading.CancellationToken());
                Assert.IsTrue(File.Exists(package.Location));
                log.Info("DownloadPackage - args(Same folder) - SUCCESS");
                (manager as FilePackageRepository).Reset();
            }

            // Download when file already exists.
            tap.Version = PluginManager.GetOpenTapAssembly().SemanticVersion;
            bag = manager.GetPackages(new PackageSpecifier(), tap);
            package = bag.ToArray()[0];
            manager.DownloadPackage(package, path, new System.Threading.CancellationToken());
            File.Delete(path);
        }
    }


    [TestFixture]
    public class RepositoryManagerTests 
    {
        static TraceSource log =  OpenTap.Log.CreateSource("Test");
        [Test]
        public void RepositoryManagerLoadAllPackagesTest()
        {
            log.Info("-----------------------------RepositoryManager LoadAllPackages-----------------------------");
            var tempFolder = "RepositoryManagerTestTempFolder";
            
            try
            {
                // Empty folder.
                Directory.CreateDirectory(tempFolder);
                var repo = new FilePackageRepository(tempFolder);
                Assert.IsTrue(repo.GetPackages(new PackageSpecifier()).Any() == false, "Empty");
                log.Info("Empty folder - SUCCESS");
    
                // Folder with one plugin.
                Directory.CreateDirectory("TapPackage");
                File.Copy("TapPackages/MyPlugin1.TapPackage", tempFolder + "/MyPlugin1.TapPackage",true);
                repo.Reset();
                Assert.IsTrue(repo.GetPackages(new PackageSpecifier()).Count() == 1, "Folder with one package");
                log.Info("Folder with one plugin - SUCCESS");
    
                // Folder with several plugins.
                Directory.GetFiles("TapPackages").ToList().ForEach(f => File.Copy(f, Path.Combine(tempFolder, Path.GetFileName(f)), true));
                repo.Reset();
                var anyVersion = new PackageSpecifier();
                Assert.AreEqual(7, repo.GetPackages(anyVersion).Count(), "Folder with several packages");
                log.Info("Folder with several plugin - SUCCESS");
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
            }
        }
    }

    [TestFixture]
    public class DependencyAnalyzerTests
    {
        static TraceSource log =  OpenTap.Log.CreateSource("Test");
        [Test]
        public void DependencyAnalyzerGetIssuesTest()
        {
            log.Info("-----------------------------DependencyAnalyzer GetIssues-----------------------------");

            // Test setup.
            var tapPackage = new PackageDef() { Name = "Tap", Version = SemanticVersion.Parse("7.0.700") };
            var packages = new List<PackageDef>();
            packages.Add(tapPackage);

            // Dependencies without any issues.
            using (var xmlText = File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(), "TapPackages/MyPlugin1.xml")))
                packages.AddRange(PackageDef.LoadManyFrom(xmlText));
            var dependencyAnalyzer = DependencyAnalyzer.BuildAnalyzerContext(packages);
            var issues = dependencyAnalyzer.GetIssues(packages.Last());
            if (issues.Any())
            {
                Assert.Fail("Unexpected Dependency Issue: {1} {0} ", issues.First().IssueType, issues.First().PackageName);
            }
            log.Info("Dependencies without any issues - SUCCESS");

            // Reset test.
            packages = packages.Take(1).ToList();

            // Dependencies with issues (Tap newer than plugin).
            using (var xmlText = File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(), "TapPackages/MyPlugin2.xml")))
            {
                var pkgs = PackageDef.LoadManyFrom(xmlText);
                packages.AddRange(pkgs);
                Assert.IsTrue(pkgs.First().Version.Major == 1);
                Assert.IsTrue(pkgs.First().Version.Minor == 2);
                Assert.IsTrue(pkgs.First().Version.Patch == 3);
            }
            dependencyAnalyzer = DependencyAnalyzer.BuildAnalyzerContext(packages);
            issues = dependencyAnalyzer.GetIssues(packages.Last());
            Assert.IsTrue(issues.Count == 1, "Dependencies with issues (Tap newer than plugin)");
            log.Info("Dependencies with issues (Tap newer than plugin) - SUCCESS");
            
            // Reset test.
            packages = packages.Take(1).ToList();

            // No dependencies.
            dependencyAnalyzer = DependencyAnalyzer.BuildAnalyzerContext(packages);
            issues = dependencyAnalyzer.GetIssues(new PackageDef());
            Assert.IsTrue(issues.Count == 0, "No dependencies");
            log.Info("No dependencies - SUCCESS");
        }

        [Test]
        public void DependencyAnalyzerFilterRelatedTest()
        {
            log.Info("-----------------------------DependencyAnalyzer FilterRelated-----------------------------");

            // Test setup.
            var tapPackage = new PackageDef() { Name = "Tap", Version = SemanticVersion.Parse("9.0") };// TapVersion.GetTapEngineVersion().ToString() };
            var packages = new List<PackageDef>();
            packages.Add(tapPackage);
            using (var xmlText = File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(), "TapPackages/MyPlugin3.xml")))
                packages.AddRange(PackageDef.LoadManyFrom(xmlText));
            using (var xmlText = File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(), "TapPackages/MyPlugin2.xml")))
                packages.AddRange(PackageDef.LoadManyFrom(xmlText));
            using (var xmlText = File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(), "TapPackages/MyPlugin1.xml")))
                packages.AddRange(PackageDef.LoadManyFrom(xmlText));

            // No dependencies
            var dependencyAnalyzer = DependencyAnalyzer.BuildAnalyzerContext(packages.Take(2).ToList());
            var issues = dependencyAnalyzer.FilterRelated(packages.Take(2).ToList());
            Assert.IsTrue(issues.BrokenPackages.Count == 0, "No dependencies");
            log.Info("No dependencies - SUCCESS");

            // Tap dependency
            dependencyAnalyzer = DependencyAnalyzer.BuildAnalyzerContext(packages.Take(3).ToList());
            issues = dependencyAnalyzer.FilterRelated(packages.Take(3).ToList());
            Assert.IsTrue(issues.BrokenPackages.Count == 1, "Tap dependency");
            log.Info("Tap dependency - SUCCESS");

            // Several Tap dependencies
            dependencyAnalyzer = DependencyAnalyzer.BuildAnalyzerContext(packages.ToList());
            issues = dependencyAnalyzer.FilterRelated(packages.ToList());
            Assert.IsTrue(issues.BrokenPackages.Count == 2, "Several Tap dependencies");
            log.Info("Several Tap dependencies - SUCCESS");
        }

        [Test]
        public void DependencyAnalyzerBuildAnalyzerContextTest()
        {
            log.Info("-----------------------------DependencyAnalyzer BuildAnalyzerContext-----------------------------");

            // Test setup.
            var tapPackage = new PackageDef() { Name = "Tap", Version = PluginManager.GetOpenTapAssembly().SemanticVersion };
            var packages = new List<PackageDef>();
            packages.Add(tapPackage);

            // Correct packages.
            using (var xmlText = File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(), "TapPackages/MyPlugin1.xml")))
            {
                packages.AddRange(PackageDef.LoadManyFrom(xmlText));

                // Multiple identical packages.
                xmlText.Seek(0, 0);
                packages.AddRange(PackageDef.LoadManyFrom(xmlText));
            }
            
            // Reset test.
            packages.Clear();

            // No packages.
            var dependencyAnalyzer = DependencyAnalyzer.BuildAnalyzerContext(packages);
            Assert.IsTrue(dependencyAnalyzer.BrokenPackages.Count == 0, "No packages");
            log.Info("Multiple identical items - SUCCESS");
        }
    }

    [TestFixture]
    public class DependencyCheckerTests
    {
        static TraceSource log =  OpenTap.Log.CreateSource("Test");

        [Test]
        public void DependencyCheckerCheckDependenciesTest()
        {
            log.Info("-----------------------------DependencyChecker CheckDependencies-----------------------------");

            // Test setup
            string MyPlugin1 = Path.Combine(Directory.GetCurrentDirectory(), "TapPackages/MyPlugin1.TapPackage");
            string MyPlugin2 = Path.Combine(Directory.GetCurrentDirectory(), "TapPackages/MyPlugin2.1.2.37-alpha.715+164e6f81.TapPackage");
            string MyPlugin3 = Path.Combine(Directory.GetCurrentDirectory(), "TapPackages/MyPlugin3.TapPackage");

            // No dependencies
            EventTraceListener listener = new EventTraceListener();
            string errors = "";
            listener.MessageLogged += events => errors += Environment.NewLine + String.Join(Environment.NewLine, events.Select(m => m.Message));
            Log.AddListener(listener);
            var issues = DependencyChecker.CheckDependencies(new string[] { MyPlugin1, MyPlugin2 });
            Log.RemoveListener(listener);
            Assert.IsTrue(issues == DependencyChecker.Issue.None, errors);
            log.Info("No dependencies - SUCCESS");

            // Dependency on plugin
            issues = DependencyChecker.CheckDependencies(new string[] { MyPlugin1, MyPlugin2, MyPlugin3 });
            Assert.IsTrue(issues == DependencyChecker.Issue.BrokenPackages, "Dependency on plugin");
            log.Info("Dependency on plugin - SUCCESS");
        }
    }

    [TestFixture]
    public class PackageManagerTests
    {
        static TraceSource log = Log.CreateSource("Test");
        [Test]
        public void PackageManagerTestsExecution()
        {
            log.Info("-----------------------------DependencyChecker CheckDependencies-----------------------------");

            if(Directory.Exists("DownloadedPackages"))
                Directory.Delete("DownloadedPackages", true);

            // Test execution
            GetPackages();
            DetermineRepositoryType();
        }

        void GetPackages()
        {
            var tap = new PackageIdentifier("OpenTAP", SemanticVersion.Parse("9.0.0-Development"), CpuArchitecture.Unknown, "Windows");
            
            // Development, don't check build version
            var packages = get(tap);
            Assert.IsTrue(packages.Any(p => p.Name == "MyPlugin1"));
            Assert.IsTrue(packages.Any(p => p.Name == "MyPlugin2"));
            Assert.IsTrue(packages.Any(p => p.Name == "MyPlugin3"));

            // Release, TAP is incompatible
            tap.Version = SemanticVersion.Parse("8.0.0");
            packages = get(tap);
            Assert.IsFalse(packages.Any(p => p.Name == "MyPlugin1"));
            Assert.IsFalse(packages.Any(p => p.Name == "MyPlugin2"));
            Assert.IsTrue(packages.Any(p => p.Name == "MyPlugin3"), "GetPackages - Release, TAP is incompatible");

            // Release, TAP is compatible
            tap.Version = SemanticVersion.Parse("9.0.800");
            packages = get(tap);
            Assert.IsTrue(packages.Any(p => p.Name == "MyPlugin1"));
            Assert.IsTrue(packages.Any(p => p.Name == "MyPlugin2"));
            Assert.IsTrue(packages.Any(p => p.Name == "MyPlugin3"));

            // Release, same build
            tap.Version = SemanticVersion.Parse("9.0.605");
            packages = get(tap);
            Assert.IsTrue(packages.Any(p => p.Name == "MyPlugin1"));
            Assert.IsTrue(packages.Any(p => p.Name == "MyPlugin2"));
            Assert.IsTrue(packages.Any(p => p.Name == "MyPlugin3"));

            // Release, 1 build behind
            tap.Version = SemanticVersion.Parse("9.0.604");
            packages = get(tap);
            Assert.IsTrue(packages.Any(p => p.Name == "MyPlugin1"));
            Assert.IsTrue(packages.Any(p => p.Name == "MyPlugin2"));
            Assert.IsTrue(packages.Any(p => p.Name == "MyPlugin3"), "GetPackages - Release, 1 build behind");

            // No build
            tap.Version = new SemanticVersion(0,0,0,null,null);
            packages = get(tap);
            Assert.IsFalse(packages.Any(p => p.Name == "MyPlugin1"));
            Assert.IsFalse(packages.Any(p => p.Name == "MyPlugin2"));
            Assert.IsTrue(packages.Any(p => p.Name == "MyPlugin3"), "GetPackages - No build");
        }
        
        List<PackageDef> get(IPackageIdentifier compatibleWith)
        {
            var packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(new PackageSpecifier(),compatibleWith);

            return packages;
        }

        void DetermineRepositoryType()
        {
            #region Path
            // Correct path
            var result = PackageRepositoryHelpers.DetermineRepositoryType(Path.Combine(Directory.GetCurrentDirectory(), "TapPackages"));
            Assert.IsTrue(result is FilePackageRepository, "DetermineRepositoryType - Correct path");

            // Correct network path
            result = PackageRepositoryHelpers.DetermineRepositoryType(@"\\Tap\Tap.PackageManager.UnitTests\TapPlugins");
            Assert.IsTrue(result is FilePackageRepository, "DetermineRepositoryType - Correct network path");

            // Incorrect path
            result = PackageRepositoryHelpers.DetermineRepositoryType(@"Tap\Tap.PackageManager.UnitTests\TapPlugins");
            Assert.IsFalse(result is FilePackageRepository, "DetermineRepositoryType - Incorrect path");

            // Path that does not exists
            result = PackageRepositoryHelpers.DetermineRepositoryType(@"C:\Users\steholst\Source\Repos\tap2\TapThatDoesNotExists");
            Assert.IsTrue(result is FilePackageRepository, "DetermineRepositoryType - Path that does not exists");

            // Illegal path
            result = PackageRepositoryHelpers.DetermineRepositoryType(@"C:\Users\steholst\Source\Repos\tap2\Tap?");
            Assert.IsTrue(result is FilePackageRepository, "DetermineRepositoryType - Illegal path");

            // Path with incorrect syntax
            result = PackageRepositoryHelpers.DetermineRepositoryType(@"C::\Users\steholst\Source\Repos\tap2\TapThatDoesNotExists");
            Assert.IsFalse(result is FilePackageRepository);
            #endregion

            #region HTTP
            // Correct http address
            result = PackageRepositoryHelpers.DetermineRepositoryType(@"http://plugindemoapi.azurewebsites.net/");
            Assert.IsTrue(result is HttpPackageRepository, "DetermineRepositoryType - Correct http address");

            // Correct https address
            result = PackageRepositoryHelpers.DetermineRepositoryType(@"https://plugindemoapi.azurewebsites.net/");
            Assert.IsTrue(result is HttpPackageRepository, "DetermineRepositoryType - Correct https address");
            #endregion
        }
    }
}
