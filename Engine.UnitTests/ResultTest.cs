using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        
        [Test]
        public void TestSimpleResults3()
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

            var rl = new RecordAllResultListener();
            rl.OnTestStepRunStartAction = () => evt.WaitOne();
            
            plan.Execute(new []{rl});
            Assert.AreEqual(step.Count, rl.Results[0].Rows);
            Assert.AreEqual(1, rl.Results.Count);
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

        class SlowResultListener : ResultListener
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
        
    }
}