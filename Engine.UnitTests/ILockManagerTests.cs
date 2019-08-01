using NUnit.Framework;
using OpenTap.Engine.UnitTests;
using System;
using System.Collections.Generic;
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
            }
            static bool isEnabled = false;
            public static void Enable()
            {
                isEnabled = true;
                AfterCloseArgs.Clear();
                BeforeOpenArgs.Clear();
            }

            public static void Disable() => isEnabled = false;
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
    }


}
