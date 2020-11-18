using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
        
        
        public void GeneralPerformanceTest(int count)
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
            buildSequence(plan, 6);
            var total = Utils.FlattenHeirarchy(plan.ChildTestSteps, x => x.ChildTestSteps).Count();

            plan.Execute(); // warm up

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
        [CommandLineArgument("time-span")]
        public bool ProfileTimeSpanToString { get; set; }
        
        [CommandLineArgument("test-plan")]
        public bool ProfileTestPlan { get; set; }
        
        [CommandLineArgument("search")]
        public bool ProfileSearch { get; set; }

        [CommandLineArgument("run-long-plan")]
        public bool LongPlan { get; set; }
        
        [CommandLineArgument("run-long-plan-with-references")]
        public bool LongPlanWithReferences { get; set; }
        
        [CommandLineArgument("parameterize")]
        public bool Parameterize { get; set; }

        [CommandLineArgument("iterations")]
        public int Iterations { get; set; } = 10;
        
        public int Execute(CancellationToken cancellationToken)
        {
            if (ProfileTimeSpanToString)
            {
                StringBuilder sb =new StringBuilder();
                ShortTimeSpan.FromSeconds(0.01 ).ToString(sb);
                var sw = Stopwatch.StartNew();
                
                
                for (int i = 0; i < 1000000; i++)
                {
                    //ShortTimeSpan.FromSeconds(0.01 * i).ToString(sb);
                    ShortTimeSpan.FromSeconds(0.01 * i).ToString(sb);
                    if (i % 10 == 0)
                        sb.Clear();
                }

                Console.WriteLine("TimeSpan: {0}ms", sw.ElapsedMilliseconds);
            }
            if(ProfileTestPlan)
                new TestPlanPerformanceTest().GeneralPerformanceTest(10000);
            if (ProfileSearch)
            {
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < Iterations; i++)
                {
                    PluginManager.Search();
                }
                Console.WriteLine("Search Took {0}ms in total.", sw.ElapsedMilliseconds);
            }

            if (LongPlan)
            {
                var testplan = new TestPlan();
                for(int i = 0 ; i < 1000000; i++)
                    testplan.Steps.Add(new LogStep());
                var sw = Stopwatch.StartNew();
                testplan.Execute();
                Console.WriteLine("Run long plan took {0}ms in total.", sw.ElapsedMilliseconds);
            }

            if (LongPlanWithReferences)
            {
                var tmpFile = Guid.NewGuid().ToString() + ".TapPlan";
                {
                    var subPlan = new TestPlan();

                    int count = 1000;

                    for (int i = 0; i < count; i++)
                    {
                        var logStep = new LogStep();
                        subPlan.Steps.Add(logStep);
                    }
                        
                    
                    for (int i = 0; i < count; i++)
                    {
                        var logStep = subPlan.Steps[i];
                        var messageMember = TypeData.GetTypeData(logStep).GetMember(nameof(LogStep.LogMessage));
                        messageMember.Parameterize(subPlan, logStep, "message");
                    }

                    subPlan.Save(tmpFile);
                }

                try
                {
                    
                    var testPlan = new TestPlan();
                    for (int i = 0; i < 100; i++)
                    {
                        var refPlan = new TestPlanReference();
                        refPlan.Filepath.Text = tmpFile;
                        testPlan.Steps.Add(refPlan);
                        refPlan.Filepath= refPlan.Filepath;
                    }

                    testPlan.Execute();
                }
                finally
                {
                    File.Delete(tmpFile);
                }


            }
            
            if (Parameterize)
            {
                
                var subPlan = new TestPlan();

                int count = 100000;

                for (int i = 0; i < count; i++)
                {
                    var logStep = new LogStep();
                    subPlan.Steps.Add(logStep);
                }

                for (int i = 0; i < count; i++)
                {
                    var logStep = subPlan.Steps[i];
                    var messageMember = TypeData.GetTypeData(logStep).GetMember(nameof(LogStep.LogMessage));
                    messageMember.Parameterize(subPlan, logStep, "message");
                }
                
            }

            return 0;
        }
    }
}