using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class AllowChildTest
    {
        
        private class BaseAutomationStep : TestStep
        {
            public override void Run()
            {
            }
        }

        [AllowAsChildIn(typeof(BaseAutomationStep))]
        private class AutomationStep : BaseAutomationStep
        {
        }

        [AllowAsChildIn(typeof(BaseAutomationStep))]
        [AllowAnyChild]
        private class LoopStep : BaseAutomationStep
        {
        }

        [Test]
        public void TestAllowChildren()
        {
            LoopStep step = new LoopStep();
            step.ChildTestSteps.Add(new AutomationStep());
        }

        [Test]
        public void TestGuid()
        {
            var a = new DelayStep();
            var b = new DelayStep();
            Assert.AreNotEqual(a.Id, b.Id);
        }

        public interface IBaseParentStep : ITestStep
        {
            
        }
        
        [AllowChildrenOfType(typeof(DelayStep))]
        private class BaseParentStep : TestStep, IBaseParentStep
        {
            public override void Run() {  }
        }

        class InheritedParentStep : BaseParentStep
        {
            
        }
        
        [AllowAsChildIn(typeof(IBaseParentStep))]
        
        public class InsideInterfaceStep : TestStep
        {
            public override void Run() { }
        }
        public class InsideInterfaceStep2 : InsideInterfaceStep
        {
            
        }

        [Test]
        public void TestInterfaceParentStep()
        {
            var parent = new InheritedParentStep();
            var child = new InsideInterfaceStep2();
            
            // this should not throw exceptions:
            parent.ChildTestSteps.Add(child);
            parent.ChildTestSteps.Add(new DelayStep());
        }
    }
}