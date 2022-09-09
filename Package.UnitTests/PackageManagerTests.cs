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
using System.Net;
using OpenTap.Authentication;

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
            SessionLogs.Initialize(string.Format("Tap.Package.UnitTests {0}.TapLog", DateTime.Now.ToString("HH-mm-ss.fff")));
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

        [TestCase("file:///C:/Packages", "file:///C:/Packages", "Windows")]
        [TestCase("file:///C:/Packages/", "file:///C:/Packages", "Windows")]
        [TestCase("file:///Packages/", "file:///{Drive}Packages", "Windows")]
        [TestCase("file:///Packages", "file:///Packages", "Linux")]
        [TestCase("file:///Packages/", "file:///Packages", "Linux")]
        [TestCase("/Temp/MyFile2.txt", "file:///Temp", "Linux")]
        [TestCase("C:/Packages", "file:///C:/Packages", "Windows")]
        [TestCase("C:/Packages/", "file:///C:/Packages", "Windows")]
        [TestCase("/Packages", "file:///Packages", "Linux")]
        [TestCase("/Packages/", "file:///Packages", "Linux")]
        [TestCase("C:\\Packages/", "file:///C:/Packages", "Windows")]
        [TestCase("PackageCache", "file:///{CurrentDirectory}/PackageCache")]
        [TestCase("PackageCache/", "file:///{CurrentDirectory}/PackageCache")]
        public void FileRepositoryUrls(string input, string expectedUrl, string os = "Windows,Linux")
        {
            if (!os.Contains(OperatingSystem.Current.ToString()))
                return;

            try
            {
                expectedUrl = expectedUrl.Replace("{CurrentDirectory}", Directory.GetCurrentDirectory().Replace('\\', '/'));
                expectedUrl = expectedUrl.Replace("{Drive}", new DriveInfo(Directory.GetCurrentDirectory()).Name).Replace('\\', '/');
                if (input.Contains("MyFile")) { 
                    Directory.CreateDirectory(input);
                    File.Create(input).Dispose();
                }
                FilePackageRepository repository = new FilePackageRepository(input);
                Assert.AreEqual(expectedUrl, repository.Url);
            }
            finally
            {
                if (input.Contains("MyFile"))
                {
                    if (File.Exists(input))
                        File.Delete(input);
                }

            }
        }

        [Test]
        public void HttpRepositoryManagerTest()
        {
            var manager = new HttpPackageRepository("http://packages.opentap.io/");
            RepositoryManagerReceivePackageList(manager);
            TestDownload(manager);
        }

        [Test]
        public void TestUserId()
        {
            var idPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), "OpenTap", "OpenTapGeneratedId");
            string orgId = null;
            if (File.Exists(idPath))
                orgId = File.ReadAllText(idPath);

            try
            {
                // Check id are the same
                var id = HttpPackageRepository.GetUserId();
                var id2 = HttpPackageRepository.GetUserId();
                Assert.AreEqual(id, id2, "User id are different between runs.");

                // Remove id file
                File.Delete(idPath);
                if (File.Exists(idPath))
                    Assert.Fail("Id still exists.");
                Assert.AreNotEqual(HttpPackageRepository.GetUserId(), default(Guid), "Failed to create new user id after deleting file.");

                // Remove directory
                Directory.Delete(Path.GetDirectoryName(idPath), true);
                Assert.AreNotEqual(HttpPackageRepository.GetUserId(), default(Guid), "Failed to create new user id after deleting directory.");
            }
            finally
            {
                // Revert changes
                if (orgId != null)
                    File.WriteAllText(idPath, orgId);
            }
        }

        public static void RepositoryManagerReceivePackageList(IPackageRepository manager)
        {
            var tap = new PackageIdentifier("OpenTAP", SemanticVersion.Parse("7.4"), CpuArchitecture.Unspecified, "Windows");

            // ReceivePackageList.
            var allPackages = manager.GetPackages(new PackageSpecifier(os: "Windows"), tap);
            Assert.IsTrue(allPackages.Length > 0, "args(7.4)");

            #region GetPackages
            log.Info("GetPackages - STARTED");

            // Get packages running in developer mode.
            tap.Version = SemanticVersion.Parse("7.3.0-Development");
            var bag = manager.GetPackages(new PackageSpecifier(os: "Windows"), tap);
            Assert.IsTrue(bag.Length > 0, "args(7.3 Development)");
            log.Info("args(7.3 Development) - SUCCESS");

            // Get packages running in release mode without build version.
            tap.Version = PluginManager.GetOpenTapAssembly().SemanticVersion;
            bag = manager.GetPackages(new PackageSpecifier(os: "Windows"), tap);
            Assert.IsTrue(bag.Length > 0, "args(TapEngine)");
            log.Info("args(TapEngine) - SUCCESS");

            log.Info("GetPackages - COMPLETED");
            #endregion
        }

        public static void TestDownload(IPackageRepository manager)
        {
            var tap = new PackageIdentifier("OpenTAP", SemanticVersion.Parse("7.5.0-Development"), CpuArchitecture.Unspecified, "Windows");

            // Download package.
            var bag = manager.GetPackages(new PackageSpecifier(os: "Windows"), tap);
            Assert.IsTrue(bag.Length > 0, "args(7.5.0-Development)");
            var package = bag[0];
            var path = Path.Combine(Directory.GetCurrentDirectory(), package.Name + ".TapPackage");
            manager.DownloadPackage(package, path, new System.Threading.CancellationToken());
            Assert.IsTrue(File.Exists(path));
            log.Info("DownloadPackage - args() - SUCCESS");

            if (manager is FilePackageRepository)
            {
                // Download to same folder.
                manager.DownloadPackage(package, path, new System.Threading.CancellationToken());
                Assert.IsTrue(File.Exists((package.PackageSource as FileRepositoryPackageDefSource)?.PackageFilePath));
                log.Info("DownloadPackage - args(Same folder) - SUCCESS");
                (manager as FilePackageRepository).Reset();
            }

            // Download when file already exists.
            tap.Version = PluginManager.GetOpenTapAssembly().SemanticVersion;
            bag = manager.GetPackages(new PackageSpecifier(os: "Windows"), tap);
            Assert.IsTrue(bag.Length > 0, "args(TapEngine)");
            package = bag.ToArray()[0];
            manager.DownloadPackage(package, path, new System.Threading.CancellationToken());
            File.Delete(path);
        }
    }


    [TestFixture]
    public class RepositoryManagerTests
    {
        static TraceSource log =OpenTap.Log.CreateSource("Test");
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
                Assert.IsTrue(repo.GetPackages(new PackageSpecifier(os: "Windows")).Count() == 1, "Folder with one package");
                log.Info("Folder with one plugin - SUCCESS");

                // Folder with several plugins.
                Directory.GetFiles("TapPackages").ToList().ForEach(f => File.Copy(f, Path.Combine(tempFolder, Path.GetFileName(f)), true));
                repo.Reset();
                var anyVersion = new PackageSpecifier(os: "Windows");
                Assert.AreEqual(9, repo.GetPackages(anyVersion).Count(), "Folder with several packages");
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
        static TraceSource log =OpenTap.Log.CreateSource("Test");
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
                packages.AddRange(PackageDef.ManyFromXml(xmlText));
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
                var pkgs = PackageDef.ManyFromXml(xmlText);
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
                packages.AddRange(PackageDef.ManyFromXml(xmlText));
            using (var xmlText = File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(), "TapPackages/MyPlugin2.xml")))
                packages.AddRange(PackageDef.ManyFromXml(xmlText));
            using (var xmlText = File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(), "TapPackages/MyPlugin1.xml")))
                packages.AddRange(PackageDef.ManyFromXml(xmlText));

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
                packages.AddRange(PackageDef.ManyFromXml(xmlText));

                // Multiple identical packages.
                xmlText.Seek(0, 0);
                packages.AddRange(PackageDef.ManyFromXml(xmlText));
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
            Installation installation = new Installation(Directory.GetCurrentDirectory());
            var issues = DependencyChecker.CheckDependencies(installation, new string[] { MyPlugin1, MyPlugin2 });
            Log.RemoveListener(listener);
            Assert.IsTrue(issues == DependencyChecker.Issue.None, errors);
            log.Info("No dependencies - SUCCESS");

            // Dependency on plugin
            issues = DependencyChecker.CheckDependencies(installation, new string[] { MyPlugin1, MyPlugin2, MyPlugin3 });
            Assert.IsTrue(issues == DependencyChecker.Issue.BrokenPackages, "Dependency on plugin");
            log.Info("Dependency on plugin - SUCCESS");
        }

        [Test]
        public void TestDependencyPatchVersion()
        {
            // Verify that the resolver bumps from installed "9.12.0" because "^9.12.1" is required
            var repo = "http://packages.opentap.io";

            var installedOpenTap = new PackageDef() {Version = SemanticVersion.Parse("9.12.0"), Name = "OpenTAP", Architecture = CpuArchitecture.x64 };

            var toInstall = new PackageDef()
            {
                Name = "MockPackage",
                Dependencies = new List<PackageDependency>()
                    {new PackageDependency("OpenTAP", VersionSpecifier.Parse("^9.12.1"), "^9.12.1")},
                Architecture = CpuArchitecture.x64
            };

            var installedPackages = new Dictionary<string, PackageDef> {{"OpenTAP", installedOpenTap}};
            var packages = new[] {toInstall};
            var repositories = new List<IPackageRepository>() {new HttpPackageRepository(repo)};

            var resolver = new DependencyResolver(installedPackages, packages, repositories);

            Assert.AreEqual(2, resolver.MissingDependencies.Count);
            var missing = resolver.MissingDependencies.FirstOrDefault(p => p.Name == "OpenTAP");

            Assert.AreEqual("OpenTAP", missing.Name);
            Assert.AreEqual(9, missing.Version.Major);
            Assert.AreEqual(12, missing.Version.Minor);
            Assert.AreEqual(1, missing.Version.Patch);
        }

        [Test]
        public void TestDependencyNoUpgrade()
        {
            // Verify that the resolver does not try to upgrade installed "9.12.0"
            var repo = "https://packages.opentap.io";

            var installedOpenTap = new PackageDef() { Version = SemanticVersion.Parse("9.12.0"), Name = "OpenTAP" };

            var toInstall = new PackageDef()
            {
                Name = "MockPackage",
                Dependencies = new List<PackageDependency>()
                    {new PackageDependency("OpenTAP", VersionSpecifier.Parse("^9.12"), "^9.12")}
            };

            var installedPackages = new Dictionary<string, PackageDef> {{"OpenTAP", installedOpenTap}};
            var packages = new[] {toInstall};
            var repositories = new List<IPackageRepository>() {new HttpPackageRepository(repo)};

            var resolver = new DependencyResolver(installedPackages, packages, repositories);

            Assert.AreEqual(1, resolver.MissingDependencies.Count);
            Assert.AreEqual("MockPackage", resolver.MissingDependencies[0].Name);
        }
    }

    [TestFixture]
    public class PackageManagerTests
    {
        static TraceSource log = Log.CreateSource("Test");
        [Test]
        public void PackageManagerTestsExecution()
        {
            if(Directory.Exists("DownloadedPackages"))
                Directory.Delete("DownloadedPackages", true);

            // Test execution
            var tap = new PackageIdentifier("OpenTAP", SemanticVersion.Parse("9.0.0-Development"), CpuArchitecture.Unspecified, "Windows");

            // Development, don't check build version
            var packages = get(tap);
            // MyPlugin1 requires OpenTAP ^9.0.605 which is not compatible with 9.0.0 
            Assert.IsFalse(packages.Any(p => p.Name == "MyPlugin1"));
            Assert.IsTrue(packages.Any(p => p.Name == "MyPlugin2"));
            Assert.IsTrue(packages.Any(p => p.Name == "MyPlugin3"));

            // Release, TAP is incompatible
            tap.Version = SemanticVersion.Parse("8.0.0");
            packages = get(tap);
            Assert.IsFalse(packages.Any(p => p.Name == "MyPlugin1"));
            Assert.IsFalse(packages.Any(p => p.Name == "MyPlugin2"));
            Assert.IsTrue(packages.Any(p => p.Name == "MyPlugin3"), "GetPackages - Release, TAP is incompatible");

            // Release, OpenTAP is compatible
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
            // MyPlugin1 requires patch 605
            Assert.IsFalse(packages.Any(p => p.Name == "MyPlugin1"));
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
            var packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(
                PackageManagerSettings.Current.Repositories.Where(p => p.IsEnabled).Select(s => s.Manager).Where(p => p is FilePackageRepository).ToList(),
                new PackageSpecifier(os: "Windows"), compatibleWith);

            return packages;
        }

        [TestCase("", "http://packages.opentap.io", typeof(HttpPackageRepository))]  // Http scheme
        [TestCase("", "https://packages.opentap.io", typeof(HttpPackageRepository))] // Https scheme
        [TestCase("", "ftp://packages.opentap.io", typeof(NotSupportedException))]   // Unsupported scheme
        [TestCase("", "something illigal|", typeof(ArgumentException))]              // Illigal chars
        [TestCase("", "C:/a", typeof(FilePackageRepository))]                        // Windows absolute path
        [TestCase("", "/a/b", typeof(FilePackageRepository))]                        // Linux absolute path
        [TestCase("", "a/b", typeof(FilePackageRepository))]                         // Relative path
        [TestCase("", @"a\b", typeof(FilePackageRepository))]                        // Relative file path using backslash
        [TestCase("", "file:///./a", typeof(FilePackageRepository))]                 // Explicit file scheme
        [TestCase("", "file:///a", typeof(FilePackageRepository))]                   // Explicit file scheme
        [TestCase("", "file:///C:/a", typeof(FilePackageRepository))]                // Explicit absolute File Path
        [TestCase("", @"\\a\b", typeof(FilePackageRepository))]                      // UNC path
        [TestCase("http://opentap.io", "http://packages.opentap.io", typeof(HttpPackageRepository))]  // Http scheme
        [TestCase("http://opentap.io", "https://packages.opentap.io", typeof(HttpPackageRepository))] // Https scheme
        [TestCase("http://opentap.io", "C:/a", typeof(FilePackageRepository))]                        // Windows absolute path
        [TestCase("http://opentap.io", "/a/b", typeof(HttpPackageRepository))]                        // Linux absolute path
        [TestCase("http://opentap.io", "a/b", typeof(HttpPackageRepository))]                         // Relative path
        [TestCase("http://opentap.io", @"a\b", typeof(HttpPackageRepository))]                        // Relative file path using backslash
        [TestCase("http://opentap.io", "file:///./a", typeof(FilePackageRepository))]                 // Explicit file scheme
        [TestCase("http://opentap.io", "file:///a", typeof(FilePackageRepository))]                   // Explicit file scheme
        [TestCase("http://opentap.io", "file:///C:/a", typeof(FilePackageRepository))]                // Explicit absolute File Path
        [TestCase("http://opentap.io", @"\\a\b", typeof(FilePackageRepository))]                      // UNC path
        //[TestCase("http://opentap.io", @"//a/b", typeof(FilePackageRepository))]                      // UNC path using forward slash (but also scheme relative URL)
        public void TestRepositoryType(string baseUrl, string url, Type expectedRepositoryType)
        {
            AuthenticationSettings.Current.BaseAddress = baseUrl;
            try
            {
                var result = PackageRepositoryHelpers.DetermineRepositoryType(url);
                if (expectedRepositoryType.DescendsTo(typeof(Exception)))
                    Assert.Fail($"Should have thrown {expectedRepositoryType.ToString()} but returned {result.GetType()}");
                Assert.AreEqual(expectedRepositoryType, result.GetType());
            }
            catch (Exception ex)
            {
                if (expectedRepositoryType == ex.GetType())
                    return;
                else
                    throw;
            }
        }


        [TestCase("http://localhost", "/api/results", "http://localhost/api/results")]
        [TestCase("http://localhost", "http://opentap.io/api/results", "http://opentap.io/api/results")]
        [TestCase("http://localhost", "https://opentap.io/api/results", "https://opentap.io/api/results")]
        [TestCase("http://localhost/hey", "/api/results", "http://localhost/api/results")]
        [TestCase("http://localhost/hey/", "api/results", "http://localhost/hey/api/results")]
        public void TestGetClient(string baseAddress, string getClientAddress, string resultingBaseAddress)
        {
            AuthenticationSettings.Current.BaseAddress = baseAddress;
            var httpClient = AuthenticationSettings.Current.GetClient(baseAddress: getClientAddress);
            Assert.AreEqual(resultingBaseAddress, httpClient.BaseAddress.ToString());
        }

        [Test]
        [Ignore("For manual debugging")]
        public void TestIfHttpPackageRepositorySupportsRelativePaths()
        {
            AuthenticationSettings.Current.BaseAddress = null;
            HttpPackageRepository httpPackageRepository = new HttpPackageRepository("packages.opentap.io");
            var packages = httpPackageRepository.GetPackageNames();
            Assert.AreEqual($"http://packages.opentap.io", httpPackageRepository.Url); // Url was changed to valid url
            Assert.IsTrue(packages.Any());
            var versions = httpPackageRepository.GetPackageVersions(packages.FirstOrDefault());
            Assert.IsTrue(versions.Any());
            string query = "query Query {packages(class:\"package\", version:\"any\"){ name version description dependencies{ name version} }}";
            var resp = httpPackageRepository.QueryGraphQL(query);
            Assert.IsNotNull(resp);
            string file = "C:/Temp/Test.TapPackage";
            try
            {
                var p = versions.FirstOrDefault();
                httpPackageRepository.DownloadPackage(new PackageIdentifier(p.Name, p.Version, p.Architecture, p.OS), file);
                Assert.IsTrue(File.Exists(file));
            }
            finally
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
        }

        [Test]
        [Ignore("For manual debugging")]
        public void TestIfHttpPackageRepositorySupportsRelativePaths2()
        {
            AuthenticationSettings.Current.BaseAddress = null;
            HttpPackageRepository httpPackageRepository = new HttpPackageRepository("packages.opentap.io");
            string file = "C:/Temp/Test.TapPackage";
            try
            {
                httpPackageRepository.DownloadPackage(new PackageIdentifier("OpenTAP", "9.18.4", CpuArchitecture.x64, "Linux,MacOS"), file);
                Assert.IsTrue(File.Exists(file));
            }
            finally
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
        }
    }
}