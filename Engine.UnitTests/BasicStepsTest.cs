using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenTap.Engine.UnitTests;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    public class BasicStepsTest
    {
        [TestCase(true, Verdict.Aborted, null)]
        [TestCase(false, Verdict.Error, null)]
        [TestCase(false, Verdict.Pass, Verdict.Pass)]
        public void TimeGuardStepTest(bool stopOnError, Verdict expectedVerdict, Verdict? verdictOnAbort)
        {
            var plan = new TestPlan();
            var guard = new TimeGuardStep {StopOnTimeout = stopOnError, Timeout = 0.05};
            if (verdictOnAbort != null)
                guard.TimeoutVerdict = verdictOnAbort.Value;
            
            // if this delay step runs to completion, the verdict of the test plan will be NotSet, failing the final assertion.
            var delay = new DelayStep {DelaySecs = 120};
            plan.ChildTestSteps.Add(guard);
            guard.ChildTestSteps.Add(delay);
            var run = plan.Execute();
            
            Assert.AreEqual(expectedVerdict, run.Verdict);
        }


        class PassThirdTime : TestStep
        {
            public int Iterations = 0;
            public override void PrePlanRun()
            {
                Iterations = 0;
                base.PrePlanRun();
            }

            public override void Run()
            {
                Iterations += 1;
                if (Iterations < 3)
                {
                    UpgradeVerdict(Verdict.Fail);
                }
                UpgradeVerdict(Verdict.Pass);
            }
        }
        
        [Test]
        [Pairwise]
        public void RepeatUntilPass([Values(true, false)] bool retry)
        {
            var step = new PassThirdTime();
            BreakConditionProperty.SetBreakCondition(step, BreakCondition.BreakOnFail);
            
            var rpt = new RepeatStep()
            {
                Action =  RepeatStep.RepeatStepAction.Until,
                TargetStep = step,
                TargetVerdict = Verdict.Pass,
                Retry = retry
            };
            rpt.ChildTestSteps.Add(step);

            var plan = new TestPlan();
            plan.ChildTestSteps.Add(rpt);

            var run = plan.Execute();

            if (retry)
            {
                Assert.AreEqual(Verdict.Pass, run.Verdict);
                Assert.AreEqual(3, step.Iterations);
            }
            else
            {
                // break condition reached -> Error verdict.
                Assert.AreEqual(Verdict.Fail, run.Verdict);
                Assert.AreEqual(1, step.Iterations);
            }
        }
        
        [Test]
        public void RepeatUntilPass2()
        {
            var step = new PassThirdTime();
            var rpt = new RepeatStep
            {
                Action =  RepeatStep.RepeatStepAction.Until,
                TargetStep = step,
                TargetVerdict = Verdict.Pass,
                ClearVerdict = true,
                MaxCount = new Enabled<uint>{IsEnabled = true, Value = 5}
            };
            rpt.ChildTestSteps.Add(step);
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(rpt);
            
            var run = plan.Execute();

            Assert.AreEqual(Verdict.Pass, run.Verdict);
            Assert.AreEqual(3, step.Iterations);
        }
        
        
        // These two cases are technically equivalent.
        [Test]
        [TestCase(Verdict.Fail, RepeatStep.RepeatStepAction.While)]
        [TestCase(Verdict.Pass, RepeatStep.RepeatStepAction.Until)]
        public void RepeatWhileError(Verdict targetVerdict, RepeatStep.RepeatStepAction action)
        {
            var step = new PassThirdTime();
            BreakConditionProperty.SetBreakCondition(step, BreakCondition.BreakOnFail);
            
            var rpt = new RepeatStep()
            {
                Action =  action,
                TargetVerdict = targetVerdict,
                Retry = true
            };
            rpt.TargetStep = rpt; // target self. The Repeat Loop will inherit the verdict.
            rpt.ChildTestSteps.Add(step);

            var plan = new TestPlan();
            plan.ChildTestSteps.Add(rpt);

            var run = plan.Execute();

            Assert.AreEqual(Verdict.Pass, run.Verdict); 
            Assert.AreEqual(3, step.Iterations);
        }

        [Test]
        public void ScpiRegexStepValidation()
        {
            ScpiInstrument instr = new ScpiDummyInstrument();
            instr.Rules.Clear(); // skip validation on the instrument itself.
            SCPIRegexStep scpiRegex = new SCPIRegexStep
            {
                Instrument = instr,
                Query = "SYST:CHAN:MOD? (@1,2)", // query with arguments.
                Action = SCPIAction.Query
            };
            Assert.IsTrue(string.IsNullOrEmpty(scpiRegex.Error));
            scpiRegex.Query = "SYST:CHAN:MOD"; // Not a valid query!
            Assert.IsFalse(string.IsNullOrEmpty(scpiRegex.Error));
            scpiRegex.Action = SCPIAction.Command; // it is a valid command though.
            Assert.IsTrue(string.IsNullOrEmpty(scpiRegex.Error));
        }

        class SimpleResultTest2 : TestStep 
        {
            public override void Run()
            {
                Results.Publish("Test", new List<string> { "X", "Y" }, 1.0, 2.0);
            }
        }
        
        [Test]
        public void RepeatCheckResultsHasIterations()
        {
            var pushResult1 = new SimpleResultTest2();
            var pushResult2 = new SimpleResultTest2();
            
            var repeatStep = new RepeatStep
            {
                Action =  RepeatStep.RepeatStepAction.Fixed_Count,
                Count = 100
            };
            
            var collectEverythingListener = new RecordAllResultListener();
            
            repeatStep.ChildTestSteps.Add(pushResult1);
            repeatStep.ChildTestSteps.Add(pushResult2);

            var plan = new TestPlan();
            plan.ChildTestSteps.Add(repeatStep);

            plan.Execute(new IResultListener[]{collectEverythingListener});
            
            // verify that there are 200 distinct result tables (from 200 different test plan runs)
            // 200 = repeatStep.Count * 2 (pushResult1 and pushResult2).
            Assert.AreEqual(200, collectEverythingListener.Results.Count);
            
            // verify that each result table came from a different step run
            Assert.AreEqual(200, collectEverythingListener.ResultTableGuids.Distinct().Count());
        }
        
        [Test]
        public void TestSweepLoopAcrossRunsReferencedResources()
        {
            var plan = new TestPlan();
            var sweep = new SweepLoop();
            var delay = new DelayStep();
            plan.ChildTestSteps.Add(sweep);
            sweep.ChildTestSteps.Add(delay);

            var b = AnnotationCollection.Annotate(sweep);
            var members = b.GetMember("SweepMembers");
            var avail = members.Get<IAvailableValuesAnnotationProxy>();
            var multi = members.Get<IMultiSelectAnnotationProxy>();
            multi.SelectedValues = avail.AvailableValues;
            b.Write();
            sweep.CrossPlan = SweepLoop.SweepBehaviour.Across_Runs;
            Assert.IsTrue(sweep.SweepParameters.Any());
            // if SweepParameters.Count > 0 && across-runs mode was enabled. This could cause an exception.
            Assert.AreEqual(0, sweep.ReferencedResources.Count());
        }

        public class ListNameStep : TestStep
        {
            public List<string> RecordedValues = new List<string>();
            public override void PrePlanRun()
            {
                base.PrePlanRun();
                RecordedValues.Clear();
            }
            public string StringMember { get; set; } = "";
            public override void Run()
            {
                RecordedValues.Add(StringMember);
                
            }
        }
        
        [Test]
        public void SweepFileTest()
        {
            var plan = new TestPlan();
            var sweep = new SweepFileParameterStep();
            var step1 = new ListNameStep();
            var step2 = new ListNameStep();
            plan.ChildTestSteps.Add(sweep);
            sweep.ChildTestSteps.Add(step1);
            sweep.ChildTestSteps.Add(step2);
            var p1 = TypeData.GetTypeData(step1).GetMember(nameof(step1.StringMember)).Parameterize(sweep, step1, "A");
            var p2 = TypeData.GetTypeData(step2).GetMember(nameof(step2.StringMember)).Parameterize(sweep, step2, "B");
            sweep.SelectedParameters = new List<ParameterMemberData>()
            {
                p1,
                p2
            };
            string thisStringDoesNotMatter = "";
            File.WriteAllText("csvTest.csv2", thisStringDoesNotMatter);
            sweep.SweepValues = "csvTest.csv2";
            plan.Execute();
            Assert.AreEqual("a", step1.RecordedValues[0]);
            Assert.AreEqual("b", step2.RecordedValues[0]);
            Assert.AreEqual("c", step1.RecordedValues[1]);
            Assert.AreEqual("d", step2.RecordedValues[1]);
            Assert.IsTrue(step1.RecordedValues.SequenceEqual(new []{"a", "c"}));
            Assert.IsTrue(step2.RecordedValues.SequenceEqual(new []{"b", "d"}));

        }
    }

    public class TestTableImport : ITableImport
    {

        public string Extension => ".csv2";
        public string Name => "supercsv";
        public string[][] ImportTableValues(string filePath)
        {
            return new[]
            {
                new[]
                {
                    "a", "b"
                },
                new[]
                {
                    "c", "d"
                }
            };
        }
    }
}