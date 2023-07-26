using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
            for (int i = 0; i < 100; i++)
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
    }
}