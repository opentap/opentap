using NUnit.Framework;
using OpenTap.UnitTests;

namespace OpenTap.Engine.UnitTests
{
    public class Instrument0 : Instrument
    {
        public virtual double DoMeasurement() => 10.0;
    }

    public class Instrument1 : Instrument
    {
        public virtual double DoMeasurement2() => 20.0;
    }

    public class Instrument1Step : TestStep
    {
        public Instrument1 Instrument { get; set; }
        public override void Run()
        {
            
        }
    }

    [Display("Instrument 0 to 1", Group: "Adapters")]
    public class Instrument0to1Adapter : Instrument1, ITypeAdaptor
    {
        public override double DoMeasurement2() => Instrument.DoMeasurement();
        public Instrument0 Instrument { get; set; }

        public override string ToString() => $"{Instrument} (Adapter)";

        public override bool Equals(object obj)
        {
            if (obj is Instrument0to1Adapter other)
                return other.Instrument == Instrument;
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return Instrument?.GetHashCode() ?? 0 + 13214;
        }
    }
    
    public class AdapterTest
    {
        [Test]
        public void TestBasicAdaptor()
        {
            using (Session.Create(SessionOptions.OverlayComponentSettings))
            {
                var instr = new Instrument0();
                InstrumentSettings.Current.Add(instr);
                var step = new AnnotationTest.InstrumentStep()
                {
                    Instrument = new Instrument0to1Adapter
                    {
                        Instrument = instr
                    }
                };
                var plan = new TestPlan();
                plan.Steps.Add(step);
                var r = plan.Execute();
                Assert.AreEqual(Verdict.Pass, r.Verdict);

                var str = plan.SerializeToString();
                var p2 = new TapSerializer().DeserializeFromString(str) as TestPlan;
                var r0 = p2.Execute();
                Assert.AreEqual(Verdict.Pass, r0.Verdict);

            }
        }
    }
}