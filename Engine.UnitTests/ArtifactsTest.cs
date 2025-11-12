using System;
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
            Assert.AreEqual(4, run.Artifacts.Count());
            Assert.IsTrue(run.Artifacts.Contains(file));
            Assert.IsTrue(run.Artifacts.Contains(file2));
            Assert.IsTrue(run.Artifacts.Any(x => Path.GetExtension(x) == ".zip"));
        }

        public class ArtifactMemoryStreamStep : TestStep
        {
            public byte[] payload { get; set; }
            public override void Run()
            {
                var ms = new MemoryStream(payload);
                ms.Seek(0, SeekOrigin.Begin);
                this.PlanRun.PublishArtifact(ms, "MemoryStream Artifact");
                UpgradeVerdict(Verdict.Pass);
            }
        }

        class MemoryStreamArtifactListener : ResultListener, IArtifactListener
        {
            public byte[] ExpectedPayload { get; set; }
            public byte[] ActualPayload { get; set; }
            public void OnArtifactPublished(TestRun run, Stream artifactStream, string artifactName)
            {
                var ms = new MemoryStream();
                artifactStream.CopyTo(ms);
                ActualPayload = ms.ToArray();
            }
        }
        
        [Test]
        public void ArtifactMemoryStreamTest([Values(1, 2, 4, 8)] int concurrentListeners, [Values(1, 1 << 10, 1 << 15, 1 << 20)] int streamLength)
        {
            byte[] payload = new byte[streamLength];
            var rand = new Random();
            rand.NextBytes(payload);
            var step = new ArtifactMemoryStreamStep()
            {
                payload = payload,
            };
            var listeners = Enumerable.Range(0, concurrentListeners)
                .Select(x => new MemoryStreamArtifactListener() { ExpectedPayload = step.payload }).ToArray();
            var plan = new TestPlan()
            {
                ChildTestSteps = { step },
            };
            var run = plan.Execute(listeners);
            Assert.That(run.Verdict, Is.EqualTo(Verdict.Pass));
            foreach (var rl in listeners)
                Assert.That(rl.ExpectedPayload, Is.EqualTo(rl.ActualPayload).AsCollection);
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
            Assert.AreEqual(4, run.Artifacts.Count());
        }
    }
}