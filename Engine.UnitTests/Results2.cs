using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenTap.Engine.UnitTests;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    public class ResultRow
    {
        public string ResultName { get; set; }
        public int ResultValue { get; set; }
    }

    public class PublishResultsSameValues : TestStep
    {

        public int Count { get; set; } = 100;

        public override void Run()
        {
            var resultValue = new Random().Next();
            for (var i = 0; i < Count; i++)
            {
                Results.Publish(new ResultRow
                {
                    ResultName = $"{this.StepRun.TestStepId}",
                    ResultValue = resultValue
                });
            }
        }
    }
    public class PublishResultsSameValues2 : TestStep
    {

        public int Count { get; set; } = 100;
        public override void Run()
        {
            var rnd = new Random();
            var resultValue = rnd.Next();
            var resultValue2 = rnd.NextDouble();
            
            for (var i = 0; i < Count; i++)
                Results.Publish("Test", new List<string> { "X", "Y" }, resultValue, resultValue2);   
        }
    }

    [TestFixture]
    public class Results2
    {
        [TestCase(typeof(PublishResultsSameValues))]
        [TestCase(typeof(PublishResultsSameValues2))]
        public void TestManyResults(Type stepType)
        {
            // this test has been a bit unstable. Repeat a few times to make sure it works.
            for (int i = 0; i < 5; i++) 
            {
                var record = new RecordAllResultListener();
                var plan = new TestPlan();
                var repeat = new RepeatStep
                {
                    Count = 10
                };
                var parallel = new ParallelStep();

                plan.ChildTestSteps.Add(repeat);

                var results1 = (TestStep)Activator.CreateInstance(stepType);
                var results2 = (TestStep)Activator.CreateInstance(stepType);
                parallel.ChildTestSteps.Add(results1);
                parallel.ChildTestSteps.Add(results2);

                repeat.ChildTestSteps.Add(parallel);

                var run = plan.Execute(new IResultListener[]
                {
                    record
                });
                Assert.AreEqual(Verdict.NotSet, run.Verdict);
                foreach (var table in record.Results)
                {
                    foreach (var column in table.Columns)
                    {
                        var distinctCount = column.Data.Cast<object>().Distinct().Count();
                        Assert.AreEqual(1, distinctCount);
                    }
                }
            }
        }
        
        [TestCase(10, 10)]
        [TestCase(50, 50)]
        [TestCase(100, 100)]
        public void TestSetParameterRaceCondition(int threadCount, int paramsPerThread)
        {
            var step = new DelayStep();
            var run = new TestStepRun(step, Guid.NewGuid(), Array.Empty<ResultParameter>());
            var threads = new Task[threadCount];
            var evt = new ManualResetEventSlim(false);
            
            for (int i = 0; i < threadCount; i++)
            {
                int i2 = i;
                threads[i2] = TapThread.StartAwaitable(() =>
                {
                    evt.Wait();
                    int start = i2 * paramsPerThread;
                    foreach (var i1 in Enumerable.Range(start, paramsPerThread))
                    {
                        run.Parameters[i1.ToString()] = i1;
                    }
                });
            }
            
            // Ensure all the threads are started and waiting on the event
            TapThread.Sleep(1000); 
            evt.Set();
            Task.WaitAll(threads);
            
            for (int i = 0; i < threadCount * paramsPerThread; i++)
            {
                var param = run.Parameters[i.ToString()];
                Assert.AreEqual(param, i);
            }
        }

        class ParameterAssigningResultListener : ResultListener
        {
            public List<string> Assignments = new List<string>();
            public override void OnTestStepRunStart(TestStepRun stepRun)
            {
                base.OnTestStepRunStart(stepRun);
                foreach (var assignment in Assignments)
                {
                    stepRun.Parameters[assignment] = assignment;
                }
            }
        }

        [Test]
        public void TestRacyResultListeners()
        {
            using var session = Session.Create(SessionOptions.OverlayComponentSettings);
            var collector = new PlanRunCollectorListener();
            var a1 = new ParameterAssigningResultListener() { Assignments = { "Param1", "Param2", "Param3" } };
            var a2 = new ParameterAssigningResultListener() { Assignments = { "Param4", "Param5", "Param6" } };
            ResultSettings.Current.Add(a1);
            ResultSettings.Current.Add(a2);
            ResultSettings.Current.Add(collector);

            var plan = new TestPlan();
            plan.ChildTestSteps.Add(new DelayStep());
            plan.Execute();

            Assert.AreEqual(collector.StepRunStartEvents[0].Verdict, Verdict.NotSet);
        }
    }
}
