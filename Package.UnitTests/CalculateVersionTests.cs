using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using Keysight.OpenTap.Sdk.MSBuild;
using Microsoft.Build.Framework;
using NUnit.Framework;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class CalculateVersionTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "calculate-version-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown() => FileSystemHelper.DeleteDirectory(directory);

        private CalculateVersion CreateTask(string input = "auto", string subdirectory = "") => new CalculateVersion
        {
            SourceFile = Path.Combine(directory, subdirectory, "Plugin.csproj"),
            // Automatic versioning must not need an installed tap executable.
            TapDir = Path.Combine(directory, "nonexistent-tap"),
            InputVersion = input,
            BuildEngine = new MockBuildEngine()
        };

        private string CommitVersion(string version, string subdirectory = "")
        {
            // Git is only used to create fixtures, never by the task under test.
            Git("init");
            Git("symbolic-ref", "HEAD", "refs/heads/main");
            var path = Path.Combine(subdirectory, ".gitversion");
            Directory.CreateDirectory(Path.Combine(directory, subdirectory));
            File.WriteAllText(Path.Combine(directory, path), "version = " + version + "\nbeta branch = ^main$\n");
            Git("add", "--", path);
            Git("-c", "user.name=Test", "-c", "user.email=test@example.com",
                "-c", "commit.gpgsign=false", "-c", "core.hooksPath=" + Path.Combine(directory, "no-hooks"),
                "commit", "-m", "Set version");
            return Git("rev-parse", "HEAD").Trim();
        }

        private string Git(params string[] arguments)
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = directory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start);
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            {
                process.Kill();
                Assert.Fail("Git fixture setup timed out.");
            }
            Assert.AreEqual(0, process.ExitCode, stderr.GetAwaiter().GetResult());
            return stdout.GetAwaiter().GetResult();
        }

        [TestCase("")]
        [TestCase("nested/project")]
        public void CalculatesInProcessFromProjectDirectory(string subdirectory)
        {
            var id = CommitVersion("1.2.3", subdirectory);
            var task = CreateTask(subdirectory: subdirectory);
            Assert.IsTrue(task.Execute());
            Assert.AreEqual("1.2.3", task.OutputShortVersion);
            Assert.AreEqual("1.2.3-beta.1+" + id.Substring(0, 8), task.OutputLongVersion);
            Assert.IsFalse(((MockBuildEngine)task.BuildEngine).buildEvents.OfType<BuildErrorEventArgs>().Any());
        }

        [TestCase("1.2.3-rc.2+abc")]
        [TestCase("<Version>1.2.3-rc.2+abc</Version>")]
        public void ExplicitVersionDoesNotRequireGit(string input)
        {
            var task = CreateTask(input);
            Assert.IsTrue(task.Execute());
            Assert.AreEqual("1.2.3", task.OutputShortVersion);
            Assert.AreEqual("1.2.3-rc.2+abc", task.OutputLongVersion);
        }

        [Test]
        public void NonGitSourcesUseZeroVersion()
        {
            var task = CreateTask();
            Assert.IsTrue(task.Execute());
            Assert.AreEqual("0.0.0", task.OutputLongVersion);
            Assert.IsTrue(((MockBuildEngine)task.BuildEngine).buildEvents.OfType<BuildWarningEventArgs>().Any());
        }

        [Test]
        public void UnsupportedRepositoryLogsBuildError()
        {
            var id = CommitVersion("1.2.3");
            File.WriteAllText(Path.Combine(directory, ".git", "shallow"), id + "\n");
            var task = CreateTask();
            Assert.IsFalse(task.Execute());
            StringAssert.Contains("Shallow history", ((MockBuildEngine)task.BuildEngine).buildEvents.OfType<BuildErrorEventArgs>().Single().Message);
        }

        private void AssertFailureWithError(CalculateVersion task, string message)
        {
            Assert.IsFalse(task.Execute());
            var errors = ((MockBuildEngine)task.BuildEngine).buildEvents.OfType<BuildErrorEventArgs>().ToList();
            Assert.AreEqual(1, errors.Count, "A failed task must log an error without duplicate diagnostics.");
            StringAssert.Contains(message, errors[0].Message);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void MissingInputLogsError(string input)
        {
            AssertFailureWithError(CreateTask(input), "must be provided");
        }

        [TestCase("<Version>", "Failed to parse input")]
        [TestCase("not-a-version", "Expected element named 'Version'")]
        [TestCase("<Version>invalid</Version>", "not a valid semantic version")]
        public void InvalidInputLogsError(string input, string message)
        {
            AssertFailureWithError(CreateTask(input), message);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("\0")]
        public void InvalidSourcePathLogsError(string sourceFile)
        {
            var task = CreateTask();
            task.SourceFile = sourceFile;
            AssertFailureWithError(task, "Unable to determine assembly version");
        }

        [Test]
        public void MissingProjectDirectoryLogsError()
        {
            AssertFailureWithError(CreateTask(subdirectory: "nonexistent"), "Unable to determine assembly version");
        }

        [Test]
        public void MissingGitVersionFileLogsError()
        {
            Git("init");
            AssertFailureWithError(CreateTask(), "does not have a .gitversion file");
        }

        [Test]
        public void CorruptRepositoryLogsError()
        {
            CommitVersion("1.2.3");
            File.Delete(Path.Combine(directory, ".git", "HEAD"));
            AssertFailureWithError(CreateTask(), "Unable to calculate gitversion");
        }

        [Test]
        public void CalculatorWarningsAreForwarded()
        {
            CommitVersion("invalid");
            var task = CreateTask();
            Assert.IsTrue(task.Execute());
            Assert.IsTrue(((MockBuildEngine)task.BuildEngine).buildEvents.OfType<BuildWarningEventArgs>()
                .Any(e => e.Message.Contains("Invalid version")));
        }
    }
}
