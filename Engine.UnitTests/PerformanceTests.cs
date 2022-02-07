using System;
using System.ComponentModel;
using System.Diagnostics;
using NUnit.Framework;
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

        class VirtualPropertiesStep : TestStep
        {
            public virtual string X { get; set; }
            public virtual string Y { get; set; }
            public virtual double Z { get; set; }
            [Browsable(false)]
            public virtual double[] Values { get; set; } = new double[1024];

            public override void Run()
            {
                
            }
        }
        
        
        public void GeneralPerformanceTest(int count, bool async, bool shortPlan = false)
        {
            void buildSequence(ITestStepParent parent, int levels)
            {
                parent.ChildTestSteps.Add(new ManySettingsStep());
                parent.ChildTestSteps.Add(new DeferringResultStep());
                parent.ChildTestSteps.Add(new VirtualPropertiesStep());
                for (int i = 0; i < levels; i++)
                {
                    var seq = new SequenceStep();
                    parent.ChildTestSteps.Add(seq);
                    buildSequence(seq, levels / 2);
                }
            }
            var plan = new TestPlan {CacheXml = true};
            buildSequence(plan, shortPlan ? 1 : 6);
            var total = Utils.FlattenHeirarchy(plan.ChildTestSteps, x => x.ChildTestSteps).Count();

            plan.Execute(); // warm up

            TimeSpan timeSpent = TimeSpan.Zero;
            
                for (int i = 0; i < count; i++)
                {
                    using (TypeData.WithTypeDataCache())
                    {
                        if(async)
                            timeSpent += plan.ExecuteAsync().Result.Duration;
                        else
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

    public class DutStep2 : TestStep
    {
        public Dut Dut { get; set; }
        public override void Run()
        {
            
        }
    }
    
    
}
