using System.ComponentModel;
using NUnit.Framework;
namespace OpenTap.UnitTests
{
    [TestFixture]
    public class TestStepListTest
    {
        
        public class ReplaceMyselfStep : TestStep
        {
            [Display("Replaced Times")]
            public int ReplacedTimes { get; set; }

            [Browsable(true)]
            public void ReplaceMe2()
            {
                ReplaceMe();
            }
            
            
            public ReplaceMyselfStep ReplaceMe()
            {
                var newStep = new ReplaceMyselfStep()
                {
                    Name = this.Name,
                    ReplacedTimes = ReplacedTimes + 1,
                    Id = Id,
                    Parent = Parent
                
                };
                var parent = Parent;
                var idx = parent.ChildTestSteps.IndexOf(this);
                parent.ChildTestSteps[idx] = newStep;
                return newStep;
            }
        
            public override void Run()
            {
            
            }
        }
        
        [Test]
        public void TestEvents()
        {
            var plan = new TestPlan();
            var step = new ReplaceMyselfStep();
            plan.ChildTestSteps.Add(step);
            var step2 = new ReplaceMyselfStep();
            plan.ChildTestSteps.Add(step2);

            plan.ChildTestSteps.ChildStepsChanged += ChildTestStepsOnChildStepsChanged;
            bool stepsChanged = false;
            void ChildTestStepsOnChildStepsChanged(TestStepList senderlist, TestStepList.ChildStepsChangedAction action, ITestStep testStep, int index)
            {
                Assert.AreEqual(plan.ChildTestSteps, senderlist);
                stepsChanged = true;
                Assert.AreEqual(TestStepList.ChildStepsChangedAction.SetStep, action);
                Assert.AreEqual(1, index);
                Assert.IsTrue(testStep == senderlist[index]);
            }
            step2 = step2.ReplaceMe();

            Assert.IsTrue(stepsChanged);
            plan.ChildTestSteps.ChildStepsChanged -= ChildTestStepsOnChildStepsChanged;
            
            plan.ChildTestSteps.ChildStepsChanged += ChildTestStepsOnChildStepsChanged2;
            
            bool stepsChanged2 = false;
            void ChildTestStepsOnChildStepsChanged2(TestStepList senderlist, TestStepList.ChildStepsChangedAction action, ITestStep testStep, int index)
            {
                Assert.AreEqual(plan.ChildTestSteps, senderlist);
                stepsChanged2 = true;
                Assert.AreEqual(TestStepList.ChildStepsChangedAction.MovedStep, action);
                Assert.AreEqual(1, index);
                Assert.IsTrue(testStep == senderlist[index]);
                Assert.IsTrue(step2 == senderlist[0]);
                Assert.IsTrue(step == senderlist[1]);
            }
            plan.ChildTestSteps.Move(0,1);
            Assert.IsTrue(stepsChanged2);
        }
        
    }
}
