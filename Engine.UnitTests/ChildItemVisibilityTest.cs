using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class ChildItemVisibilityTest
    {
        [Test]
        public void TestSerializeChildItemVisibility()
        {
            var plan = new TestPlan();
            ITestStep step = new SequenceStep();
            plan.ChildTestSteps.Add(step);
            step.ChildTestSteps.Add(new SequenceStep());
            step.ChildTestSteps[0].ChildTestSteps.Add(new SequenceStep());
            
            Assert.AreEqual(ChildItemVisibility.Visibility.Collapsed, ChildItemVisibility.GetVisibility(step));
            ChildItemVisibility.SetVisibility(step, ChildItemVisibility.Visibility.Visible);
            Assert.IsTrue(ChildItemVisibility.GetVisibility(step) == ChildItemVisibility.Visibility.Visible);
            ChildItemVisibility.SetVisibility(step, ChildItemVisibility.Visibility.Collapsed);
            Assert.IsTrue(ChildItemVisibility.GetVisibility(step) == ChildItemVisibility.Visibility.Collapsed);
            ChildItemVisibility.SetVisibility(step, ChildItemVisibility.Visibility.Visible);

            var xml = plan.SerializeToString();
            plan = (TestPlan)new TapSerializer().DeserializeFromString(xml);
            step = plan.ChildTestSteps[0];
            Assert.AreEqual(ChildItemVisibility.Visibility.Visible, ChildItemVisibility.GetVisibility(step));
            Assert.AreEqual(ChildItemVisibility.Visibility.Collapsed, ChildItemVisibility.GetVisibility(step.ChildTestSteps[0]));
        }
    }
}