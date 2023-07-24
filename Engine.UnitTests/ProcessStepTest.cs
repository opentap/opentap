using NUnit.Framework;
using OpenTap.Cli;
using OpenTap.Plugins.BasicSteps;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using OpenTap.Expressions;

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
        
        [Test]
        public void ProcessStepSetEnvironmentVariables2()
        {
            var plan = new TestPlan();
            var processStep = new ProcessStep()
            {
                Application = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "tap.exe"),
                WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                Arguments = "test envvariables print",
                Output = "0.0"
            };
            processStep.EnvironmentVariables.Add(new ProcessStep.EnvironmentVariable { Name = "VarA", Value = "1" });
            processStep.EnvironmentVariables.Add(new ProcessStep.EnvironmentVariable { Name = "VarB", Value = "2" });
            var Amem = new UserDefinedDynamicMember
            {
                TypeDescriptor = TypeData.FromType(typeof(double)),
                Name = "A",
                Readable = true,
                Writable = true,
                DeclaringType = TypeData.FromType(typeof(TestStep))
            };
            var Bmem = new UserDefinedDynamicMember
            {
                TypeDescriptor = TypeData.FromType(typeof(double)),
                Name = "B2",
                Readable = true,
                Writable = true,
                DeclaringType = TypeData.FromType(typeof(TestStep))
            };
            DynamicMember.AddDynamicMember(processStep, Amem);
            DynamicMember.AddDynamicMember(processStep, Bmem);
            Amem.SetValue(processStep, 0.0);
            Bmem.SetValue(processStep, 0.0);
            
            ExpressionManager.SetExpression(processStep, Amem, "Match(\"VarA = (?<A>[0-9]*)\",\"A\", Output)");
            ExpressionManager.SetExpression(processStep, Bmem, "Match(\"VarB = (?<B>[0-9]*)\",\"B\", Output)");
            
            plan.Steps.Add(processStep);

            var result = plan.Execute();

            Assert.AreEqual(1.0, Amem.GetValue(processStep));
            Assert.AreEqual(2.0, Bmem.GetValue(processStep));

        }
    }
}
