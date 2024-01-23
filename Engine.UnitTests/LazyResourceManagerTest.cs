using System.Xml.Serialization;
using NUnit.Framework;
using OpenTap.Engine.UnitTests;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class LazyResourceManagerTest
    {
        public class ExclusiveInstrument : Instrument
        {
            [ResourceOpen(ResourceOpenBehavior.Ignore)]
            [XmlIgnore]
            public ExclusiveInstrument OtherInstrument { get; set; }

            public override void Open()
            {
                Assert.IsTrue(OtherInstrument.IsConnected != true);
                base.Open();
                Assert.IsTrue(OtherInstrument.IsConnected != true);
            }

            public override void Close()
            {
                Assert.IsTrue(OtherInstrument.IsConnected != true);
                base.Close();
                Assert.IsTrue(OtherInstrument.IsConnected != true);
            }
        }

        public class CheckExclusiveInstrumentStep : TestStep
        {
            
            public ExclusiveInstrument Instrument { get; set; }
            public override void Run()
            {
                Assert.IsFalse(Instrument.OtherInstrument.IsConnected);
            }
        }
        
        // In this test one instrument is not permitted to be open while the other is.
        [Test]
        public void TestExclusiveInstruments()
        {
            using (Session.Create(SessionOptions.OverlayComponentSettings))
            {
                EngineSettings.Current.ResourceManagerType = new LazyResourceManager();
                var instr1 = new ExclusiveInstrument();
                var instr2 = new ExclusiveInstrument() { OtherInstrument = instr1 };
                instr1.OtherInstrument = instr2;
                var step1 = new CheckExclusiveInstrumentStep() { Instrument = instr1 };
                var step2 = new CheckExclusiveInstrumentStep() {Instrument = instr2};
                var step3 = new CheckExclusiveInstrumentStep() { Instrument = instr1 };
                var step4 = new CheckExclusiveInstrumentStep(){Instrument = instr2};
                var plan = new TestPlan();
                var repeat = new RepeatStep()
                {
                    Count = 5
                };
                plan.ChildTestSteps.Add(repeat);
                repeat.ChildTestSteps.Add(step1);
                repeat.ChildTestSteps.Add(step2);
                repeat.ChildTestSteps.Add(step3);
                repeat.ChildTestSteps.Add(step4);
                var collect = new PlanRunCollectorListener();
                var run = plan.Execute(new IResultListener[]{collect});
                Assert.IsTrue(run.Verdict != Verdict.Error);
                Assert.AreEqual(4 * repeat.Count + 1, collect.StepRuns.Count);
                
            }
        }
    }
}