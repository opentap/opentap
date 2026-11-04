using System.Linq;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    public class RepeatStepAnnotationTests
    {
        [AllowAnyChild]
        public class StepSelectorTestStep : TestStep
        {
            [StepSelector(StepSelectorAttribute.FilterTypes.All)]
            public ITestStep All { get; set; }

            [StepSelector(StepSelectorAttribute.FilterTypes.AllExcludingSelf)]
            public ITestStep AllExcludingSelf { get; set; }

            [StepSelector(StepSelectorAttribute.FilterTypes.Children)]
            public ITestStep Children { get; set; }

            [StepSelector(StepSelectorAttribute.FilterTypes.Sibling)]
            public ITestStep Sibling { get; set; }

            public override void Run() { }
        }

        [TestCase(nameof(StepSelectorTestStep.All), false)]
        [TestCase(nameof(StepSelectorTestStep.All), true)]
        [TestCase(nameof(StepSelectorTestStep.AllExcludingSelf), false)]
        [TestCase(nameof(StepSelectorTestStep.AllExcludingSelf), true)]
        [TestCase(nameof(StepSelectorTestStep.Children), false)]
        [TestCase(nameof(StepSelectorTestStep.Children), true)]
        [TestCase(nameof(StepSelectorTestStep.Sibling), false)]
        [TestCase(nameof(StepSelectorTestStep.Sibling), true)]
        public void StepSelectorAvailableValuesHandleMissingParent(string memberName, bool attachedToPlan)
        {
            var step = new StepSelectorTestStep();
            var child = new DelayStep();
            var sibling = new DelayStep();
            step.ChildTestSteps.Add(child);
            if (attachedToPlan)
            {
                var plan = new TestPlan();
                plan.ChildTestSteps.Add(step);
                plan.ChildTestSteps.Add(sibling);
            }

            var annotations = AnnotationCollection.Annotate(step);
            var member = annotations.Get<IMembersAnnotation>().Members
                .Single(item => item.Get<IMemberAnnotation>().Member.Name == memberName);
            var values = member.Get<IAvailableValuesAnnotationProxy>().AvailableValues
                .Select(item => item.Get<IObjectValueAnnotation>().Value).ToArray();

            object[] expected;
            if (memberName == nameof(StepSelectorTestStep.Children))
                expected = new object[] { child };
            else if (!attachedToPlan)
                expected = new object[0];
            else if (memberName == nameof(StepSelectorTestStep.Sibling))
                expected = new object[] { sibling };
            else if (memberName == nameof(StepSelectorTestStep.AllExcludingSelf))
                expected = new object[] { child, sibling };
            else
                expected = new object[] { step, child, sibling };

            Assert.That(values, Is.EquivalentTo(expected));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TargetStepAvailableValuesCanBeReadBeforeAddingRepeatToPlan(bool attachedToPlan)
        {
            // Reproduce opentap/opentap#2532 without the AI tools: a newly created
            // Repeat is annotated before it is inserted into a test plan.
            var repeat = new RepeatStep { Count = 2 };
            var child = new DelayStep();
            repeat.ChildTestSteps.Add(child);
            if (attachedToPlan)
            {
                var plan = new TestPlan();
                plan.ChildTestSteps.Add(repeat);
            }

            var annotations = AnnotationCollection.Annotate(repeat);
            var targetStep = annotations.Get<IMembersAnnotation>().Members
                .Single(member => member.Get<IMemberAnnotation>().Member.Name == nameof(RepeatStep.TargetStep));
            var availableValues = targetStep.Get<IAvailableValuesAnnotationProxy>();
            Assert.That(availableValues, Is.Not.Null);

            AnnotationCollection[] values = null;
            Assert.DoesNotThrow(() => values = availableValues.AvailableValues.ToArray());
            if (attachedToPlan)
            {
                Assert.That(values.Select(value => value.Get<IObjectValueAnnotation>().Value),
                    Does.Contain(child));
            }
            else
            {
                Assert.That(values, Is.Empty);
            }
        }
    }
}
