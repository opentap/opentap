using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;
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
            var step2 = new ArtifactStep { AsStream = true };

            var plan = new TestPlan();
            plan.ChildTestSteps.Add(step);
            plan.ChildTestSteps.Add(step2);

            var file = "test-artifact.txt";
            File.WriteAllText(file, "test" + new string('a', 1024 * 1024) + "test");
            var file2 = "test-artifact2.txt";
            File.WriteAllText(file2, "test" + new string('a', 1024 * 1024) + "test");
            step.File = file;
            step2.File = file2;
            
            var run = plan.Execute(new IResultListener[]{zip, txt});
            Assert.AreEqual(4, run.PublishedArtifacts.Count());
            Assert.IsTrue(run.PublishedArtifacts.Contains(file));
            Assert.IsTrue(run.PublishedArtifacts.Contains(file2));
            Assert.IsTrue(run.PublishedArtifacts.Any(x => Path.GetExtension(x) == ".zip"));
        }
        
        [Test]
        public void ZippingLogFileTestParallel()
        {
            var zip = new ArtifactZipper();
            var txt = new LogResultListener();
            var parallel = new ParallelStep();
            
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(parallel);

            var file = "test-artifact.txt";
            File.WriteAllText(file, "test" + new string('a', 1024 * 1024) + "test");
            var file2 = "test-artifact2.txt";
            File.WriteAllText(file2, "test" + new string('a', 1024 * 1024) + "test");
            
            for (int i = 0; i < 4; i++)
            {
                var step = new ArtifactStep();
                var step2 = new ArtifactStep { AsStream = true };
                parallel.ChildTestSteps.Add(step);
                parallel.ChildTestSteps.Add(step2);
                step.File = file;
                step2.File = file2;
            }
            
            var run = plan.Execute(new IResultListener[]{zip, txt});
            Assert.AreEqual(4, run.PublishedArtifacts.Count());
        }
    }
}
