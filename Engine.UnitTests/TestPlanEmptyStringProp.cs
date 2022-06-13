using System.IO;
using System.Linq;
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class TestPlanEmptyStringProp 
    {

        public class EmptyStringStep : TestStep
        {
            public string TestProp { get; set; }

            public EmptyStringStep()
            {
                TestProp = "Test";
            }

            public override void Run()
            {
            }
        }

        [Test]
        public void EmptyStringSerialization()
        {
            TestPlan target = new TestPlan();
            var targetStep = new EmptyStringStep { TestProp = "" };

            target.Steps.Add(targetStep);

            using (var ms = new MemoryStream())
            {
                target.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                TestPlan deserialized = TestPlan.Load(ms, target.Path);
                var step = deserialized.ChildTestSteps.First() as EmptyStringStep;

                Assert.AreNotEqual(null, step, "Expected step");
                Assert.AreEqual(targetStep.TestProp, step.TestProp);
            }
        }
    }
}