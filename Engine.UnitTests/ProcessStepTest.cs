using NUnit.Framework;
using OpenTap.Cli;
using OpenTap.Plugins.BasicSteps;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace OpenTap.UnitTests
{
    [Display("print", Groups: new[] { "test", "envvariables" }, Description: "Prints environment variables.")]
    public class PrintEnvVarAction : ICliAction
    {
        public int Execute(CancellationToken cancellationToken)
        {
            var log = Log.CreateSource("Environment variables");
            var variables = Environment.GetEnvironmentVariables();
            log.Info("Environment variables:");
            foreach (DictionaryEntry variable in variables)
            {
                log.Info($"\t{variable.Key} = {variable.Value}");
            }

            return (int)ExitCodes.Success;
        }
    }

    [Display("print", Groups: new[] { "test", "positionalargs" }, Description: "Prints environment variables.")]
    public class PrintPositionalArgs : ICliAction
    {
        [UnnamedCommandLineArgument(nameof(Positionals))]
        public string[] Positionals { get; set; } = [];
        public int Execute(CancellationToken cancellationToken)
        {
            for (int i = 0; i < Positionals.Length; i++)
            {
                Console.WriteLine($"{i}: <{Positionals[i]}>");
            }
            return 0;
        }
    }
    
    [Display("fail", Groups: new[] { "test" }, Description: "Fails a cli action")]
    public class FailCliAction : ICliAction
    {
        [CommandLineArgument("error")]
        public int ExitCode { get; set; } = 1;
        public int Execute(CancellationToken cancellationToken)
        {
            var log = Log.CreateSource("cli");
            log.Info("Failing with exit code {0}", ExitCode);
           
            return ExitCode;
        }
    }

    [TestFixture]
    public class ProcessStepTest
    {
        // Test single env variable.
        [TestCase(Verdict.Pass, "Ping = Pong", "Ping=Pong")]
        // Test multiple env variables.
        [TestCase(Verdict.Pass, "Ping = Pong", "Ping=Pong", "Test=test123")]
        [TestCase(Verdict.Pass, "Test = test123", "Ping=Pong", "Test=test123")]
        [TestCase(Verdict.Pass, "(Ping = Pong|Test = test123)", "Ping=Pong", "Test=test123")]
        // Test duplicate environment variable.
        [TestCase(Verdict.Error, "", "Ping=Pong", "Ping=Pong")]
        [TestCase(Verdict.Error, "", "Ping=Pong", "Ping=Ping")]
        public void ProcessStepSetEnvironmentVariables(Verdict expectedVerdict, string regex, params string[] variables)
        {
            var plan = new TestPlan();
            var processStep = new ProcessStep()
            {
                Application = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), tapBinary),
                WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                Arguments = "test envvariables print",
                RegularExpressionPattern = new Enabled<string>()
                {
                    IsEnabled = true,
                    Value = regex,
                },
            };
            foreach (var variable in variables)
            {
                string[] strs = variable.Split('=');
                processStep.EnvironmentVariables.Add(new ProcessStep.EnvironmentVariable { Name = strs[0], Value = strs[1] });
            }
            plan.Steps.Add(processStep);

            var result = plan.Execute();
            Assert.AreEqual(expectedVerdict, result.Verdict);
        }

        [TestCase(" hello   world ", "hello", "world")]
        [TestCase("basic-quotes 'quoted string' non-quoted \"another quote\"", "basic-quotes", "quoted string", "non-quoted", "another quote")]
        [TestCase("'literal \\ backslash'", "literal \\ backslash")]
        [TestCase("$'quote in \\' dollar string'", "quote in ' dollar string")]
        [TestCase("\"quote in \\\" normal string\"", "quote in \" normal string")]
        [TestCase("empty-quotes \"\" '' $''  ", "empty-quotes", "", "", "")]
        [TestCase("concat-adjacent-quotes \"1\"'2'$'3' ", "concat-adjacent-quotes", "123")]
        [TestCase("dollar escape \\$'hello'", "dollar", "escape", "$hello")]
        public void ProcessStepArgumentSplitting(string commandline, params string[] expected)
        {
            var plan = new TestPlan();
            var processStep = new ProcessStep()
            {
                Application = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), tapBinary),
                WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                Arguments = "test positionalargs print --quiet " + commandline,
                AddToLog = true,
            };
            processStep.EnvironmentVariables.Add(new ProcessStep.EnvironmentVariable { Name = "OPENTAP_COLOR", Value = "never" });
            plan.Steps.Add(processStep);
            var run = plan.Execute();

            Regex matchRegex = new("^\\d+: <.*>$", RegexOptions.Compiled);
            var lines = processStep.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                /* remove opentap noise */
                .Where(s => matchRegex.IsMatch(s))
                .ToArray();

            Assert.AreEqual(Verdict.Pass, run.Verdict, processStep.Output);
            /* verify every argument appears verbatim in the output (including newlines) */
            for (int i = 0; i < expected.Length; i++)
            {
                string exp = expected[i];
                var actual = $"{i}: <{exp}>";
                CollectionAssert.Contains(lines, actual);
            }
            /* verify there are no trailing arguments */
            Assert.IsFalse(lines.Any(x => x.StartsWith($"{expected.Length}:")));
        }

        [Test]
        public void ProcessStepOutputs()
        {
            int exitCode = 5;
            var plan = new TestPlan();
            var processStep = new ProcessStep()
            {
                Application = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), tapBinary),
                WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                Arguments = $"test fail --error {exitCode}",
            };
            plan.Steps.Add(processStep);
            var result = plan.Execute();
            Assert.AreEqual(exitCode, processStep.ExitCode);
            Assert.IsTrue(processStep.Output.Contains($"Failing with exit code {exitCode}"));
        }

        private string tapBinary = System.OperatingSystem.IsWindows() ? "tap.exe" : "tap";
        [Test]
        public void ProcessStepTimeoutTest()
        {
            var plan = new TestPlan();
            var processStep = new ProcessStep() 
            {
                Application = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), tapBinary),
                WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                Arguments = $"test fail",
                Timeout = 50, // ms
                WaitForEnd = true,
            };
            plan.ChildTestSteps.Add(processStep);
            var rl = new RecordAllResultListener();
            var planRun = plan.Execute([rl]);
            Assert.AreEqual(Verdict.Fail, planRun.Verdict);
            foreach (var line in rl.planLogs.First().Value.Split('\n'))
            {
                Assert.IsTrue(line.Split(';').ElementAtOrDefault(2)?.Trim() != "Error");
            }
            Assert.IsTrue(rl.planLogs.First().Value.Contains("Timed out while waiting for process to end"));
        }

        [Test]
        public void ProcessStepPass()
        {
            var plan = new TestPlan();
            var processStep = new ProcessStep()
            {
                Application = tapBinary,
                WorkingDirectory = "",
                Arguments = $"test fail --error 0",
                Timeout = 50000, // ms
                WaitForEnd = true,
                CheckExitCode = true
            };
            plan.ChildTestSteps.Add(processStep);
            var planRun = plan.Execute();
            Assert.AreEqual(Verdict.Pass, planRun.Verdict);
        }
    }
}
