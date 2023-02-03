using System.IO;
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class TestPlanTestFixture2
    {
        public class TestPlanTestStep : TestStep
        {
            public class TestPlanTestStep2 : TestStep
            {
                public override void Run()
                {
                }
            }

            public override void Run()
            {
            }
        }

        [Test]
        public void TestPlanSameTestStepNameTest2()
        {
            TestPlan target = new TestPlan();

            target.Steps.Add(new TestPlanTestStep());
            target.Steps.Add(new TestPlanTestStep.TestPlanTestStep2());

            using (var ms = new MemoryStream())
            {
                target.Save(ms);
                Assert.AreNotEqual(0, ms.Length);
            }
        }
    }
}