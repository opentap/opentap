using System.IO;
using System.Linq;
using System.Xml;
using NUnit.Framework;
using OpenTap.Package;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    [Display("Some test step")]
    public class MyTestStep : TestStep
    {
        [FilePath(FilePathAttribute.BehaviorChoice.Open)]
        public string StringPath { get; set; }

        [FilePath(FilePathAttribute.BehaviorChoice.Open)]
        public MacroString MacroPath { get; set; }

        public string SomeString { get; set; }

        public override void Run()
        {
        }
    }

    [TestFixture]
    public class TestPlanDependencyTest
    {
        private string[] _files => new[] {ReferencedFile, ReferencedFile2, PictureReference};
        private const string os = "Windows,Linux";
        private const string version = "3.4.5";
        private const string TestPackageName = "FakePackageReferencingFile";
        private const string ReferencedFile = "TestPlanFromPackage.TapPlan";
        public const string PictureReference = "SomePicture.png";
        private const string NotReferencedFile = "OtherFile.txt";
        private const string TestStepName = "Just a name for the step";
        private string ReferencedFile2 => $"Packages/{TestPackageName}/{ReferencedFile}";

        private string TestPackageXml => $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Package Name=""{TestPackageName}"" xmlns=""http://opentap.io/schemas/package"" Version=""{version}"" OS=""{os}"" >
  <Files>
      <File Path=""{ReferencedFile}""/>
      <File Path=""{ReferencedFile2}""/>
      <File Path=""{PictureReference}""/>
      <File Path=""{Path.GetFileName(typeof(MyTestStep).Assembly.Location)}""/>
  </Files>
</Package>
";

        private string PackageXmlPath =>
            Path.Combine(ExecutorClient.ExeDir, "Packages", TestPackageName, "package.xml");

        public string PreviousDirectory { get; set; }

        /// <summary>
        /// Engine unittests run in a temp directory it seems, and this test relies on the current installation
        /// </summary>
        [SetUp]
        public void SetDirectory()
        {
            PreviousDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(ExecutorClient.ExeDir);
        }
        
        [SetUp]
        public void Uninstall()
        {
            FileSystemHelper.EnsureDirectoryOf(PackageXmlPath);
            if (File.Exists(PackageXmlPath))
                File.Delete(PackageXmlPath);

            foreach (var file in _files)
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            
            Installation.Current.Invalidate();
        }

        [TearDown]
        public void TearDown()
        {
            Directory.SetCurrentDirectory(PreviousDirectory);
        }

        void VerifyDependency(string attr, string name, int count, XmlDocument document)
        {
            var nodes = document.SelectNodes(
                $"/TestPlan/Package.Dependencies/Package[@Name='{TestPackageName}']/{attr}[@Name='{name}']");
            Assert.AreEqual(count, nodes.Count);
        }

        [Test]
        public void TestPictureDependency()
        {
            InstallPackage();
            var plan = new TestPlan();
            var pic = new MyPictureUsingTestStep();
            pic.Picture.Source = PictureReference;
            plan.ChildTestSteps.Add(pic);

            var xml = plan.SerializeToString();

            var document = new XmlDocument();
            document.LoadXml(xml);

            VerifyDependency("File", PictureReference, 1, document);
        }

        [Test]
        public void TestFindInvalidPath()
        {
            var file = new string(Path.GetInvalidFileNameChars());
            var a = Installation.Current.FindPackageContainingFile(file);
            Assert.IsNull(a);

            var file2 = "abc/def : ghi.txt";
            var b = Installation.Current.FindPackageContainingFile(file2);
            Assert.IsNull(b);
        }


        [Test]
        public void TestPackageFileDependencies()
        {
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(new MyTestStep()
            {
                StringPath = ReferencedFile, MacroPath = new MacroString() {Text = ReferencedFile2},
                SomeString = NotReferencedFile,
                Name = TestStepName
            });

            var planXml = plan.SerializeToString();
            var document = new XmlDocument();
            document.LoadXml(planXml);

            void VerifyDependency(string attr, string name, int count)
            {
                this.VerifyDependency(attr, name, count, document);
            }


            // Verify warnings when files used by test steps are missing
            {
                VerifyDependency("File", ReferencedFile, 0);
                VerifyDependency("File", ReferencedFile2, 0);
                VerifyDependency("Type", typeof(MyTestStep).FullName, 0);
                VerifyDependency("File", NotReferencedFile, 0);

                var ser = new TapSerializer();
                ser.DeserializeFromString(planXml);
                var errors = ser.Errors.ToArray();

                Assert.AreEqual(0, errors.Length, "Expected no errors.");
            }

            // Verify no serializer errors when files are present
            {
                InstallPackage();
                // OnSearch invalidates the cache of currently installed packages;
                // otherwise the serializer will not detect that we have added a package 
                // And add the files the package is expected to contain
                Installation.Current.Invalidate();

                planXml = plan.SerializeToString();
                document.LoadXml(planXml);

                VerifyDependency("File", ReferencedFile, 1);
                VerifyDependency("File", ReferencedFile2, 1);
                // Uncomment when <Type> attributes are added to Package.Dependencies child elements
                // VerifyDependency("Type", typeof(MyTestStep).FullName, 1);
                VerifyDependency("File", NotReferencedFile, 0);

                var ser = new TapSerializer();
                ser.DeserializeFromString(planXml);
                var errors = ser.Errors.ToArray();

                Assert.AreEqual(0, errors.Length, "Expected 0 errors.");
            }

            Installation.Current.Invalidate();

            // Verify serializer errors when required package and files are missing
            {
                // Remove the package again
                Uninstall();

                var ser = new TapSerializer();
                ser.DeserializeFromString(planXml);
                var errors = ser.Errors.ToArray();

                // Expect a warning for the two [FilePath] properties on the test step
                // Expect an error for the missing package and two errors for the missing files
                Assert.AreEqual(3, errors.Length, "Expected 3 errors.");
                Assert.IsTrue(errors.Any(e =>
                    e.Contains(
                        $"Package '{TestPackageName}' is required to load, but it is not installed.")));
                Assert.IsTrue(errors.Any(e =>
                    e.Contains(
                        $"File '{ReferencedFile}' from package '{TestPackageName}' is required by the test plan, but it could not be found.")));
                Assert.IsTrue(errors.Any(e =>
                    e.Contains(
                        $"File '{ReferencedFile2}' from package '{TestPackageName}' is required by the test plan, but it could not be found.")));
            }
        }

        // Create fake install of the package
        private void InstallPackage()
        {
            File.WriteAllText(PackageXmlPath, TestPackageXml);
            foreach (var file in _files)
            {
                var fullPath = Path.Combine(ExecutorClient.ExeDir, file);
                File.WriteAllText(fullPath, "test");
            }
        }

        [Test]
        [TestCase(".")]
        [TestCase("../")]
        [TestCase("../../")]
        public void TestFindPackageOf(string workingDirectory)
        {
            var start = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(workingDirectory);
                // Test FindPackageContainingType(TypeData)
                {
                    var td = TypeData.FromType(typeof(MyTestStep));

                    var p1 = Installation.Current.FindPackageContainingType(td);
                    Assert.IsNull(p1);
                    InstallPackage();
                    var p2 = Installation.Current.FindPackageContainingType(td);
                    // The package should be null because the cache is not invalidated
                    Assert.IsNull(p2);
                    Installation.Current.Invalidate();
                    var p3 = Installation.Current.FindPackageContainingType(td);
                    Assert.IsNotNull(p3);

                    StringAssert.AreEqualIgnoringCase(p3.Name, TestPackageName);
                    StringAssert.AreEqualIgnoringCase(p3.Version.ToString(), version);
                }

                Uninstall();

                // Test FindPackageContainingFile("File/Path")
                {
                    var filename = ReferencedFile2;
                    var p1 = Installation.Current.FindPackageContainingFile(filename);
                    Assert.IsNull(p1);
                    InstallPackage();
                    var p2 = Installation.Current.FindPackageContainingFile(filename);
                    // The package should be null because the cache is not invalidated
                    Assert.IsNull(p2);
                    Installation.Current.Invalidate();
                    var p3 = Installation.Current.FindPackageContainingFile(filename);
                    Assert.IsNotNull(p3);

                    StringAssert.AreEqualIgnoringCase(p3.Name, TestPackageName);
                    StringAssert.AreEqualIgnoringCase(p3.Version.ToString(), version);
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(start);
            }
        }
    }
}
