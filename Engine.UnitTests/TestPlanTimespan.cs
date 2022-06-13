using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class TestPlanTimespan 
    {
        public class TimespanStep : TestStep
        {
            public TimeSpan TestProp { get; set; }

            public override void Run()
            {
            }
        }

        [Test]
        public void TimespanSerialization()
        {
            TestPlan target = new TestPlan();
            var targetStep = new TimespanStep { TestProp = TimeSpan.FromSeconds(100) };

            target.Steps.Add(targetStep);

            using (var ms = new MemoryStream())
            {
                target.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                TestPlan deserialized = TestPlan.Load(ms, null);
                var step = (TimespanStep)deserialized.ChildTestSteps.First();

                Assert.AreNotEqual(null, step, "Expected step");
                Assert.AreEqual(targetStep.TestProp, step.TestProp);
            }
        }
    }
}