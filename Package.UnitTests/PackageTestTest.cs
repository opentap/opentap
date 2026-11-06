using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using OpenTap.Cli;
using OpenTap.Diagnostic;
using OpenTap.EngineUnitTestUtils;

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

        class LogCollector : ILogListener
        {
            public enum Kind { Verbose, Stdout, Stderr }
            public class Message(Kind stream, string line)
            {
                public Kind Stream => stream;
                public string Line => line;
            }

            public List<Message> Messages { get; } = [];
            public void EventsLogged(IEnumerable<Event> Events)
            {
                foreach (var evt in Events)
                {
                    switch ((LogEventType)evt.EventType)
                    {
                        case LogEventType.Debug:
                            Messages.Add(new(Kind.Verbose, evt.Message));
                            break;
                        case LogEventType.Information:
                            Messages.Add(new(Kind.Stdout, evt.Message));
                            break;
                        case LogEventType.Warning:
                            Messages.Add(new(Kind.Stdout, evt.Message));
                            break;
                        case LogEventType.Error:
                            Messages.Add(new(Kind.Stderr, evt.Message));
                            break;
                    }
                }
            }

            public IEnumerable<string> Lines(Func<Message, bool> pred) => Messages.Where(pred).Select(x => x.Line);

            public void Flush() { }
        }
        private (string Stdout, string Stderr, int ExitCode) RunTest(bool verbose)
        {
            using var _ = Session.Create(SessionOptions.OverlayComponentSettings | SessionOptions.RedirectLogging);
            var evt = new LogCollector();
            Log.AddListener(evt);
            int status = CliActionExecutor.Execute("package", "test", "MyPlugin5");
            var stdout = string.Join("\n", evt.Lines(x => x.Stream == LogCollector.Kind.Stdout || (verbose && x.Stream == LogCollector.Kind.Verbose)));
            var stderr = string.Join("\n", evt.Lines(x => x.Stream == LogCollector.Kind.Stderr));
            return (stdout, stderr, status);
        }

        private void InstallTestPackage()
        {
            var status = new PackageInstallAction()
            {
                Packages = ["./TapPackages/MyPlugin5.TapPackage"],
                Force = true,
            }.Execute(CancellationToken.None);
            Assert.That(status, Is.Zero);
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
            var (stdout, _, exitCode) = RunTest(true);

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

            var (Stdout, Stderr, ExitCode) = RunTest(false);
            StringAssert.Contains($"Starting test step '{exeName} hello'", Stdout);
            StringAssert.DoesNotContain("Successfully ran test step", Stdout);
            Assert.AreNotEqual(0, ExitCode);
            StringAssert.Contains("Failed to run test package action", Stderr);
        }
    }
}
