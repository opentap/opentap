using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenTap.Cli;
using OpenTap.Plugins.BasicSteps;
using OpenTap.UnitTests;

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

    [Display("profile")]
    public class ProfileAction : ICliAction
    {
        [CommandLineArgument("time-span")]
        public bool ProfileTimeSpanToString { get; set; }
        
        [CommandLineArgument("test-plan", Description = "Expected time ~50s")]
        public bool ProfileTestPlan { get; set; }
        [CommandLineArgument("enabled-if")]
        public bool EnabledIfPerformanceTest { get; set; }
        
        [CommandLineArgument("short-test-plan", Description = "Expected time ~7s")]
        public bool ProfileShortTestPlan { get; set; }
        
        [CommandLineArgument("run-async")]
        public bool AsyncTestPlan { get; set; }
        
        [CommandLineArgument("search")]
        public bool ProfileSearch { get; set; }

        [CommandLineArgument("run-long-plan", Description = "Expected time ~1m20s")]
        public bool LongPlan { get; set; }
        
        [CommandLineArgument("run-long-plan-with-references", Description = "Expected time ~32s")]
        public bool LongPlanWithReferences { get; set; }
        
        [CommandLineArgument("serialize-deserialize-long-plan", Description = "Expected time ~13s")]
        public bool SerializeDeserializeLongPlan { get; set; }

        [CommandLineArgument("hide-steps")]
        public bool HideSteps { get; set; }
        
        
        [CommandLineArgument("parameterize")]
        public bool Parameterize { get; set; }

        [CommandLineArgument("iterations")]
        public int Iterations { get; set; } = -1;
        
        [CommandLineArgument("logging")]
        public bool Logging { get; set; }
        
        public int Execute(CancellationToken cancellationToken)
        {
            if (ProfileTimeSpanToString)
            {
                StringBuilder sb =new StringBuilder();
                ShortTimeSpan.FromSeconds(0.01 ).ToString(sb);
                var sw = Stopwatch.StartNew();
                var iterations = Iterations == -1 ? 1000000 : Iterations;
                
                for (int i = 0; i < iterations; i++)
                {
                    //ShortTimeSpan.FromSeconds(0.01 * i).ToString(sb);
                    ShortTimeSpan.FromSeconds(0.01 * i).ToString(sb);
                    if (i % 10 == 0)
                        sb.Clear();
                }

                Console.WriteLine("TimeSpan: {0}ms", sw.ElapsedMilliseconds);
            }

            if (Logging)
            {
                var iterations = Iterations == -1 ? 1000000 : Iterations;
                Log.Flush();
                var sw = Stopwatch.StartNew();
                var profileLogger = Log.CreateSource("Profile");
                for (int i = 0; i < iterations; i++)
                {
                    profileLogger.Debug("Iteration");
                }

                Log.Flush();
            }
            
            if(ProfileTestPlan)
                new TestPlanPerformanceTest().GeneralPerformanceTest(Iterations == -1 ? 10000 : Iterations, AsyncTestPlan);
            if(ProfileShortTestPlan)
                new TestPlanPerformanceTest().GeneralPerformanceTest(Iterations == -1 ? 10000 : Iterations, AsyncTestPlan, true);
            if (ProfileSearch)
            {
                var iterations = Iterations == -1 ? 10 : Iterations;
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < iterations; i++)
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
                
                var iterations = Iterations == -1 ? 1 : Iterations;
                for(int i = 0; i < iterations; i++)
                    testplan.Execute();
                Console.WriteLine("Run long plan took {0}ms in total.", sw.ElapsedMilliseconds);
            }

            if (LongPlanWithReferences)
            {
                DummyDut dut = new DummyDut();
                DutSettings.Current.Add(dut);
                var tmpFile = Guid.NewGuid().ToString() + ".TapPlan";
                {
                    var subPlan = new TestPlan();

                    
                    int count = 10;

                    for (int i = 0; i < count; i++)
                    {
                        var logStep = new LogStep();
                        subPlan.Steps.Add(logStep);
                    }

                    for (int i = 0; i < count; i++)
                    {
                        var logStep = new DutStep2() {Dut = dut};
                        subPlan.Steps.Add(logStep);
                    }
                        
                    
                    foreach(var step in subPlan.Steps)
                    {
                        if (step is LogStep logStep)
                        {
                            var messageMember = TypeData.GetTypeData(logStep).GetMember(nameof(LogStep.LogMessage));
                            messageMember.Parameterize(subPlan, logStep, "message");
                        }else if (step is DutStep2 dutStep)
                        {
                            var messageMember = TypeData.GetTypeData(dutStep).GetMember(nameof(dutStep.Dut));
                            messageMember.Parameterize(subPlan, dutStep, "dut");
                        }
                    }

                    subPlan.Save(tmpFile);
                }

                try
                {
                    var testPlan = new TestPlan();
                    var iterations = Iterations == -1 ? 10000 : Iterations;
                    for (int i = 0; i < iterations; i++)
                    {
                        var refPlan = new TestPlanReference
                        {
                        };
                        var hideSteps = refPlan.GetType().GetProperty("HideSteps", BindingFlags.Instance | BindingFlags.NonPublic);
                        hideSteps.SetValue(refPlan, true);

                        refPlan.Filepath.Text = tmpFile;
                        testPlan.Steps.Add(refPlan);
                        refPlan.Filepath= refPlan.Filepath;
                    }

                    var run = testPlan.Execute();
                    Assert.IsTrue(run.Verdict <= Verdict.Pass);
                }
                finally
                {
                    File.Delete(tmpFile);
                }


            }

            if (SerializeDeserializeLongPlan)
            {
                var testplan = new TestPlan();
                for (var i = 0; i < 100000; i++)
                    testplan.Steps.Add(new LogStep());
                var sw = Stopwatch.StartNew();

                var iterations = Iterations == -1 ? 1 : Iterations;
                for (var i = 0; i < iterations; i++)
                {
                    var serializer = new TapSerializer();
                    var planXml = serializer.SerializeToString(testplan);
                    testplan = (TestPlan) serializer.DeserializeFromString(planXml);
                }

                Console.WriteLine("Serialize/Deserialize long plan took {0}ms in total.", sw.ElapsedMilliseconds);
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

            if (EnabledIfPerformanceTest)
            {
                bool EnabledIfPerformanceTest()
                {
                    try
                    {
                        var myInstrument = new TestResource() {HasBeenOpened = false};
                        InstrumentSettings.Current.Clear();
                        InstrumentSettings.Current.Add(myInstrument);

                        var magnitude = 10; // magnitude^3 + magnitude^2 + magnitude steps
                        var timeLimit = TimeSpan.FromSeconds(10);

                        var sw = Stopwatch.StartNew();

                        var testPlan = new TestPlan();

                        // Build a triple nested test plan which forwards parameters upwards
                        using (ParameterManager.WithSanityCheckDelayed())
                        {
                            for (int i = 0; i < magnitude; i++)
                            {
                                var outerSequence = new SequenceStep() {Name = "outerSequence"};
                                testPlan.ChildTestSteps.Add(outerSequence);

                                for (int j = 0; j < magnitude; j++)
                                {
                                    var innerSequence = new SequenceStep() {Name = "innerSequence"};
                                    outerSequence.ChildTestSteps.Add(innerSequence);
                                    for (int k = 0; k < magnitude; k++)
                                    {
                                        var step = new ResourceTest.ResourceTestStep()
                                            {ResourceEnabled = true, MyTestResource = myInstrument};
                                        innerSequence.ChildTestSteps.Add(step);

                                        // Parameterize the member on the parent sequence
                                        var stepMember = TypeData.GetTypeData(step)
                                            .GetMember(nameof(ResourceTest.ResourceTestStep.MyTestResource));
                                        stepMember.Parameterize(innerSequence, step,
                                            nameof(ResourceTest.ResourceTestStep.MyTestResource));
                                    }

                                    // Forward the parameterized instrument to the outer sequence
                                    var innerSequenceMember = TypeData.GetTypeData(innerSequence)
                                        .GetMember(nameof(ResourceTest.ResourceTestStep.MyTestResource));
                                    innerSequenceMember.Parameterize(outerSequence, innerSequence,
                                        nameof(ResourceTest.ResourceTestStep.MyTestResource));
                                }

                                // Forward the forwarded instrument to the test plan
                                var outerSequenceMember = TypeData.GetTypeData(outerSequence)
                                    .GetMember(nameof(ResourceTest.ResourceTestStep.MyTestResource));
                                outerSequenceMember.Parameterize(testPlan, outerSequence,
                                    nameof(ResourceTest.ResourceTestStep.MyTestResource));
                            }
                        }

                        Assert.Less(sw.Elapsed, timeLimit);

                        // IsEnabled is potentially a bottleneck in large test plans
                        // Ensure it runs in a reasonable amount of time
                        sw = Stopwatch.StartNew();
                        
                        var cts = new CancellationTokenSource();
                        var t = Task.Run(() => testPlan.Execute(), cts.Token);
                        t.Wait(timeLimit);
                        if (t.IsCompleted == false)
                        {
                            cts.Cancel();
                            return false;
                        }

                        var run = t.Result;
                        Assert.IsFalse(run.FailedToStart);
                        Assert.AreEqual(Verdict.NotSet, run.Verdict);
                        Assert.Less(sw.Elapsed, timeLimit);
                        return true;
                    }
                    finally
                    {
                        InstrumentSettings.Current.Clear();
                    }
                }

                var timer = Stopwatch.StartNew();
                var success = EnabledIfPerformanceTest();
                if (success)
                {
                    Console.WriteLine($"Enabled-if performance test took {timer.ElapsedMilliseconds}ms in total.");
                    return 0;
                }
                else
                {
                    Console.WriteLine($"Enabled-if performance test failed after {timer.ElapsedMilliseconds}ms.");
                    return 1;
                }
            }

            return 0;
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
