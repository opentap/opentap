using NUnit.Framework;
using OpenTap.Cli;
using OpenTap.Plugins.BasicSteps;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
    }
}
