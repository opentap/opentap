using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class PackageTestTest
    {
        #region Utils

        (string Timestamp, string Source, string Type, string Message)? Parse(string line)
        {
            var logParts = line.Split(new string[] {" : "}, StringSplitOptions.None);

            if (logParts.Length == 4)
                return (logParts[0].Trim(), logParts[1].Trim(), logParts[2].Trim(), logParts[3].Trim());
            return null;
        }

        (string Timestamp, string Source, string Type, string Message)[] ParseStdout(string stdout)
        {
            return stdout.Split('\n')
                .Select(Parse)
                .Where(v => v.HasValue)
                .Select(v => v.Value).ToArray();
        }
        private (string Stdout, string Stderr, int ExitCode) RunTest(bool verbose)
        {
            string arguments = "package test MyPlugin5";
            if (verbose)
                arguments += " -v";

            var process = new Process
            {
                StartInfo =
                {
                    FileName = "tap",
                    Arguments = arguments,
                    WorkingDirectory = WorkingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();

            // For some reason, the process is kept open when running in verbose.. ?
            if (verbose)
                process.WaitForExit(5000);
            else
            {
                process.WaitForExit();
            }

            return (process.StandardOutput.ReadToEnd(), process.StandardError.ReadToEnd(), process.ExitCode);
        }

        private void InstallTestPackage()
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = "tap",
                    Arguments = $"package install -f ./TapPackages/MyPlugin5.TapPackage",
                    WorkingDirectory = WorkingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();
        }

        private void Backup()
        {
            var original = PackageXml;
            var backup = Path.Combine(PackageDir, "backup.xml");
            if (File.Exists(backup) == false)
                File.Copy(original, backup);
        }

        private string PackageDir => Path.Combine(WorkingDirectory, "Packages", "MyPlugin5");
        private string PackageXml => Path.Combine(PackageDir, "package.xml");

        private void AddTestElement(string testAction)
        {
            var xmlContent = $@"
<PackageActionExtensions>
  {testAction}
</PackageActionExtensions>";
            string content = "";
            using (var reader = new StreamReader(PackageXml))
                content = reader.ReadToEnd();
            var lines = content.Split('\n').ToList();
            var idx = lines.FindIndex(l => l.Contains("</Package>"));
            lines.Insert(idx, xmlContent);

            using (var writer = new StreamWriter(PackageXml))
                writer.Write(string.Join("\n", lines));
        }

        private void Restore()
        {
            var original = PackageXml;
            var backup = Path.Combine(PackageDir, "backup.xml");
            if (File.Exists(original))
                File.Delete(original);
            File.Copy(backup, original);
        }

        private string WorkingDirectory => Directory.GetCurrentDirectory();
        #endregion
        public PackageTestTest()
        {
            InstallTestPackage();
            Backup();
            Restore();
        }

        [Test]
        public void BasicTest()
        {
            Restore();
            var (stdout, stderr, exitCode) = RunTest(true);

            StringAssert.Contains("Tried to test MyPlugin5, but there was nothing to do.", stdout);
            Assert.AreEqual(exitCode, 0);
        }

        [Ignore("Echo not available on Windows runner")]
        [Test]
        public void EchoTest()
        {
            Restore();
            var testAction = @"<ActionStep ActionName = ""test"" ExeFile=""echo"" Arguments='hello' />";
            AddTestElement(testAction);

            var normalOutput = RunTest(false);
            StringAssert.Contains("Starting test step 'echo hello'", normalOutput.Stdout);
            // The echoed "hello" should only appear in debug output 
            CollectionAssert.DoesNotContain(normalOutput.Stdout.Split('\n'), "hello");
            StringAssert.Contains("Successfully ran test step  'echo hello'", normalOutput.Stdout);
            Assert.AreEqual(0, normalOutput.ExitCode);

            var verboseOutput = RunTest(true);
            var verboseLines = ParseStdout(verboseOutput.Stdout);
            CollectionAssert.Contains(verboseLines.Select(v => v.Message), "hello");
            Assert.AreEqual(0, verboseOutput.ExitCode);
        }

        [Test]
        public void LogInheritTest()
        {
            Restore();
            var testAction = @"<ActionStep ActionName = ""test"" ExeFile=""tap"" Arguments='sdk gitversion' />";
            AddTestElement(testAction);

            var actualGitversion = new GitVersionCalulator(WorkingDirectory).GetVersion().ToString();

            {   // Normal output tests
                var normalOutput = RunTest(false);
                
                StringAssert.Contains("Starting test step 'tap sdk gitversion'", normalOutput.Stdout);
                StringAssert.Contains("Successfully tested MyPlugin5 version 1.0.0.", normalOutput.Stdout);
                StringAssert.Contains("Successfully ran test step  'tap sdk gitversion'", normalOutput.Stdout);
                StringAssert.Contains(actualGitversion, normalOutput.Stdout);
                Assert.AreEqual(0, normalOutput.ExitCode);
            }
            {   // Verbose output tests
                var verboseOutput = RunTest(true);

                StringAssert.Contains("Successfully tested MyPlugin5 version 1.0.0.", verboseOutput.Stdout);

                var verboseLines = ParseStdout(verboseOutput.Stdout);

                var gitversionInfos = verboseLines.Where(l => l.Source == "GitVersion" && l.Type == "Information");
                var gitversionMessages = gitversionInfos.Select(i => i.Message).ToArray();

                Assert.Contains(actualGitversion, gitversionMessages);
                Assert.AreEqual(0, verboseOutput.ExitCode);
            }
        }

        [Test]
        public void FailingTest()
        {
            var exeName = "SomeExeFileWhichDoesNotExist";
            Restore();
            var testAction = $@"<ActionStep ActionName = ""test"" ExeFile=""{exeName}"" Arguments='hello' />";
            AddTestElement(testAction);

            var normalOutput = RunTest(false);
            StringAssert.Contains($"Starting test step '{exeName} hello'", normalOutput.Stdout);
            StringAssert.DoesNotContain("Successfully ran test step", normalOutput.Stdout);
            Assert.AreNotEqual(0, normalOutput.ExitCode);
            StringAssert.Contains("Failed to run test package action", normalOutput.Stderr);
        }
    }
}
