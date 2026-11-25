using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class NamedMutexTest
    {
        public class MutexUsingTestStep : TestStep
        {
            public string MutexName { get; set; }
            public bool AlreadyLocked { get; set; }
            public override void Run()
            {
                using var mut = FileLock.Create(MutexName);
                if (AlreadyLocked)
                {
                    if (mut.WaitOne(0))
                        UpgradeVerdict(Verdict.Fail);
                    else
                        UpgradeVerdict(Verdict.Pass);
                }
                else
                {
                    if (mut.WaitOne(0) == false)
                        UpgradeVerdict(Verdict.Fail);
                    else 
                        UpgradeVerdict(Verdict.Pass);
                }
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestExclusiveAccessAcrossProcesses(bool alreadyLocked)
        {
            var mutexName = Path.GetTempFileName();
            File.Delete(mutexName);
            using (var mut = FileLock.Create(mutexName))
            {
                var step = new MutexUsingTestStep() { MutexName = mutexName, AlreadyLocked = alreadyLocked };
                var processRunner = new SubProcessHost();

                if (alreadyLocked)
                {
                    Assert.IsTrue(mut.WaitOne(0), "Failed initially getting the mutex.");
                }

                var result = processRunner.Run(step, false, CancellationToken.None);
                var msg = alreadyLocked ? "Expected the mutex to be locked." : "Expected the mutex to be available.";
                Assert.AreEqual(Verdict.Pass, result, msg);
            }
        }

        [Test]
        [Platform("Win")]
        public void DisposeReleasesWindowsMutexWithoutAbandoningIt()
        {
            var name = Path.Combine(Path.GetTempPath(), "DisposeLock-" + Guid.NewGuid().ToString("N"));
            using var owner = FileLock.Create(name);
            using var waiter = FileLock.Create(name);
            Assert.IsTrue(owner.WaitOne(0));
            using var attempted = new ManualResetEventSlim();
            var acquisition = Task.Run(() =>
            {
                Assert.IsFalse(waiter.WaitOne(0));
                attempted.Set();
                // An abandoned mutex throws instead of returning true.
                Assert.IsTrue(waiter.WaitOne(TimeSpan.FromSeconds(5), CancellationToken.None));
                waiter.Release();
            });
            Assert.IsTrue(attempted.Wait(5000));
            owner.Dispose();
            Assert.IsTrue(acquisition.Wait(10000), "Disposal left the mutex owned by the original thread.");
            owner.Dispose();
        }

        [Test]
        public void TestExclusiveAccess()
        {
            var outerEvent = new ManualResetEventSlim(false);
            var innerEvent = new ManualResetEventSlim(false);
            var mutexName = Path.GetTempFileName();
            File.Delete(mutexName);

            using var innerMutex = FileLock.Create(mutexName);
            // Manually disposed later
            var outerMutex = FileLock.Create(mutexName);

            { // Verify the outer mutex works
                Assert.IsTrue(outerMutex.WaitOne(0));
                outerMutex.Release();
                Assert.IsTrue(outerMutex.WaitOne(0));
                outerMutex.Release();
            }

            Task.Run(() =>
            {
                Assert.IsTrue(innerMutex.WaitOne());
                innerEvent.Set();
                outerEvent.Wait();
                // Sleep for a "long" time before releasing
                TapThread.Sleep(TimeSpan.FromSeconds(1));
                innerMutex.Release();
            });

            // Wait for the inner mutex to be acquired
            innerEvent.Wait();
            // Verify this locks the outer mutex
            Assert.IsFalse(outerMutex.WaitOne(0));
            // Signal the thread to release the inner mutex
            var sw = Stopwatch.StartNew();
            var limit = TimeSpan.FromSeconds(2);
            outerEvent.Set();
            // Wait for the inner mutex to be released. Also test that timeouts work
            Assert.IsTrue(outerMutex.WaitOne(limit));
            Assert.IsTrue(sw.Elapsed < limit, "Timeout took longer than expected!");
            // Verify that disposing the mutex releases the lock
            outerMutex.Dispose();
            Assert.IsTrue(innerMutex.WaitOne(0));
        }
    }
}
