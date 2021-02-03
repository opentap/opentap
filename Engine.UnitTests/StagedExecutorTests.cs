using NUnit.Framework;
using OpenTap.Engine.UnitTests;
using OpenTap.Plugins.BasicSteps;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.UnitTests
{
    public class StagedExecutorTests
    {
        public interface ITestExecutionStage : IExecutionStage
        {

        }

        public class FirstStage : ITestExecutionStage
        {
            public int Output = 0;
            public void Execute(ExecutionStageContext context)
            {
                Debug.WriteLine("First stage executing.");
                Thread.Sleep(50);
                Output = 1;
            }
        }

        public class SecondStage : ITestExecutionStage
        {
            public FirstStage First { get; set; }
            
            public void Execute(ExecutionStageContext context)
            {
                Assert.NotNull(First);
                Assert.AreEqual(1, First.Output);
                Debug.WriteLine("Second stage executing.");
                Thread.Sleep(50);
            }
        }

        public class ThirdStage : ITestExecutionStage
        {
            public SecondStage Dep { get; set; }

            public void Execute(ExecutionStageContext context)
            {
                Assert.NotNull(Dep);
                Thread.Sleep(50);
            }
        }

        public class FourthStage : ITestExecutionStage
        {
            public static bool DidRun = false;
            public SecondStage Dep { get; set; }
            public ThirdStage Dep2 { get; set; }

            public void Execute(ExecutionStageContext context)
            {
                Assert.NotNull(Dep);
                Assert.NotNull(Dep2);
                Thread.Sleep(50);
                DidRun = true;
            }
        }

        [Test]
        public void Execute()
        {
            var executor = new StagedExecutor(TypeData.FromType(typeof(ITestExecutionStage)));
            executor.Execute<string>(null);
            Assert.IsTrue(FourthStage.DidRun, "Execute completed without the last stage getting executed.");
        }
    }

    public class TestPlanStagedExecutorTests
    {
        [Test]
        public void SimpleTest()
        {
            TestPlan plan = new TestPlan();
            plan.Steps.Add(new DelayStep());

            Log.AddListener(new EngineUnitTestUtils.DiagnosticTraceListener());
            var executor = new TestPlanStagedExecutor(plan);
            var run = executor.Execute(Array.Empty<IResultListener>(), null, null);
            Assert.NotNull(run);
            Assert.AreEqual(Verdict.NotSet,run.Verdict);
            Log.Flush();
        }

        public class CrashStep : TestStep
        {
            public override void Run()
            {
                throw new Exception("Crash");
            }
        }

        [Test]
        public void SimpleCrashStepTest()
        {
            TestPlan plan = new TestPlan();
            plan.Steps.Add(new CrashStep());

            Log.AddListener(new EngineUnitTestUtils.DiagnosticTraceListener());
            var executor = new TestPlanStagedExecutor(plan);
            var run = executor.Execute(Array.Empty<IResultListener>(), null, null);
            Assert.NotNull(run);
            Assert.AreEqual(Verdict.Error, run.Verdict);
            Log.Flush();
        }

        //[Test]
        //public void CrashInstrTest()
        //{
        //    TestPlan plan = new TestPlan();
        //    var instr = new OpenCrash();
        //    InstrumentSettings.Current.Add(instr);
        //    try
        //    {
        //        plan.Steps.Add(new InstrumentTestStep() { Instrument = instr });

        //        Log.AddListener(new EngineUnitTestUtils.DiagnosticTraceListener());
        //        var executor = new TestPlanStagedExecutor(plan);
        //        var run = executor.Execute(Array.Empty<IResultListener>(), null, null);
        //        Assert.NotNull(run);
        //        Assert.AreEqual(Verdict.Error, run.Verdict);
        //        Assert.IsTrue(run.FailedToStart);
        //        Log.Flush();
        //    }
        //    finally
        //    {
        //        InstrumentSettings.Current.Remove(instr);
        //    }
        //}

    }
}
