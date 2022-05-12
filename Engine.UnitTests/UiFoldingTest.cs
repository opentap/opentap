using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class UiFoldingTest
    {
        [Test]
        public void TestSerializeUiFolding()
        {
            var plan = new TestPlan();
            ITestStep step = new SequenceStep();
            plan.ChildTestSteps.Add(step);
            step.ChildTestSteps.Add(new SequenceStep());
            step.ChildTestSteps[0].ChildTestSteps.Add(new SequenceStep());
            
            Assert.IsNull(UiFolding.GetFolding(step));
            UiFolding.SetFolding(step, true);
            Assert.IsTrue(UiFolding.GetFolding(step));
            UiFolding.SetFolding(step, false);
            Assert.IsFalse(UiFolding.GetFolding(step));

            var xml = plan.SerializeToString();
            plan = (TestPlan)new TapSerializer().DeserializeFromString(xml);
            step = plan.ChildTestSteps[0];
            Assert.IsFalse(UiFolding.GetFolding(step));
            Assert.IsNull(UiFolding.GetFolding(step.ChildTestSteps[0]));
        }
    }
}