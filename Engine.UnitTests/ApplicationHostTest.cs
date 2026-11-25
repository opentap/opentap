using System;
using System.Reflection;
using System.IO;
using System.Runtime.Loader;
using System.Threading;
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class ApplicationHostTest
    {
        [Test]
        public void LogicalEntryAssemblyFallsBackWithoutApplicationHost()
        {
            Assert.AreSame(Assembly.GetEntryAssembly(), ApplicationContext.GetEntryAssembly());
        }

        [Test]
        public void ReloadsApplicationAndEngineAndReturnsResult()
        {
            var context = OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(ApplicationHostEntry), nameof(ApplicationHostEntry.Run)).WithArgument("payload").Build());
            var result = (object[])context.Result;
            try
            {
                Assert.AreEqual("payload", result[0]);
                Assert.AreEqual(typeof(ApplicationHostEntry).Assembly.Location, result[1]);
                Assert.AreEqual("OpenTAP", result[2]);
                Assert.AreEqual(false, result[3], "The entry must execute against a second engine copy.");
            }
            finally { context.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => { var ignored = context.Result; });
            Assert.AreEqual("payload", result[0], "A result explicitly retained by the caller remains usable.");
            context.Dispose(); // idempotent
        }

        [Test]
        public void EntryFailureIsUnwrappedAndNullInputAndOutputAreSupported()
        {
            Assert.Throws<InvalidOperationException>(() =>
                OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(ApplicationHostEntry), nameof(ApplicationHostEntry.Fail)).WithArgument(null).Build()));
            using (var context = OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(ApplicationHostEntry), nameof(ApplicationHostEntry.Echo)).WithArgument(null).Build()))
                Assert.IsNull(context.Result);
        }

        [Test]
        public void ObjectArrayInputIsPassedAsOneArgumentWithoutConversion()
        {
            var input = new object[] { "payload", 42 };
            using (var context = OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(ApplicationHostEntry), nameof(ApplicationHostEntry.Echo)).WithArgument(input).Build()))
                Assert.AreSame(input, context.Result);
        }

        [Test]
        public void ValueTupleArraysCanBeCastAndUsedByCaller()
        {
            (string operation, string package)[] commands;
            using (var context = OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(ApplicationHostEntry), nameof(ApplicationHostEntry.Commands)).WithArgument(null).Build()))
            {
                var result = (object[])context.Result;
                Assert.AreEqual("restart-session", result[0]);
                commands = ((string operation, string package)[])result[1];
                Assert.AreEqual(("remove", "CSV:9.13.0"), commands[0]);
                Assert.AreEqual(("add", "CSV:9.14.0"), commands[1]);
                Assert.AreEqual(typeof((string, string)[]), commands.GetType());
                Assert.AreEqual(AssemblyLoadContext.Default,
                    AssemblyLoadContext.GetLoadContext(commands[0].GetType().Assembly));
            }
            Assert.AreEqual("CSV:9.14.0", commands[1].package);
        }

        [Test]
        public void ReferenceTupleArraysCanBeCastAndUsedByCaller()
        {
            using (var context = OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(ApplicationHostEntry), nameof(ApplicationHostEntry.ReferenceTuples)).WithArgument(null).Build()))
            {
                var result = (Tuple<string, string>[])context.Result;
                Assert.AreEqual("remove", result[0].Item1);
                Assert.AreEqual("CSV:9.13.0", result[0].Item2);
            }
        }

        [Test]
        public void NestedHostsRestoreOuterContextAndRequestUnloading()
        {
            int unloading = 0;
            Action onUnload = () => Interlocked.Increment(ref unloading);
            using (var context = OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(ApplicationHostEntry), nameof(ApplicationHostEntry.Nested)).WithArgument(onUnload).Build()))
            {
                Assert.AreEqual("outer-resumed", context.Result);
                Assert.AreEqual(1, unloading, "Inner disposal must request unload while outer remains alive.");
            }
            Assert.AreEqual(2, unloading, "Outer disposal must also request unload.");
        }

        [Test]
        public void NestedHostsExecuteTestPlansAndDeliverResults()
        {
            // Each entry validates Pass, both step completions and the published
            // measurement before returning. Collection is a separate requirement.
            var results = RunTestPlanHosts();
            CollectionAssert.AreEqual(new[] { "outer-plan-passed", "inner-plan-passed" }, results);
        }

        static string[] RunTestPlanHosts()
        {
            // Do not discover NUnit/test-runner plugins in the shared bin/Debug
            // tree: their process-wide registrations are outside the engine lifecycle.
            var directory = Path.Combine(Path.GetTempPath(), "OpenTapPlanHost-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var source = Path.GetDirectoryName(typeof(ApplicationContext).Assembly.Location);
            foreach (var file in new[] { "OpenTap.dll", "OpenTap.Runtime.dll", "OpenTap.Plugins.BasicSteps.dll" })
                File.Copy(Path.Combine(source, file), Path.Combine(directory, file));
            try
            {
                using (var host = OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(ApplicationHostEntry), nameof(ApplicationHostEntry.ExecutePlan)).WithArgument("outer").WithBaseDirectory(directory).Build()))
                    return (string[])host.Result;
            }
            finally
            {
                // Files may still be locked until collection on Windows.
                try { Directory.Delete(directory, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        [Test]
        public void DisposalNotifiesOnlyItsInstanceAndCancelsCooperativeChildren()
        {
            int notified = 0, cancelled = 0;
            var input = new object[] { (Action)(() => Interlocked.Increment(ref notified)),
                (Action)(() => Interlocked.Increment(ref cancelled)), false };
            var first = OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(ApplicationHostEntry), nameof(ApplicationHostEntry.Lifecycle)).WithArgument(input).Build());
            var second = OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(ApplicationHostEntry), nameof(ApplicationHostEntry.Lifecycle)).WithArgument(input).Build());
            try
            {
                first.Dispose();
                first.Dispose();
                Assert.AreEqual(1, notified);
                Assert.IsTrue(SpinWait.SpinUntil(() => Volatile.Read(ref cancelled) == 1, 5000));
                Assert.AreEqual(1, cancelled);
                Assert.AreEqual("running", second.Result);
            }
            finally { first.Dispose(); second.Dispose(); }
            Assert.AreEqual(2, notified);
            Assert.IsTrue(SpinWait.SpinUntil(() => Volatile.Read(ref cancelled) == 2, 5000));
        }

        [Test]
        public void FailedEntryStillNotifiesAndCancelsChildren()
        {
            int notified = 0, cancelled = 0;
            Assert.Throws<InvalidOperationException>(() => OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(ApplicationHostEntry), nameof(ApplicationHostEntry.Lifecycle)).WithArgument(new object[] {
                    (Action)(() => Interlocked.Increment(ref notified)),
                    (Action)(() => Interlocked.Increment(ref cancelled)), true }).Build()));
            Assert.AreEqual(1, notified);
            Assert.IsTrue(SpinWait.SpinUntil(() => Volatile.Read(ref cancelled) == 1, 5000));
        }

        [Test]
        public void DisposalDoesNotWaitForNonCooperativeWorkAndStopsLogProcessor()
        {
            using (var release = new ManualResetEventSlim())
            {
                var host = OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(ApplicationHostEntry), nameof(ApplicationHostEntry.NonCooperative)).WithArgument(release).Build());
                var threads = (Thread[])host.Result;
                try
                {
                    var disposing = System.Threading.Tasks.Task.Run(() => host.Dispose());
                    Assert.IsTrue(disposing.Wait(5000), "Disposal waited for non-cooperative plugin work.");
                    Assert.IsTrue(threads[0].IsAlive, "Non-cooperative work must not be forcibly terminated.");
                    Assert.IsTrue(threads[1].Join(5000), "Engine log processor did not exit.");
                }
                finally { release.Set(); host.Dispose(); }
                Assert.IsTrue(threads[0].Join(5000));
            }
        }

        [Test]
        public void DisposalClosesEngineOwnedSessionLogFile()
        {
            var path = Path.Combine(Path.GetTempPath(), "HostLog-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                using (var host = OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(ApplicationHostEntry), nameof(ApplicationHostEntry.SessionLog)).WithArgument(path).Build()))
                    Assert.AreEqual(path, host.Result);
                using (var file = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    Assert.Greater(file.Length, 0);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void BuilderOptionsAreSnapshotsAndSupportBothEntryPointForms()
        {
            var builder = new ApplicationHostOptions.Builder()
                .WithEntryPoint(typeof(ApplicationHostEntry), nameof(ApplicationHostEntry.Echo))
                .WithArgument("first");
            var first = builder.Build();
            var second = builder.WithArgument("second").Build();
            using (var host = OpenTap.Hosting.ApplicationHost.Run(first)) Assert.AreEqual("first", host.Result);
            using (var host = OpenTap.Hosting.ApplicationHost.Run(second)) Assert.AreEqual("second", host.Result);
            var fileOptions = new ApplicationHostOptions.Builder()
                .WithEntryPoint(typeof(ApplicationHostEntry).Assembly.Location, typeof(ApplicationHostEntry).FullName,
                    nameof(ApplicationHostEntry.Echo)).WithArgument("file").Build();
            using (var host = OpenTap.Hosting.ApplicationHost.Run(fileOptions)) Assert.AreEqual("file", host.Result);
            Assert.Throws<InvalidOperationException>(() => new ApplicationHostOptions.Builder().Build());
            Assert.Throws<ArgumentNullException>(() => OpenTap.Hosting.ApplicationHost.Run((ApplicationHostOptions)null));
            Assert.Throws<ArgumentException>(() => new ApplicationHostOptions.Builder().WithEntryPoint("", "Entry", "Run"));
        }

        [Test]
        public void FileEntryPointDoesNotLoadEntryAssemblyIntoParent()
        {
            var name = "FileEntry" + Guid.NewGuid().ToString("N");
            var path = Path.Combine(Path.GetTempPath(), name + ".dll");
            var compiled = CodeGen.BuildCode(@"public static class Entry {
                public static object Run(object argument) {
                    if (OpenTap.ApplicationContext.GetEntryAssembly() != typeof(Entry).Assembly)
                        throw new System.Exception(""Wrong logical entry assembly"");
                    return argument;
                }
            }", name);
            Assert.IsTrue(compiled.Success, compiled.Log);
            File.WriteAllBytes(path, compiled.Bytes);
            try
            {
                var options = new ApplicationHostOptions.Builder().WithEntryPoint(path, "Entry", "Run")
                    .WithArgument("private-only").Build();
                using (var host = OpenTap.Hosting.ApplicationHost.Run(options)) Assert.AreEqual("private-only", host.Result);
                Assert.IsFalse(System.Linq.Enumerable.Any(AssemblyLoadContext.Default.Assemblies, a => a.GetName().Name == name));
            }
            finally { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }

        [Test]
        public void SupportsInternalEntryPointsButRejectsPrivateAndProtectedMethods()
        {
            var options = new ApplicationHostOptions.Builder()
                .WithEntryPoint(typeof(NonPublicHostEntry), nameof(NonPublicHostEntry.InternalEntry))
                .WithArgument("internal").Build();
            using (var host = OpenTap.Hosting.ApplicationHost.Run(options)) Assert.AreEqual("internal", host.Result);
            foreach (var method in new[] { "PrivateEntry", "ProtectedEntry" })
                Assert.Throws<ArgumentException>(() => OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder()
                    .WithEntryPoint(typeof(NonPublicHostEntry), method).Build()));
        }

        [Test]
        public void RejectsInvalidEntrySignatures()
        {
            Assert.Throws<ArgumentException>(() =>
                OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(ApplicationHostEntry), nameof(ApplicationHostEntry.InvalidReturn)).WithArgument(null).Build()));
            Assert.Throws<ArgumentException>(() =>
                OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(ApplicationHostEntry), nameof(ApplicationHostEntry.InvalidParameter)).WithArgument(null).Build()));
        }
    }

    internal class NonPublicHostEntry
    {
        internal static object InternalEntry(object argument) => argument;
        private static object PrivateEntry(object argument) => argument;
        protected static object ProtectedEntry(object argument) => argument;
    }

    public class HostedMeasurementStep : TestStep
    {
        public override void Run()
        {
            Log.Info("Executing hosted measurement.");
            Results.Publish("Measurement", new System.Collections.Generic.List<string> { "Voltage" }, 1.23);
            UpgradeVerdict(Verdict.Pass);
        }
    }

    public class HostedPlanListener : ResultListener
    {
        public int Results;
        public int CompletedSteps;
        public bool Completed;
        public override void OnResultPublished(Guid stepRunId, ResultTable result) => Results++;
        public override void OnTestStepRunCompleted(TestStepRun stepRun) => CompletedSteps++;
        public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream) => Completed = true;
    }

    public static class ApplicationHostEntry
    {
        public static object Run(object argument)
        {
            if (ApplicationContext.GetEntryAssembly() != typeof(ApplicationHostEntry).Assembly)
                throw new Exception("Logical entry assembly is not the reloaded application.");
            return new object[]
            {
                argument,
                ApplicationContext.GetEntryAssembly().Location,
                AssemblyLoadContext.GetLoadContext(typeof(ApplicationContext).Assembly).Name,
                AssemblyLoadContext.GetLoadContext(typeof(ApplicationContext).Assembly) == AssemblyLoadContext.Default
            };
        }
        public static object Nested(object argument)
        {
            var outer = AssemblyLoadContext.GetLoadContext(typeof(ApplicationContext).Assembly);
            if (!outer.IsCollectible || AssemblyLoadContext.CurrentContextualReflectionContext != outer)
                throw new Exception("Outer host is not collectible or has the wrong reflection context.");
            var onUnload = (Action)argument;
            outer.Unloading += _ => onUnload();
            using (var inner = OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(ApplicationHostEntry), nameof(Inner)).WithArgument(new object[] { new WeakReference(outer), onUnload }).Build()))
            {
                if ((string)inner.Result != "inner-completed") throw new Exception("Wrong nested result.");
            }
            if (AssemblyLoadContext.CurrentContextualReflectionContext != outer ||
                AssemblyLoadContext.GetLoadContext(typeof(ApplicationContext).Assembly) != outer)
                throw new Exception("Nested invocation did not restore the outer context.");
            if (ApplicationContext.GetEntryAssembly() != typeof(ApplicationHostEntry).Assembly)
                throw new Exception("Nested invocation changed the outer logical entry assembly.");
            // Verify ordinary outer execution continues after requesting inner unload.
            if ((string)Echo("outer-resumed") != "outer-resumed")
                throw new Exception("Outer execution did not resume.");
            return "outer-resumed";
        }

        public static object Inner(object argument)
        {
            var args = (object[])argument;
            var inner = AssemblyLoadContext.GetLoadContext(typeof(ApplicationContext).Assembly);
            inner.Unloading += _ => ((Action)args[1])();
            if (!inner.IsCollectible || ReferenceEquals(inner, ((WeakReference)args[0]).Target) ||
                AssemblyLoadContext.CurrentContextualReflectionContext != inner)
                throw new Exception("Inner host did not create an independent collectible context.");
            if (ApplicationContext.GetEntryAssembly() != typeof(ApplicationHostEntry).Assembly)
                throw new Exception("Inner logical entry assembly is incorrect.");
            return "inner-completed";
        }

        public static object ExecutePlan(object argument)
        {
            PluginManager.GetPlugins<TestStep>();
            var plan = new TestPlan();
            plan.Steps.Add(new OpenTap.Plugins.BasicSteps.DelayStep { DelaySecs = 0.01 });
            plan.Steps.Add(new HostedMeasurementStep());
            var listener = new HostedPlanListener();
            var run = plan.Execute(new IResultListener[] { listener });
            if (run.Verdict != Verdict.Pass || listener.Results != 1 || listener.CompletedSteps != 2 || !listener.Completed)
                throw new Exception("Hosted test plan or result delivery failed.");
            if ((string)argument == "inner") return "inner-plan-passed";
            using (var inner = OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(ApplicationHostEntry), nameof(ExecutePlan)).WithArgument("inner").Build()))
                return new[] { "outer-plan-passed", (string)inner.Result };
        }

        public static object SessionLog(object argument)
        {
            SessionLogs.SkipStartupInfo = true;
            SessionLogs.Initialize((string)argument);
            return SessionLogs.GetSessionLogFilePath();
        }

        public static object NonCooperative(object argument)
        {
            var release = (ManualResetEventSlim)argument;
            Thread worker = null;
            using (var started = new ManualResetEventSlim())
            {
                TapThread.Start(() =>
                {
                    worker = Thread.CurrentThread;
                    started.Set();
                    release.Wait();
                    Log.CreateSource("Late work").Info("Logging after shutdown must not wait for a stopped processor.");
                });
                if (!started.Wait(5000)) throw new Exception("Worker failed to start.");
            }
            var logContext = typeof(Log).GetField("rootLogContext", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var processor = (Thread)logContext.GetType().GetField("processor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(logContext);
            return new[] { worker, processor };
        }

        public static object Lifecycle(object argument)
        {
            var input = (object[])argument;
            var context = AssemblyLoadContext.GetLoadContext(typeof(ApplicationContext).Assembly);
            var root = TapThread.Current;
            ApplicationContext.Disposing += (_, __) => throw new InvalidOperationException("Failing notification");
            ApplicationContext.Disposing += (_, __) =>
            {
                if (AssemblyLoadContext.CurrentContextualReflectionContext != context || root.AbortToken.IsCancellationRequested)
                    throw new Exception("Wrong disposal context or notification order.");
                Log.CreateSource("Lifecycle").Info("Disposal notification can still log.");
                ((Action)input[0])();
            };
            var started = new ManualResetEventSlim();
            TapThread.Start(() =>
            {
                started.Set();
                try { TapThread.Sleep(TimeSpan.FromHours(1)); }
                catch (OperationCanceledException) { ((Action)input[1])(); }
            });
            if (!started.Wait(5000)) throw new Exception("Child failed to start.");
            started.Dispose();
            if ((bool)input[2]) throw new InvalidOperationException("Entry failure");
            return "running";
        }

        public static object Fail(object argument) => throw new InvalidOperationException("Entry failure");
        public static object Echo(object argument) => argument;
        public static object Commands(object argument) => new object[]
        {
            "restart-session",
            new[] { ("remove", "CSV:9.13.0"), ("add", "CSV:9.14.0") }
        };
        public static object ReferenceTuples(object argument) => new[] { Tuple.Create("remove", "CSV:9.13.0") };
        public static void InvalidReturn(object argument) { }
        public static object InvalidParameter(object[] argument) => null;
    }
}
