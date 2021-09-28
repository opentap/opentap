using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;
using OpenTap.EngineUnitTestUtils;

namespace OpenTap.UnitTests
{
    public class ResultListenerApiTest
    {
        [Test]
        public void TestReadLog()
        {
            var logListener = new TestTraceListener();
            Log.AddListener(logListener);
            try
            {
                var rl = new RecordAllResultListener();
                var plan = new TestPlan();
                var log = new LogStep();
                
                // force the hybrid log stream to save in a file by
                // writing ~10MB log data.
                log.LogMessage = new string('A', 100000);
                var repeat = new RepeatStep() {Count = 100};
                repeat.ChildTestSteps.Add(log);
                plan.ChildTestSteps.Add(repeat);
                var run = plan.Execute(new IResultListener[] {rl});
                Log.Flush();
                Assert.AreEqual(Verdict.NotSet, run.Verdict);
                Assert.IsTrue(logListener.ErrorMessage.Count == 0);
                Assert.IsNull(run.Parameters["TestPlanPath"]);
                Assert.IsNull(run.Parameters.Find("TestPlanPath"));

            }
            finally
            {
                Log.RemoveListener(logListener);
            }
        }
    }
}