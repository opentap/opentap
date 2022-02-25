using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using OpenTap.Sdk.MSBuild;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using OpenTap.Image.Tests;

namespace OpenTap.Package.UnitTests
{
    public class DictTaskItem : ITaskItem
    {
        // private ConcurrentDictionary<string, string> metadata = new ConcurrentDictionary<string, string>();
        public Dictionary<string, string> MetaData = new Dictionary<string, string>();

        public DictTaskItem(string itemSpec)
        {
            ItemSpec = itemSpec;
        }
        public string GetMetadata(string metadataName)
        {
            return MetaData.TryGetValue(metadataName, out var v) ? v : "";
        }

        public void SetMetadata(string metadataName, string metadataValue)
        {
            MetaData[metadataName] = metadataValue;
        }

        public void RemoveMetadata(string metadataName)
        {
            MetaData.Remove(metadataName);
        }

        public void CopyMetadataTo(ITaskItem destinationItem)
        {
            throw new System.NotImplementedException();
        }

        public IDictionary CloneCustomMetadata()
        {
            throw new System.NotImplementedException();
        }

        public string ItemSpec { get; set; }

        public ICollection MetadataNames => MetaData.Keys.ToArray();
        public int MetadataCount => MetaData.Count;
    }

    public class MockBuildEngine : IBuildEngine
    {
        private static TraceSource log = Log.CreateSource(nameof(MockBuildEngine));
        internal List<BuildEventArgs> buildEvents = new List<BuildEventArgs>();
        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            buildEvents.Add(e);
        }

        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            buildEvents.Add(e);
        }

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            buildEvents.Add(e);
        }

        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            buildEvents.Add(e);
        }

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties,
            IDictionary targetOutputs)
        {
            return true;
        }

        public bool ContinueOnError { get; }
        public int LineNumberOfTaskNode { get; }
        public int ColumnNumberOfTaskNode { get; }
        public string ProjectFileOfTaskNode { get; }
    }

    public class MockImageDeployer : IImageDeployer
    {
        public Action<ImageSpecifier, Installation, CancellationToken> OnInstall;

        public void Install(ImageSpecifier spec, Installation install, CancellationToken cts)
        {
            OnInstall(spec, install, cts);
        }
    }

    [TestFixture]
    public class MSBuildSdkTest
    {
        [Test]
        public void TestRejectOpenTap()
        {
            MockRepository mockRepository = new MockRepository("mock://localhost");
            PackageRepositoryHelpers.RegisterRepository(mockRepository);

            var openTapTaskItem = new DictTaskItem("OpenTAP")
            {
                MetaData = { ["Version"] = "9.12.0" }
            };
            ITaskItem[] packages = { openTapTaskItem };

            ITaskItem[] repos =
            {
                new TaskItem("mock://localhost"),
            };

            void Install(ImageSpecifier spec, Installation install, CancellationToken cts)
            {
                var expected = Installation.Current.GetOpenTapPackage();
                Assert.AreEqual(1, spec.Packages.Count);
                spec.Repositories.Add(Path.GetDirectoryName(typeof(MSBuildSdkTest).Assembly.Location));
                var img = spec.Resolve(cts);
                Assert.AreEqual(expected, img.Packages.First());
            }

            var deployer = new MockImageDeployer
            {
                OnInstall = Install
            };

            var buildEngine = new MockBuildEngine();

            var installBuildAction = new InstallOpenTapPackages
            {
                TapDir = ExecutorClient.ExeDir,
                BuildEngine = buildEngine,
                PackagesToInstall = packages,
                Repositories = repos,
                ImageDeployer = deployer
            };

            installBuildAction.Execute();

            var OpenTapNugetPackage = Installation.Current.GetOpenTapPackage();

            bool containsOpenTapWarning()
            {
                return buildEngine.buildEvents.Select(b => b.Message).Any(m =>
                    m.Contains($"This project was restored using OpenTAP version '{OpenTapNugetPackage.Version}'"));
            }
            Assert.IsTrue(containsOpenTapWarning(),
                "Requesting OpenTAP version 9.12 should issue a warning!");

            buildEngine.buildEvents.Clear();
            openTapTaskItem.SetMetadata("Version", "any");

            installBuildAction.Execute();

            Assert.IsFalse(containsOpenTapWarning(), "There should be no warning when requesting a compatible OpenTAP version!");
        }
    }
}