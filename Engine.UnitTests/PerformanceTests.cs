using System;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using OpenTap.Cli;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class TestPlanPerformanceTest
    {
        class DeferringResultStep : TestStep
        {
            static double result1 = 5;
            static double result2 = 5;
            public override void Run()
            {
                Results.Defer(() =>
                {
                    Results.Publish("Test", new {X = result1, Y = result2});
                });
            }
        }
            
        [Test, Ignore("Performance test that does not make sense in integration tests.")]
        public void GeneralPerformanceTest()
        {
            var plan = new TestPlan {CacheXml = true};
            for (int i = 0; i < 100; i++)
            {
                plan.Steps.Add(new SequenceStep());
                plan.Steps.Add(new ManySettingsStep());
                plan.Steps.Add(new DeferringResultStep());
            }

            plan.Execute(); // warm up

            int count = 1000;
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
            Console.WriteLine("Time spent per plan: {0}ms", spentMs);
            Console.WriteLine("Time spent per step: {0}ms", spentMs / plan.Steps.Count);
        }
    }

    [Display("profile")]
    public class ProfileAction : ICliAction
    {
        public int Execute(CancellationToken cancellationToken)
        {
            new TestPlanPerformanceTest().GeneralPerformanceTest();
            return 0;
        }
    }
}