using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class TapThreadParallelQueryTest
    {
        [TestCase(0)]
        [TestCase(5)]
        public void SelectIsDeferredAndReturnsResults(int count)
        {
            int calls = 0;
            var query = Enumerable.Range(0, count).AsParallelTapThread().Select(item =>
            {
                Interlocked.Increment(ref calls);
                return item * item;
            });

            Assert.AreEqual(0, calls);
            CollectionAssert.AreEquivalent(Enumerable.Range(0, count).Select(item => item * item), query.ToArray());
            Assert.AreEqual(count, calls);
        }

        [Test]
        public void SelectReevaluatesOnEachEnumeration()
        {
            int calls = 0;
            var query = new[] { 1, 2, 3 }.AsParallelTapThread().Select(item =>
            {
                Interlocked.Increment(ref calls);
                return item * item;
            });

            CollectionAssert.AreEquivalent(new[] { 1, 4, 9 }, query.ToArray());
            Assert.AreEqual(3, calls);
            CollectionAssert.AreEquivalent(new[] { 1, 4, 9 }, query.ToArray());
            Assert.AreEqual(6, calls);
        }

        [Test]
        public void TrySelectReevaluatesOnEachEnumeration()
        {
            int calls = 0;
            int failures = 0;
            var query = new[] { 0, 2 }.AsParallelTapThread().TrySelect(item =>
            {
                Interlocked.Increment(ref calls);
                return 10 / item;
            }, ex => Interlocked.Increment(ref failures));

            CollectionAssert.AreEqual(new[] { 5 }, query.ToArray());
            Assert.AreEqual(2, calls);
            Assert.AreEqual(1, failures);
            CollectionAssert.AreEqual(new[] { 5 }, query.ToArray());
            Assert.AreEqual(4, calls);
            Assert.AreEqual(2, failures);
        }

        [TestCase(0)]
        [TestCase(5)]
        public void ForAllExecutesEveryItemBeforeReturning(int count)
        {
            var results = new ConcurrentBag<int>();
            Enumerable.Range(0, count).AsParallelTapThread().ForAll(results.Add);
            CollectionAssert.AreEquivalent(Enumerable.Range(0, count), results);
        }

        [Test]
        public void SelectAbortsScheduledWorkersWhenSourceEnumerationFails()
        {
            using var started = new ManualResetEventSlim();
            using var completed = new ManualResetEventSlim();
            bool cancelled = false;
            var sourceFailure = new InvalidOperationException("Source failed.");

            System.Collections.Generic.IEnumerable<int> Source()
            {
                yield return 1;
                Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(10)), "Worker did not start.");
                throw sourceFailure;
            }

            var query = Source().AsParallelTapThread().Select(item =>
            {
                var token = TapThread.Current.AbortToken;
                started.Set();
                try
                {
                    cancelled = token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                    return item;
                }
                finally
                {
                    completed.Set();
                }
            });

            Assert.AreSame(sourceFailure, Assert.Throws<InvalidOperationException>(() => query.ToArray()));
            Assert.IsTrue(completed.Wait(TimeSpan.FromSeconds(10)), "Worker did not finish.");
            Assert.IsTrue(cancelled, "Worker did not receive cancellation.");
            Assert.IsFalse(TapThread.Current.AbortToken.IsCancellationRequested, "Caller should not be aborted.");
        }

        [Test]
        public void SelectAggregatesWorkerExceptions()
        {
            var query = new[] { 1, 2 }.AsParallelTapThread().Select<int>(item =>
                throw new InvalidOperationException(item.ToString()));

            var exception = Assert.Throws<AggregateException>(() => query.ToArray());
            CollectionAssert.AreEquivalent(new[] { "1", "2" },
                exception.Flatten().InnerExceptions.Select(ex => ex.Message));
        }

        [Test]
        public void ForAllAggregatesWorkerExceptions()
        {
            int calls = 0;
            var exception = Assert.Throws<AggregateException>(() =>
                new[] { 1, 2 }.AsParallelTapThread().ForAll(item =>
                {
                    Interlocked.Increment(ref calls);
                    throw new InvalidOperationException(item.ToString());
                }));

            Assert.AreEqual(2, calls);
            Assert.AreEqual(2, exception.Flatten().InnerExceptions.Count);
        }

        [Test]
        public void SelectInheritsCallerContextAndIsolatesWorkerChanges()
        {
            var field = new ThreadField<object>();
            var originalThread = TapThread.Current;
            TapThread.WithNewContext(() =>
            {
                var parent = TapThread.Current;
                var value = new object();
                field.Value = value;
                var results = new[] { 1, 2 }.AsParallelTapThread().Select(item =>
                {
                    Assert.AreSame(parent, TapThread.Current.Parent);
                    Assert.AreSame(value, field.Value);
                    field.Value = new object();
                    return item;
                }).ToArray();

                CollectionAssert.AreEquivalent(new[] { 1, 2 }, results);
                Assert.AreSame(value, field.Value);
                Assert.AreSame(parent, TapThread.Current);
            });
            Assert.AreSame(originalThread, TapThread.Current);
        }

        [Test]
        public void TrySelectRunsConcurrentlyAndHandlesFailuresInWorkerContext()
        {
            var parent = TapThread.Current;
            var handled = new ConcurrentBag<int>();
            using var started = new CountdownEvent(2);
            var query = new[] { 0, 2 }.AsParallelTapThread().TrySelect(item =>
            {
                Assert.AreSame(parent, TapThread.Current.Parent);
                started.Signal();
                Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(10)), "Selectors did not run concurrently.");
                return 10 / item;
            }, (ex, item) =>
            {
                Assert.AreSame(parent, TapThread.Current.Parent);
                Assert.IsInstanceOf<DivideByZeroException>(ex);
                handled.Add(item);
            });

            Assert.AreEqual(2, started.CurrentCount, "TrySelect should be deferred.");
            CollectionAssert.AreEqual(new[] { 5 }, query.ToArray());
            CollectionAssert.AreEqual(new[] { 0 }, handled);
        }

        [Test]
        public void TrySelectExceptionOnlyHandlerPreservesSuccessfulDefaults()
        {
            int failures = 0;
            var results = new[] { 0, 1, 2 }.AsParallelTapThread().TrySelect(item =>
            {
                if (item == 1)
                    throw new InvalidOperationException();
                return 0;
            }, ex => Interlocked.Increment(ref failures)).ToArray();

            CollectionAssert.AreEqual(new[] { 0, 0 }, results);
            Assert.AreEqual(1, failures);
        }

        [Test]
        public void TrySelectHandlesEmptySource()
        {
            var results = Array.Empty<int>().AsParallelTapThread().TrySelect(item => item,
                ex => Assert.Fail("No exception should be handled.")).ToArray();
            Assert.IsEmpty(results);
        }

        [Test]
        public void TrySelectTypedHandlerPropagatesUnmatchedExceptions()
        {
            int failures = 0;
            var query = new[] { 0, 1 }.AsParallelTapThread().TrySelect<int, DivideByZeroException>(item =>
            {
                if (item == 1)
                    throw new InvalidOperationException("unmatched");
                return 10 / item;
            }, (ex, item) => Interlocked.Increment(ref failures));

            var exception = Assert.Throws<AggregateException>(() => query.ToArray());
            Assert.AreEqual(1, failures);
            Assert.IsInstanceOf<InvalidOperationException>(exception.Flatten().InnerExceptions.Single());
        }

        [Test]
        public void TrySelectPropagatesHandlerExceptions()
        {
            var query = new[] { 0 }.AsParallelTapThread().TrySelect(item => 10 / item,
                (ex, item) => throw new InvalidOperationException("handler failed"));

            var exception = Assert.Throws<AggregateException>(() => query.ToArray());
            Assert.AreEqual("handler failed", exception.Flatten().InnerExceptions.Single().Message);
        }
    }
}
