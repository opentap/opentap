using System.Linq;
using NUnit.Framework;

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
    }
}