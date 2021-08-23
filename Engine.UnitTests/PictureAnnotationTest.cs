using NUnit.Framework;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class PictureAnnotationTest
    {
        [Test]
        public void AnnotationTest()
        {
            var step = new MyPictureUsingTestStep();
            var a = AnnotationCollection.Annotate(step);
            var mem = a.GetMember(nameof(step.Picture));
            
            var img = mem.Get<IPictureAnnotation>() as PictureAnnotation;
            Assert.NotNull(img);

            StringAssert.AreEqualIgnoringCase(img.Source, TestPlanDependencyTest.PictureReference);

            step.Picture.Source = "test1";
            mem.Read(step.Picture);

            StringAssert.AreEqualIgnoringCase(img.Source, "test1");

            img.Source = "test2";
            mem.Write(step.Picture);

            Assert.AreEqual(img.Source, "test2");
        }
    }
}
