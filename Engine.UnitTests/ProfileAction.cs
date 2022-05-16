using System;
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
        
        [CommandLineArgument("test")]
        public string Test { get; set; }
        
        [CommandLineArgument("type-data-attributes")]
        public bool TypeDataAttributes { get; set; }

        static TraceSource log = Log.CreateSource("profile"); 
        
        public int Execute(CancellationToken cancellationToken)
        {
            if (Test != null)
            {
                var it = Iterations == -1 ? 1 : Iterations;
                var td = TypeData.GetTypeData(Test);
                string methodName = null;
                if (td == null)
                {
                    var names = Test.Split('.');
                    methodName = names.Last();
                    td = TypeData.GetTypeData(string.Join(".", names.Take(names.Length - 1)));
                }
                var td2 = td.AsTypeData().Type;
                var obj = td.CreateInstance();

                var methods = methodName == null ? td2.GetMethods() : new[] {td2.GetMethod(methodName)};
                foreach (var method in methods)
                {
                    var paramCount = method.GetParameters().Count();
                    if (paramCount == 0)
                    {
                        var sw = Stopwatch.StartNew();
                        for (int i = 0; i < it; i++)    
                            method.Invoke(obj, Array.Empty<object>());
                        Console.WriteLine("{0}x{1}: : {2} ms,", method, it, sw.ElapsedMilliseconds);
                    }
                }
            }
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

                //using (ParameterManager.WithSanityCheckDelayed())
                {
                    for (int i = 0; i < count; i++)
                    {

                        var logStep = subPlan.Steps[i];
                        var messageMember = TypeData.GetTypeData(logStep).GetMember(nameof(LogStep.LogMessage));
                        messageMember.Parameterize(subPlan, logStep, "message");
                        var severity = TypeData.GetTypeData(logStep).GetMember(nameof(LogStep.Severity));
                        severity.Parameterize(subPlan, logStep, "sev" + i);
                    }
                }

                var mems = TypeData.GetTypeData(subPlan).GetMembers();
                var cn = mems.Count();
                Assert.Greater(cn, count);
                Assert.AreEqual(cn, count + 18);
                Assert.IsNotNull(TypeData.GetTypeData(subPlan).GetMember("sev0"));
                using (ParameterManager.WithSanityCheckDelayed())
                {
                    var members = AnnotationCollection.Annotate(subPlan).Get<IMembersAnnotation>().Members;
                    foreach (var mem in members)
                    {
                        if ((mem.Get<IAccessAnnotation>()?.IsVisible == true) && (mem.Get<IAccessAnnotation>()?.IsReadOnly == false))
                        {
                            if (mem.Get<IDisplayAnnotation>()?.Name.Contains("message") == true)
                            {
                                var v = mem.Get<IStringValueAnnotation>();
                                v.Value = "111111111";
                                mem.Write();
                            }
                        }
                    }
                    var cn2 = members.Count();
                }

                Assert.AreEqual("111111111", TypeData.GetTypeData(subPlan).GetMember("message").GetValue(subPlan));

                subPlan.Steps.Clear();
                var mems2 = TypeData.GetTypeData(subPlan).GetMembers();


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

            if (TypeDataAttributes)
            {
                // loop through all types and get attributes.
                int total = 0;
                var sw = Stopwatch.StartNew();
                var allTypes = PluginManager.GetSearcher().AllTypes.Values.ToArray();
                for (int i = 0; i < Iterations; i++)
                {
                    TapThread.Current.AbortToken.ThrowIfCancellationRequested();
                    foreach (var tp in allTypes)
                    {
                        foreach (var _ in tp.GetAttributes<DisplayAttribute>())
                        {
                            total += 1;
                        }
                    }
                }
                log.Info(sw, "TypeData (test number {0})", total);
            }
            return 0;
        }
    }
}