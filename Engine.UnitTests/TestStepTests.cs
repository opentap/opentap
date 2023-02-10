using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class TestStepTests
    {
        [Test]
        public void FormattedName()
        {
            var delay = new DelayStep() {DelaySecs = 0.1, Name = "Delay: {Time Delay}"};
            var formattedName = delay.GetFormattedName();
            Assert.That(formattedName, Does.Match(@"Delay: 0(\.|,)1 s"));
        }

        [Test]
        public void AnnotatedFormattedName()
        {
            // both annotating the step itself and TestStep.Name should give the same GetFormatted read-only string.
            
            var delay = new DelayStep() {DelaySecs = 0.1, Name = "Delay: {Time Delay}"};
            var annotation = AnnotationCollection.Annotate(delay);
            var formattedName = annotation.Get<IStringReadOnlyValueAnnotation>().Value;
            Assert.That(formattedName, Does.Match(@"Delay: 0(\.|,)1 s"));

            var formattedName2 = annotation.GetMember(nameof(TestStep.Name)).Get<IStringReadOnlyValueAnnotation>().Value;
            Assert.That(formattedName2, Does.Match(@"Delay: 0(\.|,)1 s"));
        }

        [Test]
        public void FormattedNameIssue()
        {
            var logStep = new LogStep() {};
            logStep.Name = "Log: {0}"; // At one point this caused a bug, but it was not because of GetFormattedName.
            var formattedName = logStep.GetFormattedName();
            Assert.AreEqual("Log: {0}", formattedName);

            var plan = new TestPlan();
            plan.ChildTestSteps.Add(logStep);
            var run = plan.Execute();
            Assert.AreEqual(Verdict.NotSet, run.Verdict);
        }
    }
}