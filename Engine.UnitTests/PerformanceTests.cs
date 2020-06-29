using System;
using System.Diagnostics;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class TestPlanPerformanceTest
    {
        [Test, Ignore("Performance test that does not make sense in integration tests.")]
        public void GeneralPerformanceTest()
        {
            var plan = new TestPlan {CacheXml = true};
            for (int i = 0; i < 50; i++)
            {
                plan.Steps.Add(new SequenceStep());
                plan.Steps.Add(new ManySettingsStep());
            }

            plan.Execute(); // warm up

            int count = 100;
            TimeSpan timeSpent = TimeSpan.Zero;
            for (int i = 0; i < count; i++)
            {
                using (TypeData.WithTypeDataCache())
                {
                    timeSpent += plan.Execute().Duration;
                }
            }

            var proc = Process.GetCurrentProcess();
            var time = proc.TotalProcessorTime;
            var time2 = DateTime.Now - proc.StartTime;
            var spentMs = timeSpent.TotalMilliseconds / count;
        }
    }
}