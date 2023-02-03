using System.IO;
using System.Linq;
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class TestPlanTestFixture3 
    {


        [AllowAnyChild]
        public class TestPlanTestStep : TestStep
        {
            public override void Run()
            {
            }
        }

        [Test]
        public void ChildStepSerialization()
        {
            TestPlan target = new TestPlan();

            ITestStep step = new TestPlanTestStep();
            target.Steps.Add(step);
            step.ChildTestSteps.Add(new TestPlanTestStep());

            using (var ms = new MemoryStream())
            {
                target.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                TestPlan deserialized = TestPlan.Load(ms, target.Path);

                Assert.AreEqual(1, deserialized.ChildTestSteps.Count);
                Assert.AreEqual(1, deserialized.ChildTestSteps.First().ChildTestSteps.Count);

                Assert.IsTrue(deserialized.ChildTestSteps.First() is TestPlanTestStep);
                Assert.IsTrue(deserialized.ChildTestSteps.First().ChildTestSteps.First() is TestPlanTestStep);
            }
        }
    }
}