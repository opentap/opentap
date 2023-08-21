using System.IO;
using NUnit.Framework;
namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class ArtifactsTest 
    {
        [Test]
        public void ZippingLogFileTest()
        {
            var zip = new ArtifactZipper();
            var txt = new LogResultListener();
            var step = new ArtifactStep();

            var plan = new TestPlan();
            plan.ChildTestSteps.Add(step);

            var file = "test-artifact.txt";
            File.WriteAllText(file, "test" + new string('a', 1024 * 1024) + "test");
            step.File = file;
            
            var run = plan.Execute(new IResultListener[]{zip, txt});
            Assert.AreEqual(3, run.PublishedArtifacts.Count());

        }
        
    }
}
