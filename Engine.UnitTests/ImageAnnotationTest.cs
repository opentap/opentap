using NUnit.Framework;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class ImageAnnotationTest
    {
        [Test]
        public void AnnotationTest()
        {
            var step = new MyPictureUsingTestStep();
            var a = AnnotationCollection.Annotate(step);
            var mem = a.GetMember(nameof(step.Picture));
            var img = mem.Get<IImageAnnotation>();

            StringAssert.AreEqualIgnoringCase(img.Source, TestPlanDependencyTest.PictureReference);
            StringAssert.AreEqualIgnoringCase(img.Description, TestPlanDependencyTest.PictureDescription);

            step.Picture.Description = "test1";
            mem.Read(step.Picture);
            

            StringAssert.AreEqualIgnoringCase(img.Description, "test1");

            (img as ImageAnnotation).Description = "test2";
            mem.Write(step.Picture);

            Assert.AreEqual(step.Picture.Description, "test2");
        }
    }
}