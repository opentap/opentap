using System.IO;
using System.Threading;
using NUnit.Framework;
using OpenTap.Cli;
using OpenTap.EngineUnitTestUtils;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.Engine.UnitTests
{
    public class RunCliActionTest
    {
        const string planFile = "list-external-parameters.TapPlan";

        [Test]
        public void ListExternalParameters()
        {
            var runcli = new RunCliAction
            {
                ListExternal = true,
                TestPlanPath = planFile,
                NonInteractive = true
            };

            var plan = new TestPlan();
            var step = new ProcessStep()
            {
                LogHeader = null, // test that the log header is a null string.
                Application = "tap.exe" // test that application is a non-null string.
            };
            plan.ChildTestSteps.Add(step);
            var stepType = TypeData.GetTypeData(step);
            stepType.GetMember(nameof(step.LogHeader)).Parameterize(plan, step, "log-header");
            stepType.GetMember(nameof(step.Timeout)).Parameterize(plan, step, "timeout");
            stepType.GetMember(nameof(step.WaitForEnd)).Parameterize(plan, step, "wait-for-end");
            stepType.GetMember(nameof(step.VerdictOnMatch)).Parameterize(plan, step, "match-verdict");
            stepType.GetMember(nameof(step.Application)).Parameterize(plan, step, "application");
            plan.Save(planFile);
            var traceListener = new TestTraceListener();
            Log.AddListener(traceListener);
            try
            {
                var exitCode = runcli.Execute(CancellationToken.None);
                Assert.AreEqual(0, exitCode);
                Log.Flush();
                var log = traceListener.allLog.ToString();
                StringAssert.Contains("Listing 5 External Test Plan Parameters", log);
                StringAssert.Contains("application = tap.exe", log);
                StringAssert.Contains("log-header = ", log);
                StringAssert.Contains("wait-for-end = True", log);
                StringAssert.Contains("match-verdict = Pass", log);
            }
            finally
            {
                Log.RemoveListener(traceListener);
                File.Delete(planFile);
            }
        }
    }
}