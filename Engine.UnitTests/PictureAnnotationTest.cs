using NUnit.Framework;

namespace OpenTap.UnitTests
{
    [Display("Some test step using pictures")]
    public class MyPictureUsingTestStep : TestStep
    {
        public Picture Picture { get; set; } = new Picture()
            {Source = "Some Picture", Description = "Initial Description"};

        public override void Run()
        {

        }
    }
    
    [TestFixture]
    public class PictureAnnotationTest
    {
        [Test]
        public void PictureReadWriteTest()
        {
            var step = new MyPictureUsingTestStep();
            var a = AnnotationCollection.Annotate(step);
            var mem = a.GetMember(nameof(step.Picture));
            var pic = mem.Get<IPictureAnnotation>() as PictureAnnotation;

            Assert.AreEqual("Some Picture", pic.Source);
            Assert.AreEqual("Initial Description", pic.Description);

            { // Test read
                step.Picture.Source = "New Picture";
                step.Picture.Description = "New Description";
                
                Assert.AreEqual("Some Picture", pic.Source);
                Assert.AreEqual("Initial Description", pic.Description);
                
                a.Read();
                
                Assert.AreEqual("New Picture", pic.Source);
                Assert.AreEqual("New Description", pic.Description);
            }

            { // Test write
                pic.Source = "Final Picture";
                pic.Description = "Final Description";
                
                Assert.AreEqual("New Picture", step.Picture.Source);
                Assert.AreEqual("New Description", step.Picture.Description);
                
                a.Write();
                
                Assert.AreEqual("Final Picture", step.Picture.Source);
                Assert.AreEqual("Final Description", step.Picture.Description);
            }
        }
    }
}
