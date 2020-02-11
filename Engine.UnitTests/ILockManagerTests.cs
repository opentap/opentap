using NUnit.Framework;
using OpenTap.EngineUnitTestUtils;
using OpenTap.Plugins.BasicSteps;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class ILockManagerTests
    {
        public class UnitTestingLockManager : ILockManager
        {
            static public List<IEnumerable<IResourceReferences>> AfterCloseArgs = new List<IEnumerable<IResourceReferences>>();
            public void AfterClose(IEnumerable<IResourceReferences> resources, CancellationToken abortToken)
            {
                if(isEnabled)
                    AfterCloseArgs.Add(resources);
            }
            static public List<IEnumerable<IResourceReferences>> BeforeOpenArgs = new List<IEnumerable<IResourceReferences>>();
            public void BeforeOpen(IEnumerable<IResourceReferences> resources, CancellationToken abortToken)
            {
                if(isEnabled)
                    BeforeOpenArgs.Add(resources);
                if (BeforeOpenEffect != null)
                    BeforeOpenEffect(resources);
            }
            static bool isEnabled = false;
            public static Action<IEnumerable<IResourceReferences>> BeforeOpenEffect = null;
            public static void Enable()
            {
                isEnabled = true;
                AfterCloseArgs.Clear();
                BeforeOpenArgs.Clear();
            }

            public static void Disable()
            {
                isEnabled = false;
                BeforeOpenEffect = null;
            }
        }

        public class InstrumentTestStep : TestStep
        {
            public IInstrument Instrument { get; set; }
            public override void Run()
            {

            }
        }

        public class DoubleInstrumentTestStep : TestStep
        {
            public IInstrument Instrument1 { get; set; }
            public IInstrument Instrument2 { get; set; }
            public override void Run()
            {

            }
        }

        public class SomeInstrument : Instrument
        {

        }
        
        [Test]
        public void SimpleTestDefaultManager()
        {
            EngineSettings.Current.ResourceManagerType = new ResourceTaskManager();
            IInstrument instr1 = new SomeInstrument() { Name = "INSTR1"};
            IInstrument instr2 = new SomeInstrument() { Name = "INSTR2" };
            InstrumentSettings.Current.Add(instr1);
            InstrumentSettings.Current.Add(instr2);
            TestPlan plan = new TestPlan();
            ITestStep step1 = new InstrumentTestStep() { Instrument = instr1 };
            plan.Steps.Add(step1);
            ITestStep step2 = new InstrumentTestStep() { Instrument = instr2 };
            plan.Steps.Add(step2);
            UnitTestingLockManager.Enable();
            plan.Execute();
            UnitTestingLockManager.Disable();
            Assert.AreEqual(1, UnitTestingLockManager.BeforeOpenArgs.Count(), "BeforeOpen hook called an unexpected number of times.");
            Assert.AreEqual(2, UnitTestingLockManager.BeforeOpenArgs.First().Count(), "Resources list contain an unexpected number of items.");

            Assert.AreEqual(instr1, UnitTestingLockManager.BeforeOpenArgs.First().First().Resource, "ResourceReference has unexpected Resource.");
            Assert.AreEqual(1, UnitTestingLockManager.BeforeOpenArgs.First().First().References.Count(), "ResourceReference has unexpected number of references.");
            Assert.AreEqual(step1, UnitTestingLockManager.BeforeOpenArgs.First().First().References.First().Instance, "ResourceReference references unexpected object.");
            Assert.AreEqual(step1.GetType().GetProperty("Instrument"), UnitTestingLockManager.BeforeOpenArgs.First().First().References.First().Property, "ResourceReference references unexpected property.");

            Assert.AreEqual(instr2, UnitTestingLockManager.BeforeOpenArgs.First().Last().Resource, "Resource has unexpected value.");
        }

        [Test]
        public void SimpleTestLazyManager()
        {
            EngineSettings.Current.ResourceManagerType = new LazyResourceManager();
            IInstrument instr1 = new SomeInstrument() { Name = "INSTR1" };
            IInstrument instr2 = new SomeInstrument() { Name = "INSTR2" };
            InstrumentSettings.Current.Add(instr1);
            InstrumentSettings.Current.Add(instr2);
            TestPlan plan = new TestPlan();
            plan.Steps.Add(new InstrumentTestStep() { Instrument = instr1 });
            plan.Steps.Add(new InstrumentTestStep() { Instrument = instr2 });
            UnitTestingLockManager.Enable();
            plan.Execute();
            UnitTestingLockManager.Disable();
            Assert.AreEqual(2, UnitTestingLockManager.BeforeOpenArgs.Count(), "BeforeOpen hook called an unexpected number of times.");
            Assert.AreEqual(1, UnitTestingLockManager.BeforeOpenArgs.First().Count(), "Resources list contain an unexpected number of items.");
            Assert.AreEqual(instr1, UnitTestingLockManager.BeforeOpenArgs.First().First().Resource, "ResourceReference has unexpected Resource.");
        }

        [Test]
        public void DoubleResourcePropertyTest()
        {
            EngineSettings.Current.ResourceManagerType = new LazyResourceManager();
            IInstrument instr1 = new SomeInstrument() { Name = "INSTR1" };
            IInstrument instr2 = new SomeInstrument() { Name = "INSTR2" };
            InstrumentSettings.Current.Add(instr1);
            InstrumentSettings.Current.Add(instr2);
            TestPlan plan = new TestPlan();
            ITestStep step1 = new DoubleInstrumentTestStep() { Instrument1 = instr1, Instrument2 = instr1 };
            plan.Steps.Add(step1);
            ITestStep step2 = new DoubleInstrumentTestStep() { Instrument1 = instr2, Instrument2 = instr1 };
            plan.Steps.Add(step2);
            UnitTestingLockManager.Enable();
            var run = plan.Execute();
            UnitTestingLockManager.Disable();

            Assert.IsFalse(run.FailedToStart,"Plan run failed.");
            Assert.AreEqual(2, UnitTestingLockManager.BeforeOpenArgs.Count(), "BeforeOpen hook called an unexpected number of times.");

            IEnumerable<IResourceReferences> arg1 = UnitTestingLockManager.BeforeOpenArgs.First();
            Assert.AreEqual(1, arg1.Count(), "Resources list contain an unexpected number of items.");
            Assert.AreEqual(instr1, arg1.First().Resource, "ResourceReference has unexpected Resource.");
            Assert.AreEqual(2, arg1.First().References.Count(), "ResourceReference has unexpected number of references.");
            Assert.AreEqual(step1, arg1.First().References.First().Instance, "ResourceReference references unexpected object.");
            Assert.AreEqual(step1, arg1.First().References.Last().Instance, "ResourceReference references unexpected object.");
            Assert.AreEqual(step1.GetType().GetProperty("Instrument1"), arg1.First().References.First().Property, "ResourceReference references unexpected property.");
            Assert.AreEqual(step1.GetType().GetProperty("Instrument2"), arg1.First().References.Last().Property, "ResourceReference references unexpected property.");

            IEnumerable<IResourceReferences> arg2 = UnitTestingLockManager.BeforeOpenArgs.Last();
            Assert.AreEqual(2, arg2.Count(), "Resources list contain an unexpected number of items.");
            Assert.IsTrue(arg2.Any(rr => rr.Resource == instr1), "ResourceReference has unexpected Resource.");
            Assert.IsTrue(arg2.Any(rr => rr.Resource == instr2), "ResourceReference has unexpected Resource.");
            Assert.AreEqual(1, arg2.First().References.Count(), "ResourceReference has unexpected number of references.");
            Assert.AreEqual(step2, arg2.First().References.First().Instance, "ResourceReference references unexpected object.");
            Assert.AreEqual(step2.GetType().GetProperty("Instrument1"), arg2.First().References.First().Property, "ResourceReference references unexpected property.");
        }

        private void SetNullResources(IEnumerable<IResourceReferences> resources)
        {
            // If some resources are still null at this stage, ILockManager is expected to set them. Otherwise the plan run will error out with "Instrument not configured for step..." error
            try
            {
                foreach (var r in resources)
                {
                    if (r.Resource != null)
                        continue;
                    foreach (var step in r.References)
                    {
                        var instrument = InstrumentSettings.Current.First();
                        if (step.Property.GetValue(step.Instance) == null)
                        {
                            step.Property.SetValue(step.Instance, InstrumentSettings.Current.First());
                            r.Resource = instrument;
                        }
                    }
                }
            }
            catch
            {

            }
        }

        [Test]
        public void SimpleResourceNullPropertyTest()
        {
            // Just test that we can run a plan with a null resource, if the null is repaced with an actual instance by ILockManager.BeforeOpen()
            EngineSettings.Current.ResourceManagerType = new LazyResourceManager();
            IInstrument instr1 = new SomeInstrument() { Name = "INSTR1" };
            InstrumentSettings.Current.Add(instr1);
            TestPlan plan = new TestPlan();
            ITestStep step1 = new InstrumentTestStep() { Instrument = null };
            plan.Steps.Add(step1);
            UnitTestingLockManager.Enable();
            UnitTestingLockManager.BeforeOpenEffect = SetNullResources;
            var run = plan.Execute();
            UnitTestingLockManager.Disable();

            Assert.IsFalse(run.FailedToStart, "Plan run failed.");
            Assert.AreEqual(Verdict.NotSet, run.Verdict);
            Assert.AreEqual(1, UnitTestingLockManager.BeforeOpenArgs.Count(), "BeforeOpen hook called an unexpected number of times.");

            IEnumerable<IResourceReferences> arg1 = UnitTestingLockManager.BeforeOpenArgs.First();
            Assert.AreEqual(1, arg1.Count(), "Resources list contain an unexpected number of items.");
        }

        [Test]
        public void DoubleResourceNullPropertyTest()
        {
            // Tests the special case where two properties use null resources (= placeholders) on the same step.
            // Normally only one IResourceReferences item is given to ILockManager.BeforeOpen() for each Resource used. 
            // But for null Resources, we want one IResourceReferences item per property, so BeforeOpen can set each prop 
            // to different Resource instances when replacing the null values.
            EngineSettings.Current.ResourceManagerType = new LazyResourceManager();
            IInstrument instr1 = new SomeInstrument() { Name = "INSTR1" };
            IInstrument instr2 = new SomeInstrument() { Name = "INSTR2" };
            InstrumentSettings.Current.Add(instr1);
            InstrumentSettings.Current.Add(instr2);
            TestPlan plan = new TestPlan();
            ITestStep step1 = new DoubleInstrumentTestStep() { Instrument1 = null, Instrument2 = null };
            plan.Steps.Add(step1);
            UnitTestingLockManager.Enable();
            UnitTestingLockManager.BeforeOpenEffect = SetNullResources;
            var run = plan.Execute();
            UnitTestingLockManager.Disable();

            Assert.IsFalse(run.FailedToStart, "Plan run failed.");
            Assert.AreEqual(Verdict.NotSet, run.Verdict);
            Assert.AreEqual(1, UnitTestingLockManager.BeforeOpenArgs.Count(), "BeforeOpen hook called an unexpected number of times.");

            IEnumerable<IResourceReferences> arg1 = UnitTestingLockManager.BeforeOpenArgs.First();
            Assert.AreEqual(2, arg1.Count(), "Resources list contain an unexpected number of items.");
            Assert.AreEqual(1, arg1.First().References.Count(), "ResourceReference has unexpected number of references.");
            Assert.AreEqual(1, arg1.Last().References.Count(), "ResourceReference has unexpected number of references.");
            Assert.AreEqual(step1, arg1.First().References.First().Instance, "ResourceReference references unexpected object.");
            Assert.AreEqual(step1, arg1.Last().References.First().Instance, "ResourceReference references unexpected object.");
            Assert.AreEqual(step1.GetType().GetProperty("Instrument1"), arg1.First().References.First().Property, "ResourceReference references unexpected property.");
            Assert.AreEqual(step1.GetType().GetProperty("Instrument2"), arg1.Last().References.First().Property, "ResourceReference references unexpected property.");
        }
        
        [TestCase(typeof(LazyResourceManager), typeof(OperationCanceledException), Verdict.Error, false)]
        [TestCase(typeof(LazyResourceManager), typeof(Exception), Verdict.Error, false)]
        [TestCase(typeof(ResourceTaskManager), typeof(OperationCanceledException), Verdict.Error, false)]
        [TestCase(typeof(ResourceTaskManager), typeof(Exception), Verdict.Error, false)]
        [TestCase(typeof(LazyResourceManager), typeof(OperationCanceledException), Verdict.Error, true)]
        [TestCase(typeof(LazyResourceManager), typeof(Exception), Verdict.Error, true)]
        [TestCase(typeof(ResourceTaskManager), typeof(OperationCanceledException), Verdict.Error, true)]
        [TestCase(typeof(ResourceTaskManager), typeof(Exception), Verdict.Error, true)]
        public void BeforeOpenExceptionTest(Type managerType, Type exceptionType, Verdict expectedVerdict, bool withParallel)
        {
            EngineSettings.Current.ResourceManagerType = (IResourceManager)Activator.CreateInstance(managerType);
            IInstrument instr1 = new SomeInstrument() { Name = "INSTR1" };
            InstrumentSettings.Current.Add(instr1);
            try
            {
                TestPlan plan = new TestPlan();
                ITestStep step1 = new InstrumentTestStep() { Instrument = instr1 };
                if (withParallel)
                {
                    var par = new ParallelStep();
                    par.ChildTestSteps.Add(step1);
                    step1 = par;
                }
                plan.Steps.Add(step1);
                ITestStep lastStep = new LogStep() { Name="Last Step", LogMessage="Last Step Ran" };
                plan.Steps.Add(lastStep);
                UnitTestingLockManager.Enable();
                UnitTestingLockManager.BeforeOpenEffect = r => throw (Exception)Activator.CreateInstance(exceptionType, new string[] { "Custom exception message" });
                var logListener = new TestTraceListener();
                Log.AddListener(logListener);
                //string logFileName = Path.Combine(Path.GetDirectoryName(PluginManager.GetOpenTapAssembly().Location), $"BeforeOpenExceptionTest({managerType},{exceptionType},{withParallel}).txt");
                //if (File.Exists(logFileName)) File.Delete(logFileName);
                //var fileListener = new FileTraceListener(logFileName);
                //Log.AddListener(fileListener);
                try
                {
                    var run = plan.Execute();
                    Assert.AreEqual(expectedVerdict, run.Verdict);
                }
                finally
                {
                    Log.RemoveListener(logListener);
                    //Log.RemoveListener(fileListener);
                    UnitTestingLockManager.Disable();
                }

                // We should have the custom exception message in the log
                StringAssert.Contains("Custom exception message", logListener.GetLog());

                if (expectedVerdict == Verdict.Error)
                    StringAssert.IsMatch(": Error .+Custom exception message", logListener.GetLog());
                else
                    StringAssert.IsMatch(": Warning .+Custom exception message", logListener.GetLog());

                // The last step should not have run:
                if(exceptionType == typeof(OperationCanceledException))
                    StringAssert.DoesNotContain("Last Step Ran", logListener.GetLog());
                
            }
            finally
            {
                InstrumentSettings.Current.Remove(instr1);
            }
        }


        [TestCase(typeof(LazyResourceManager), 3)]
        [TestCase(typeof(ResourceTaskManager), 1)]
        public void BeforeOpenAfterCloseCallCountTest(Type managerType, int expectedCount)
        {
            EngineSettings.Current.ResourceManagerType = (IResourceManager)Activator.CreateInstance(managerType);
            IInstrument instr1 = new SomeInstrument() { Name = "INSTR1" };
            IInstrument instr2 = new SomeInstrument() { Name = "INSTR2" };
            InstrumentSettings.Current.Add(instr1);
            InstrumentSettings.Current.Add(instr2);
            Log.AddListener(new DiagnosticTraceListener());
            TestPlan plan = new TestPlan();
            ITestStep step1 = new DoubleInstrumentTestStep() { Instrument1 = instr1, Instrument2 = instr2 };
            plan.Steps.Add(step1);
            ITestStep step2 = new InstrumentTestStep() { Instrument = instr1 };
            plan.Steps.Add(step2);
            ITestStep step4 = new InstrumentTestStep() { Instrument = instr1 }; // a step uses the same instrument as another step to test that this 
            plan.Steps.Add(step4);
            ITestStep step3 = new DelayStep() { DelaySecs = 0 }; // a step without any Resource properties. To check that this does not create a call to BeforeOpen
            plan.Steps.Add(step3);
            UnitTestingLockManager.Enable();
            UnitTestingLockManager.BeforeOpenEffect = SetNullResources;
            var run = plan.Execute();
            UnitTestingLockManager.Disable();

            Assert.IsFalse(run.FailedToStart, "Plan run failed.");
            Assert.AreEqual(Verdict.NotSet, run.Verdict);
            Assert.AreEqual(expectedCount, UnitTestingLockManager.BeforeOpenArgs.Count(), "BeforeOpen hook called an unexpected number of times.");
            Assert.AreEqual(expectedCount, UnitTestingLockManager.AfterCloseArgs.Count(), "AfterClose hook called an unexpected number of times.");
        }
    }
}
