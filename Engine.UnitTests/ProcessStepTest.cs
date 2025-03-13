﻿using NUnit.Framework;
using OpenTap.Cli;
using OpenTap.Plugins.BasicSteps;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
                Application = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "tap.exe"),
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

        [Test]
        public void ProcessStepOutputs()
        {
            int exitCode = 5;
            var plan = new TestPlan();
            var processStep = new ProcessStep()
            {
                Application = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "tap.exe"),
                WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                Arguments = $"test fail --error {exitCode}",
            };
            plan.Steps.Add(processStep);
            var result = plan.Execute();
            Assert.AreEqual(exitCode, processStep.ExitCode);
            Assert.IsTrue(processStep.Output.Contains($"Failing with exit code {exitCode}"));
        }

        [Test]
        public void ProcessStepTimeoutTest()
        {
            var plan = new TestPlan();
            var processStep = new ProcessStep()
            {
                Application = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "tap.exe"),
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
                Application = "tap.exe",
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
