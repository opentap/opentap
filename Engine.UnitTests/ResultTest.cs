using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Threading;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    public class ResultTest
    {
        public class SimpleResultTest : TestStep
        {
            public class Result
            {
                public double A { get; set; }
                public double B { get; set; }
            }
            
            [Result]
            public Result StepResult { get; private set; }
            
            public override void Run()
            {
                StepResult = new Result {A = 1, B = 2};
            }
        }
        
        public class SimpleResultTestMany : TestStep
        {
            public class Result
            {
                public double A { get; set; }
                public double B { get; set; }
            }
            
            public int Count { get; set; } = 1;
            
            public override void Run()
            {
                for(int i =0 ; i < Count; i++)
                    Results.Publish(new Result{A = i, B = i});
            }
        }

        public class ResultTrivial : TestStep
        {

            [Result]
            public double A { get; set; }
            [Result]
            public int B { get; set; }
            [Result]
            public string C { get; set; }
            public override void Run()
            {
                
            }
        }

        public class ActionStep : TestStep
        {
            public Action Action { get; set; }
            public override void Run()
            {
                Action();
            }
        }
        
        [Test]
        public void TestSimpleResults()
        {
            var plan = new TestPlan();
            var step = new SimpleResultTest();
            plan.Steps.Add(step);

            var rl = new RecordAllResultListener();
            
            plan.Execute(new []{rl});

            var t1 = rl.Results[0];
            Assert.AreEqual(1, t1.Rows);
            var columnA = t1.Columns.First(x => x.Name == "A");
            var columnB = t1.Columns.First(x => x.Name == "B");
            Assert.AreEqual(1.0, columnA.Data.GetValue(0));
            Assert.AreEqual(2.0, columnB.Data.GetValue(0));
        }

        [Test]
        public void TestSimpleResults4()
        {
            var step = new ResultTrivial
            {
                A = 1,
                B = 2,
                C = "3"
            };
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(step);
            
            var rl = new RecordAllResultListener();
            
            plan.Execute(new []{rl});
            var table1 = rl.Results.First();
            ResultColumn column(string s) => table1.Columns.FirstOrDefault(x => x.Name == s);
            var a = column("A")?.GetValue<double>(0);
            var b = column("B")?.GetValue<int>(0);
            var c = column("C")?.GetValue<string>(0);
            Assert.AreEqual(3, table1.Columns.Length);
            Assert.AreEqual(1.0, a);
            Assert.AreEqual(2, b);
            Assert.AreEqual("3", c);

        }

        [Test]
        public void TestResultParameters()
        {
            var plan = new TestPlan();
            var step = new SimpleResultTest();
            plan.Steps.Add(step);

            var rl = new RecordAllResultListener();
            
            plan.Execute(new []{rl});
            var run = rl.Runs.Values.OfType<TestStepRun>().FirstOrDefault();

        }
        
        [Test]
        public void TestSimpleResults2()
        {
            var plan = new TestPlan();
            var step = new SimpleResultTest();
            TypeData.GetTypeData(step).GetMember("OpenTap.Description").SetValue(step, "ASD");
            plan.Steps.Add(step);

            var rl = new RecordAllResultListener();
            
            plan.Execute(new []{rl});

            var parameterNames = rl.Runs.Values.OfType<TestStepRun>().SelectMany(run => run.Parameters.Select(x => x.Name)).Distinct();
            Assert.IsFalse(parameterNames.Contains("Description"));
            Assert.IsFalse(parameterNames.Contains("Break Conditions"));
        }
        
        [TestCase(true)]
        [TestCase(false)]
        public void TestSimpleResults3(bool mergeResults)
        {
            var plan = new TestPlan();
            var step = new SimpleResultTestMany
            {
                Count = 10
            };
            var evt = new ManualResetEvent(false);
            var actionStep = new ActionStep()
            {
                Action = () => evt.Set()
            };
            TypeData.GetTypeData(step).GetMember("OpenTap.Description").SetValue(step, "ASD");
            plan.Steps.Add(step);
            plan.Steps.Add(actionStep);

            RecordAllResultListener rl = mergeResults ? new RecordAllMergedResultListener() : new RecordAllResultListener();
            rl.OnTestStepRunStartAction = () => evt.WaitOne();
            
            plan.Execute(new []{rl});
            if (mergeResults)
            {
                Assert.AreEqual(step.Count, rl.Results[0].Rows);
                Assert.AreEqual(1, rl.Results.Count);    
            }
            else
            {
                Assert.AreEqual(1, rl.Results[0].Rows);
                Assert.AreEqual(10, rl.Results.Count);
            }
            
        }

        [Test]
        public void TestResultMetadataSimple()
        {
            var metadata = new ResultParameter("Test", "MetaData1", "Value1", new MetaDataAttribute());
            var plan = new TestPlan();
            var pr = new TestPlanRun(plan, new List<IResultListener>(), DateTime.Now, 0,"",false);
            pr.Parameters.Add(metadata);
            var mt = pr.Parameters.Find((metadata.Name, "Test"));
            Assert.IsTrue(mt.IsMetaData);
        }
        
        
        [Test]
        public void TestResultMetadata()
        {
            var step = new SimpleResultTest();
            var seq = new SequenceStep();
            var plan = new TestPlan();
            plan.Steps.Add(seq);
            seq.ChildTestSteps.Add(step);
            
            var rl = new SimpleResultTest2();
            var metadata = new ResultParameter("Test", "MetaData1", "Value1", new MetaDataAttribute());
            Assert.IsTrue(metadata.IsMetaData);
            var run = plan.Execute(new[] {rl}, new[] {metadata});
            Assert.IsTrue(run.Parameters.Find(("MetaData1", "Test")).IsMetaData);
        }

        [Test]
        public void TestResultParameterConstructorArguments()
        {
            var step = new SimpleResultTest();
            var seq = new SequenceStep();
            var plan = new TestPlan();
            plan.Steps.Add(seq);
            seq.ChildTestSteps.Add(step);
            
            var rl = new SimpleResultTest2();
            var planRun = plan.Execute(new[] {rl});
            var resultParameters = new ResultParameters(planRun.Parameters);
            var resultParameters1 = new ResultParameters(planRun.Parameters.Concat(new List<ResultParameter> {
                //new ResultParameter("", "Duration", planRun.Duration.TotalSeconds),
                new ResultParameter("", "Verdict", planRun.Verdict)
            }));
            Assert.IsTrue(resultParameters.SequenceEqual(resultParameters1));
        }

        
        class CustomResultColumn : ResultColumn, IAttributedObject
        {
            string IAttributedObject.ObjectType => "Limit Column";
            public CustomResultColumn(string name, Array data) : base(name, data)
            {
            }

            public CustomResultColumn(string name, Array data, params IParameter[] parameters) : base(name, data, parameters)
            {
            }

            internal CustomResultColumn(string name, Array data, IData table, IParameters parameters) : base(name, data, table, parameters)
            {
            }
        }
        [Test]
        public void TestResultTableMutability()
        {
            var rt = new ResultTable("Test1", new ResultColumn[]
            {
                new ResultColumn("X", new double[] {1, 2, 3}),
                new CustomResultColumn("Limit", new double[] {4, 4, 4})
            });
            Assert.AreEqual((rt.Columns.Last() as IAttributedObject).ObjectType, "Limit Column");
            rt.Columns[0].Data.SetValue(10.0, 0);
            Assert.AreEqual(10.0, rt.Columns[0].Data.GetValue(0));
        }
        

        class SimpleResultTest2 : ResultListener
        {
            
            Dictionary<Guid, TestRun> runs = new Dictionary<Guid, TestRun>();
            public override void OnTestPlanRunStart(TestPlanRun planRun) => runs.Add(planRun.Id, planRun);
            
            public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream) => runs.Remove(planRun.Id);
            public override void OnTestStepRunStart(TestStepRun stepRun) => runs.Add(stepRun.Id, stepRun);
            public override void OnTestStepRunCompleted(TestStepRun stepRun) => runs.Remove(stepRun.Id);

            public override void OnResultPublished(Guid stepRunId, ResultTable result)
            {
                base.OnResultPublished(stepRunId, result);
                ResultParameters parameterList = new ResultParameters();
                Guid runid = stepRunId;
                while (runs.TryGetValue(runid, out TestRun subRun))
                {
                    foreach(var subparameter in subRun.Parameters.Where(parameter => parameter.IsMetaData))
                        if (parameterList.Find(subparameter.Key) == null)
                        {
                            parameterList.Add(subparameter);
                        }
                    if (subRun is TestStepRun run)
                        runid = run.Parent;
                    else break;
                }
            }
        }

        class SlowResultListener : ResultListener, IMergedTableResultListener
        {
            public override void OnResultPublished(Guid stepRunId, ResultTable result)
            {
                base.OnResultPublished(stepRunId, result);
                TapThread.Sleep(100);
            }
        }
        
        [Test]
        public void TestResultsOptimizeBug()
        {
            // An issue in the code caused the semaphores to be released but never waited
            // this was because work items are popped from the WorkQueue when optimized result tables
            // are being created.
            // When we hit the max semaphore count (normally around 1M work items) this causes an infinite delay.
            
            using (Session.Create(SessionOptions.OverlayComponentSettings))
            {
                // set the max semaphore count so we dont have to create 1M results, but just around 30.
                var prevSemCount = WorkQueue.semaphoreMaxCount;
                WorkQueue.semaphoreMaxCount = 20;
                try
                {
                    ResultSettings.Current.Add(new SlowResultListener());
                    var plan = new TestPlan();
                    var step = new SimpleResultTestMany {Count = 1000};
                    plan.Steps.Add(step);

                    var token = CancellationTokenSource.CreateLinkedTokenSource(TapThread.Current.AbortToken);
                    // cancel the run after 60 seconds.
                    token.CancelAfter(TimeSpan.FromSeconds(60));
                    Assert.AreEqual(Verdict.NotSet, plan.ExecuteAsync(token.Token).Result.Verdict);
                }
                finally
                {
                    WorkQueue.semaphoreMaxCount = prevSemCount;
                }
            }
        }
        
        [Display("Throwing Property Instrument")]
        public class ThrowingPropertyInstrument : Instrument
        {
            [Browsable(false)]
            [System.Xml.Serialization.XmlIgnore]
            public bool ShouldThrow { get; set; }

            [Display("Value")]
            public double Value
            {
                get
                {
                    if (ShouldThrow)
                        throw new InvalidOperationException("Device is detached");
                    return 42.0;
                }
                set { }
            }

            public override void Open() { base.Open(); ShouldThrow = false; }
            public override void Close() { ShouldThrow = false; base.Close(); }
        }

        [Display("Throwing Property Step")]
        public class ThrowingPropertyStep : TestStep
        {
            public ThrowingPropertyInstrument Instrument { get; set; }

            public override void Run()
            {
                Instrument.ShouldThrow = true;
                UpgradeVerdict(Verdict.Pass);
            }
        }

        /// <summary>
        /// Verifies that an exception thrown from an instrument property getter during
        /// post-Run() ResultParameters.UpdateParams() gives the step Verdict.Error but
        /// does not bypass break conditions — sibling steps still execute.
        /// Reproduces GitHub issue #2307.
        /// </summary>
        [Test]
        public void PropertyGetterExceptionInUpdateParamsGivesErrorVerdictAndRespectBreakConditions()
        {
            var instrument = new ThrowingPropertyInstrument();
            InstrumentSettings.Current.Add(instrument);
            try
            {
                var plan = new TestPlan();
                var sequence = new SequenceStep();
                plan.ChildTestSteps.Add(sequence);

                var throwingStep = new ThrowingPropertyStep { Instrument = instrument };
                // Set break condition to "Do not break" so sibling steps still run.
                BreakConditionProperty.SetBreakCondition(throwingStep, (BreakCondition)0);
                sequence.ChildTestSteps.Add(throwingStep);

                var logStep = new LogStep();
                sequence.ChildTestSteps.Add(logStep);

                var rl = new RecordAllResultListener();
                var run = plan.Execute(new[] { rl });

                var stepRuns = rl.Runs.Values.OfType<TestStepRun>().ToList();

                // The throwing step should have Verdict.Error from the property getter exception.
                var throwingRun = stepRuns.FirstOrDefault(r => r.TestStepId == throwingStep.Id);
                Assert.IsNotNull(throwingRun, "Throwing step run should exist");
                Assert.AreEqual(Verdict.Error, throwingRun.Verdict,
                    "Throwing step should get Verdict.Error from property getter exception in UpdateParams");

                // The log step should still have been executed (break conditions respected).
                var logRun = stepRuns.FirstOrDefault(r => r.TestStepId == logStep.Id);
                Assert.IsNotNull(logRun, "Log step should have been executed — break conditions must be respected");

                // The plan verdict should be Error (propagated from the step).
                Assert.AreEqual(Verdict.Error, run.Verdict);
            }
            finally
            {
                InstrumentSettings.Current.Remove(instrument);
            }
        }

        /// <summary>
        /// Verifies that when a property getter throws during TestStepRun construction
        /// (GetParams), the step gets Verdict.Error and does not execute Run().
        /// </summary>
        [Test]
        public void PropertyGetterExceptionInGetParamsGivesErrorVerdictWithoutRunning()
        {
            var instrument = new ThrowingPropertyInstrument { ShouldThrow = true };
            InstrumentSettings.Current.Add(instrument);
            try
            {
                var plan = new TestPlan();
                var throwingStep = new ThrowingPropertyStep { Instrument = instrument };
                plan.ChildTestSteps.Add(throwingStep);

                var rl = new RecordAllResultListener();
                var run = plan.Execute(new[] { rl });

                // The step should have a run with Verdict.Error.
                var stepRuns = rl.Runs.Values.OfType<TestStepRun>().ToList();
                var throwingRun = stepRuns.FirstOrDefault(r => r.TestStepId == throwingStep.Id);
                Assert.IsNotNull(throwingRun, "Step run should still be created even with property getter error");
                Assert.AreEqual(Verdict.Error, throwingRun.Verdict,
                    "Step should get Verdict.Error when GetParams encounters a property getter exception");
            }
            finally
            {
                InstrumentSettings.Current.Remove(instrument);
            }
        }

        /// <summary>
        /// Verifies that ResultParameters.GetParams handles property getter exceptions
        /// gracefully — returns parameters for non-failing properties and does not throw.
        /// </summary>
        [Test]
        public void GetParamsHandlesPropertyGetterException()
        {
            var instrument = new ThrowingPropertyInstrument { ShouldThrow = true };
            InstrumentSettings.Current.Add(instrument);
            try
            {
                var step = new ThrowingPropertyStep { Instrument = instrument };
                // GetParams reads properties including instrument properties.
                // Should not throw even though the instrument property getter throws.
                var parameters = ResultParameters.GetParams(step);
                Assert.IsNotNull(parameters);
            }
            finally
            {
                InstrumentSettings.Current.Remove(instrument);
            }
        }
    }
}