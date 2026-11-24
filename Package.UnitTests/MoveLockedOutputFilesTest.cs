using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Keysight.OpenTap.Sdk.MSBuild;
using Microsoft.Build.Framework;
using NUnit.Framework;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class MoveLockedOutputFilesTest
    {
        string root, src, outDir, projectFile;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "MoveLockedOutputFilesTest", Guid.NewGuid().ToString());
            src = Path.Combine(root, "src");
            outDir = Path.Combine(root, "out") + Path.DirectorySeparatorChar;
            projectFile = Path.Combine(root, "MyPlugin.csproj");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(outDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }

        (string Source, string Destination) CreatePair(string name, string srcContent, string dstContent, DateTime srcTime, DateTime dstTime)
        {
            var s = Path.Combine(src, name);
            var d = Path.Combine(outDir, name);
            Directory.CreateDirectory(Path.GetDirectoryName(s));
            Directory.CreateDirectory(Path.GetDirectoryName(d));
            File.WriteAllText(s, srcContent);
            File.WriteAllText(d, dstContent);
            File.SetLastWriteTimeUtc(s, srcTime);
            File.SetLastWriteTimeUtc(d, dstTime);
            return (s, d);
        }

        static ITaskItem Item(string source, string destination, string mode) => new DictTaskItem(source)
        {
            MetaData = { ["Destination"] = destination, ["Mode"] = mode }
        };

        MoveLockedOutputFiles CreateTask(params ITaskItem[] items) => new MoveLockedOutputFiles
        {
            BuildEngine = new MockBuildEngine(),
            Files = items,
            OutDir = outDir,
            ProjectFile = projectFile,
        };

        FinishMovedOutputFiles CreateFinish(ITaskItem[] moved) => new FinishMovedOutputFiles
        {
            BuildEngine = new MockBuildEngine(),
            MovedFiles = moved,
        };

        string UninstallDir => Path.Combine(outDir, MoveLockedOutputFiles.UninstallDirName);

        string[] MovedFilesOnDisk() => Directory.Exists(UninstallDir)
            ? Directory.GetFiles(UninstallDir, "*", SearchOption.AllDirectories).Where(f => Path.GetFileName(f) != ".OpenTapIgnore").ToArray()
            : Array.Empty<string>();

        // Lock a file similar to how a loaded assembly is locked: renamable, but not writable.
        static IDisposable Lock(string file) =>
            new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);

        static readonly DateTime t0 = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [TestCase(MoveLockedOutputFiles.ModeIfDifferent, "same", "same", 0, false)]
        [TestCase(MoveLockedOutputFiles.ModeIfDifferent, "same", "same", 10, true)]
        [TestCase(MoveLockedOutputFiles.ModeIfDifferent, "same", "same", -10, true)]
        [TestCase(MoveLockedOutputFiles.ModeIfDifferent, "longer", "short", 0, true)]
        [TestCase(MoveLockedOutputFiles.ModeIfNewer, "same", "same", 0, false)]
        [TestCase(MoveLockedOutputFiles.ModeIfNewer, "same", "same", 10, true)]
        [TestCase(MoveLockedOutputFiles.ModeIfNewer, "same", "same", -10, false)]
        [TestCase(MoveLockedOutputFiles.ModeAlways, "same", "same", 0, true)]
        public void WillBeWrittenMirrorsMsBuild(string mode, string srcContent, string dstContent, int srcOffsetSeconds, bool expected)
        {
            var (s, d) = CreatePair("a.dll", srcContent, dstContent, t0.AddSeconds(srcOffsetSeconds), t0);
            Assert.AreEqual(expected, MoveLockedOutputFiles.WillBeWritten(s, d, mode));
        }

        [Test]
        public void WillBeWrittenIgnoresMissingFiles()
        {
            var (s, d) = CreatePair("a.dll", "x", "y", t0, t0);
            Assert.IsFalse(MoveLockedOutputFiles.WillBeWritten(s, d + ".missing", MoveLockedOutputFiles.ModeAlways));
            Assert.IsFalse(MoveLockedOutputFiles.WillBeWritten(s + ".missing", d, MoveLockedOutputFiles.ModeAlways));
            Assert.IsFalse(MoveLockedOutputFiles.WillBeWritten(d, d, MoveLockedOutputFiles.ModeAlways));
        }

        [Test]
        public void UnlockedFilesAreNotTouched()
        {
            var (s, d) = CreatePair("a.dll", "new", "old", t0.AddSeconds(10), t0);
            var task = CreateTask(Item(s, d, MoveLockedOutputFiles.ModeIfDifferent));
            Assert.IsTrue(task.Execute());
            Assert.IsEmpty(task.MovedFiles);
            Assert.IsTrue(File.Exists(d));
            Assert.AreEqual(t0, File.GetLastWriteTimeUtc(d), "Checking for locks must not modify the file.");
            Assert.IsFalse(Directory.Exists(UninstallDir));
        }

        [Test]
        public void FinishWithNothingReplacedRestoresFiles()
        {
            // Simulate a file moved by MoveLockedOutputFiles, which the build then did not replace.
            var (_, d) = CreatePair("a.dll", "x", "orig", t0, t0);
            var moveDir = Path.Combine(UninstallDir, "build-x-1");
            var movedTo = Path.Combine(moveDir, "a.dll");
            Directory.CreateDirectory(moveDir);
            File.Move(d, movedTo);
            var item = new DictTaskItem(d) { MetaData = { ["MovedTo"] = movedTo, ["MoveDir"] = moveDir } };

            var finish = CreateFinish(new ITaskItem[] { item });
            Assert.IsTrue(finish.Execute());

            Assert.IsTrue(File.Exists(d), "The file should be moved back.");
            Assert.AreEqual("orig", File.ReadAllText(d));
            Assert.AreEqual(t0, File.GetLastWriteTimeUtc(d), "Moving back must preserve the timestamp.");
            Assert.IsFalse(Directory.Exists(moveDir), "The empty move directory should be removed.");
            Assert.IsEmpty(finish.ReplacedFiles);
            Assert.IsEmpty(((MockBuildEngine)finish.BuildEngine).buildEvents.OfType<BuildWarningEventArgs>());
        }

        [Test]
        public void AnnounceUsesOutDirAndNeverFails()
        {
            var announced = new List<string>();
            var task = new AnnounceOpenTapBuild { BuildEngine = new MockBuildEngine(), OutDir = outDir, OpenTapRuntimeDir = "unused", Announce = announced.Add };
            Assert.IsTrue(task.Execute());
            CollectionAssert.AreEqual(new[] { AnnounceOpenTapBuild.CanonicalPath(outDir) }, announced);

            task.Announce = _ => throw new IOException("boom");
            Assert.IsTrue(task.Execute(), "Announcing must never fail the build.");
        }

        [Test]
        public void ProjectIdIsStableAndCaseInsensitive()
        {
            Assert.AreEqual(MoveLockedOutputFiles.ProjectId("/a/B/c.csproj"), MoveLockedOutputFiles.ProjectId("/a/b/C.csproj"));
            Assert.AreNotEqual(MoveLockedOutputFiles.ProjectId("/a/b/c.csproj"), MoveLockedOutputFiles.ProjectId("/a/b/d.csproj"));
        }

        [Test]
        [Platform(Include = "Win")]
        public void CanonicalPathUsesCasingOnDisk()
        {
            var dir = Path.Combine(root, "MixedCase");
            Directory.CreateDirectory(dir);
            Assert.AreEqual(dir, AnnounceOpenTapBuild.CanonicalPath(dir.ToLowerInvariant()));
        }

        [Test]
        [Platform(Include = "Win")]
        public void LockedFilesAreMovedOnlyIfTheyWillBeWritten()
        {
            var (s1, changed) = CreatePair("changed.dll", "new", "old", t0.AddSeconds(10), t0);
            var (s2, unchanged) = CreatePair("unchanged.dll", "same", "same", t0, t0);
            var (s3, nested) = CreatePair(Path.Combine("sub", "nested.dll"), "new", "old", t0.AddSeconds(10), t0);

            using (Lock(changed))
            using (Lock(unchanged))
            using (Lock(nested))
            {
                Assert.IsTrue(MoveLockedOutputFiles.IsLocked(changed));

                var task = CreateTask(
                    Item(s1, changed, MoveLockedOutputFiles.ModeIfDifferent),
                    Item(s2, unchanged, MoveLockedOutputFiles.ModeIfDifferent),
                    Item(s3, nested, MoveLockedOutputFiles.ModeIfNewer));
                Assert.IsTrue(task.Execute());

                CollectionAssert.AreEquivalent(new[] { changed, nested }, task.MovedFiles.Select(f => f.ItemSpec));
                Assert.IsFalse(File.Exists(changed));
                Assert.IsFalse(File.Exists(nested));
                Assert.IsTrue(File.Exists(unchanged), "Files which will not be written must not be moved.");

                Assert.IsTrue(File.Exists(Path.Combine(UninstallDir, ".OpenTapIgnore")));
                var moveDir = Directory.GetDirectories(UninstallDir).Single();
                StringAssert.StartsWith("build-" + MoveLockedOutputFiles.ProjectId(projectFile) + "-", Path.GetFileName(moveDir));
                Assert.IsTrue(File.Exists(Path.Combine(moveDir, "changed.dll")));
                Assert.IsTrue(File.Exists(Path.Combine(moveDir, "sub", "nested.dll")), "Relative paths should be preserved.");
                Assert.IsTrue(task.MovedFiles.All(f => f.GetMetadata("MoveDir") == moveDir));

                // Simulate the build: changed.dll is replaced, nested.dll is not (e.g. a custom target removed it).
                File.Copy(s1, changed);

                var finish = CreateFinish(task.MovedFiles);
                Assert.IsTrue(finish.Execute());

                CollectionAssert.AreEquivalent(new[] { changed }, finish.ReplacedFiles.Select(f => f.ItemSpec));
                Assert.AreEqual("new", File.ReadAllText(changed));
                Assert.AreEqual("old", File.ReadAllText(nested), "Files which were not replaced should be moved back.");
                Assert.AreEqual(t0, File.GetLastWriteTimeUtc(nested), "Moving back must preserve the timestamp.");
                var warning = ((MockBuildEngine)finish.BuildEngine).buildEvents.OfType<BuildWarningEventArgs>().Single();
                Assert.AreEqual("OPENTAP0001", warning.Code);

                // The old changed.dll is still in use, so it stays, and its directory is marked as done.
                Assert.IsTrue(File.Exists(Path.Combine(moveDir, "changed.dll")));
                Assert.IsTrue(File.Exists(Path.Combine(moveDir, MoveLockedOutputFiles.DoneMarker)));
            }

            // Once released, moved files are cleaned up by the next build.
            Assert.IsTrue(CreateTask().Execute());
            Assert.IsEmpty(Directory.GetDirectories(UninstallDir));
        }

        [Test]
        [Platform(Include = "Win")]
        public void FilesFromAnIncompleteBuildAreRestored()
        {
            var (s, d) = CreatePair("a.dll", "new", "old", t0.AddSeconds(10), t0);
            using (Lock(d))
            {
                var task = CreateTask(Item(s, d, MoveLockedOutputFiles.ModeIfDifferent));
                Assert.IsTrue(task.Execute());
                Assert.IsFalse(File.Exists(d));
            }
            // The build failed (e.g. a compile error in a later project), so FinishMovedOutputFiles never ran.
            // The next build of the same project restores the file.
            Assert.IsTrue(CreateTask().Execute());
            Assert.IsTrue(File.Exists(d));
            Assert.AreEqual("old", File.ReadAllText(d));
            Assert.AreEqual(t0, File.GetLastWriteTimeUtc(d));
            Assert.IsEmpty(MovedFilesOnDisk());
        }

        [Test]
        [Platform(Include = "Win")]
        public void IncompleteMovesFromOtherProjectsAreLeftAlone()
        {
            var (s, d) = CreatePair("a.dll", "new", "old", t0.AddSeconds(10), t0);
            using (Lock(d))
                Assert.IsTrue(CreateTask(Item(s, d, MoveLockedOutputFiles.ModeIfDifferent)).Execute());

            // Another project sharing the output directory may be building concurrently, so its moved files must
            // not be touched until it finishes.
            var other = new MoveLockedOutputFiles
            {
                BuildEngine = new MockBuildEngine(),
                Files = Array.Empty<ITaskItem>(),
                OutDir = outDir,
                ProjectFile = Path.Combine(root, "Other.csproj"),
            };
            Assert.IsTrue(other.Execute());
            Assert.IsFalse(File.Exists(d));
            Assert.AreEqual(1, MovedFilesOnDisk().Length);
        }

        [Test]
        [Platform(Include = "Win")]
        public void MovesAreUndoneIfAFileCannotBeMoved()
        {
            var (s1, movable) = CreatePair("a.dll", "new", "old", t0.AddSeconds(10), t0);
            var (s2, unmovable) = CreatePair("b.xml", "new", "old", t0.AddSeconds(10), t0);
            using (Lock(movable))
            // Opened without FileShare.Delete, e.g. File.OpenRead. Such files cannot be renamed.
            using (new FileStream(unmovable, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var task = CreateTask(
                    Item(s1, movable, MoveLockedOutputFiles.ModeIfDifferent),
                    Item(s2, unmovable, MoveLockedOutputFiles.ModeIfDifferent));
                Assert.IsTrue(task.Execute());
                Assert.IsEmpty(task.MovedFiles);
                Assert.IsTrue(File.Exists(movable), "The build will fail anyway, so the output directory should be left intact.");
                Assert.AreEqual(t0, File.GetLastWriteTimeUtc(movable));
                Assert.IsEmpty(MovedFilesOnDisk());
            }
        }
    }
}
