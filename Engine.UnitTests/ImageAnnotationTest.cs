using NUnit.Framework;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class ImageAnnotationTest
    {
        [Test]
        public void AnnotationTest()
        {
            var step = new MyImageUsingTestStep();
            var a = AnnotationCollection.Annotate(step);
            var mem = a.GetMember(nameof(step.Image));
            var img = mem.Get<IImageAnnotation>();

            StringAssert.AreEqualIgnoringCase(img.ImageSource, TestPlanDependencyTest.ImageReference);
            StringAssert.AreEqualIgnoringCase(img.Description, TestPlanDependencyTest.ImageDescription);

            step.Image.Description = "test1";
            mem.Read(step.Image);
            

            StringAssert.AreEqualIgnoringCase(img.Description, "test1");

            (img as ImageAnnotation).Description = "test2";
            mem.Write(step.Image);

            Assert.AreEqual(step.Image.Description, "test2");
        }
    }
}