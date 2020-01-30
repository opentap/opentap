using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    public class AnnotationTest
    {
        [Test]
        public void TestPlanReferenceNameTest()
        {
            // var delay = new DelayStep();
            // var delayName = AnnotationCollection.Annotate(delay).Get<IStringReadOnlyValueAnnotation>()?.Value;
            // Assert.AreEqual(delay.Name, delayName);
            
            var testPlanReference = new TestPlanReference();
            var testPlanReferenceName = AnnotationCollection.Annotate(testPlanReference).Get<IStringReadOnlyValueAnnotation>()?.Value;
            Assert.AreEqual(testPlanReference.Name, testPlanReferenceName);
        }
    }
}