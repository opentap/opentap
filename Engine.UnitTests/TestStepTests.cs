using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;
using OpenTap.UnitTests;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class TestStepTests
    {
        [Test]
        public void FormattedName()
        {
            var delay = new DelayStep() {DelaySecs = 0.1, Name = "Delay: {Time Delay}"};
            var formattedName = delay.GetFormattedName();
            Assert.AreEqual("Delay: 0.1 s", formattedName);
        }

        [Test]
        public void AnnotatedFormattedName()
        {
            // both annotating the step itself and TestStep.Name should give the same GetFormatted read-only string.
            
            var delay = new DelayStep() {DelaySecs = 0.1, Name = "Delay: {Time Delay}"};
            var annotation = AnnotationCollection.Annotate(delay);
            var formattedName = annotation.Get<IStringReadOnlyValueAnnotation>().Value;
            Assert.AreEqual("Delay: 0.1 s", formattedName);

            var formattedName2 = annotation.GetMember(nameof(TestStep.Name)).Get<IStringReadOnlyValueAnnotation>().Value;
            Assert.AreEqual("Delay: 0.1 s", formattedName2);
        }

        [Test]
        public void FormattedNameIssue()
        {
            var logStep = new LogStep() {};
            logStep.Name = "Log: {0}"; // At one point this caused a bug, but it was not because of GetFormattedName.
            var formattedName = logStep.GetFormattedName();
            Assert.AreEqual("Log: {0}", formattedName);

            var plan = new TestPlan();
            plan.ChildTestSteps.Add(logStep);
            var run = plan.Execute();
            Assert.AreEqual(Verdict.NotSet, run.Verdict);
        }

        [Test]
        public void TestGetObjectSettings()
        {
            // A race condition issue occured inside GetObjectSettings.
            // to reproduce it, do it in two synchronized threads.
            // the mix of threads and semaphores below are to ensure that the threadsa
            // starts as much in sync as possible.
            var steps = new ITestStep[]
            {
                new DelayStep(), new SequenceStep(), new LockStep(), new SequenceStep(),
                new DialogStep(), new BusyStep(), new ArtifactStep(), new SerializeEnumTest.Step1(), new SerializeEnumTest.Step2(),
                new MemberDataProviderTests.Delay2Step(), new ResultTest.ActionStep(), new DutStep2(), new IfStep()
            };
            int threadCount = 2;
            var threadWaitSem = new Semaphore(0, threadCount);
            var mainWaitSem = new Semaphore(0, threadCount);
            Exception error = null;
            for (int i = 0; i < threadCount; i++)
            {
                TapThread.Start(() =>
                {
                    //signal the main thread that we are ready to go.
                    mainWaitSem.Release();
                    // wait for the main thread to signal back.
                    threadWaitSem.WaitOne();
                    try
                    {
                        TestStepExtensions.GetObjectSettings<object, ITestStep, object>(steps, false, (t, data) => t, new HashSet<object>());
                    }
                    catch (Exception e)
                    {
                        error = e;
                    }
                    //signal the main thread that we are done.
                    mainWaitSem.Release();
                });
            }

            // wait for the threads to be ready.
            for(int i = 0; i < threadCount; i++)
                mainWaitSem.WaitOne();
            
            // signal that the threads can start.
            threadWaitSem.Release(threadCount);
            
            // Wait for all to complete.
            for(int i = 0; i < threadCount; i++)
                mainWaitSem.WaitOne();
            
            // the issue should cause an exception when reproduced.
            // if its fixed the error will be null.
            Assert.IsNull(error);
        }
    }
}