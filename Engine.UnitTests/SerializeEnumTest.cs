using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using NUnit.Framework;
using OpenTap.Engine.UnitTests.TestTestSteps;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class SerializeEnumTest
    {

        public class Step1 : TestStep
        {
            public enum SomeEnum { A, B };
            public Instr2 instr1 { get; set; }
            public SomeEnum Value { get; set; }
            public Step1()
            {
                Value = SomeEnum.B;
            }

            public override void Run()
            {
                throw new NotImplementedException();
            }
        }

        public class Step2 : TestStep
        {
            public enum SomeEnum { A, B };

            public SomeEnum Value { get; set; }
            public Step2()
            {
                Value = SomeEnum.B;
            }

            public override void Run()
            {

            }
        }

        public class Instr1 : Instrument
        {
            public enum SomeEnum { A, B };
            public IInstrument Instr { get; set; }
            public SomeEnum Value { get; set; }
            public Instr1()
            {
                Value = SomeEnum.B;
            }
        }

        public class Instr2 : Instrument
        {
            public enum SomeEnum { A, B };
            public SomeEnum Value { get; set; }
            public IInstrument Instr { get; set; }
            public IResultListener Result { get; set; }
            public Instr2()
            {
                Value = SomeEnum.B;
            }
        }

        [DisplayName("Test\\UnitTest Result2")]
        public class Result2 : ResultListener
        {
            public Result2()
            {
                Name = "Result2";
            }

            public IInstrument Instr { get; set; }
        }



        [Test]
        public void TestSerializePlan()
        {
            Step1 step1 = new Step1();
            Step2 step2 = new Step2();
            TestPlan plan1 = new TestPlan();
            plan1.ChildTestSteps.Add(step1);
            plan1.ChildTestSteps.Add(step2);
            string planString;
            using (var ms = new MemoryStream())
            {
                plan1.Save(ms);
                planString = Encoding.UTF8.GetString(ms.ToArray());
            }

            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(planString)))
            {
                var plan = TestPlan.Load(ms, plan1.Path);
                Assert.AreEqual(((Step1)plan.ChildTestSteps[0]).Value, Step1.SomeEnum.B);
                Assert.AreEqual(((Step2)plan.ChildTestSteps[1]).Value, Step2.SomeEnum.B);

                // test some overloads.
                ms.Position = 0;
                Assert.IsNotNull(TestPlan.Load(ms, ""));

                ms.Position = 0;
                Assert.IsNotNull(TestPlan.Load(ms, null));
            }
        }

        [Test]
        public void SerializeAndRunSimplePlan()
        {
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(new Step2());
            Assert.AreEqual(Verdict.NotSet, plan.Execute().Verdict);

            using (var ms = new MemoryStream())
            {
                plan.Save(ms);
                ms.Position = 0;

                var plan2 = TestPlan.Load(ms, "");
                var planrun = plan2.Execute();

                Assert.AreEqual(Verdict.NotSet, planrun.Verdict);
            }
        }


        bool membersEquals(object o1, object o2)
        {
            var p1 = o1.GetType().GetPropertiesTap().Where(t => t.PropertyType.IsPrimitive).Select(prop => prop.GetValue(o1, null)).ToList();
            var p2 = o1.GetType().GetPropertiesTap().Where(t => t.PropertyType.IsPrimitive).Select(prop => prop.GetValue(o2, null)).ToList();
            return p1.Zip(p2, object.Equals).All(p => p);
        }

        [Test]
        public void SerializeResourcesWithResources()
        {
            var i1 = new Instr1();
            var i2 = new Instr1();
            InstrumentSettings.Current.Add(i1);
            InstrumentSettings.Current.Add(i2);
            try
            {
                var instruments = new InstrumentSettings();
                instruments.Add(new Instr1() { Value = Instr1.SomeEnum.A, Instr = i2 });
                instruments.Add(new Instr2() { Value = Instr2.SomeEnum.A, Instr = i2 });

                string stringdata;
                using (MemoryStream m = new MemoryStream())
                {
                    using (var x = XmlWriter.Create(m))
                        new TapSerializer().Serialize(x, instruments);
                    stringdata = Encoding.UTF8.GetString(m.ToArray());
                }

                InstrumentSettings instruments2;
                using (MemoryStream m = new MemoryStream(Encoding.UTF8.GetBytes(stringdata)))
                {
                    instruments2 = (InstrumentSettings)new TapSerializer().Deserialize(m);
                }

                Assert.AreEqual(((Instr1)instruments[0]).Value, ((Instr1)instruments2[0]).Value);
                Assert.AreEqual(((Instr2)instruments[1]).Value, ((Instr2)instruments2[1]).Value);
                Assert.AreEqual(((Instr2)instruments[1]).Instr, i2);
                Assert.AreEqual(((Instr1)instruments[0]).Instr, i2);
            }
            finally
            {
                InstrumentSettings.Current.Remove(i1);
                InstrumentSettings.Current.Remove(i2);
            }
        }

        [Test]
        public void SerializeSwitch()
        {
            var trace = new EngineUnitTestUtils.TestTraceListener();
            Log.AddListener(trace);

            // Lets hook up a switch to an RfConnection
            // and check that it can be serialized and deserialized.
            var sw = new DummySwitchIntrument();
            var rfCon = new RfConnection();
            rfCon.Via.Add(sw.Positions[0]);
            rfCon.Via.Add(sw.Position1);

            InstrumentSettings.Current.Clear();
            InstrumentSettings.Current.Add(sw);
            ConnectionSettings.Current.Clear();
            ConnectionSettings.Current.Add(rfCon);

            InstrumentSettings.Current.Save();
            ConnectionSettings.Current.Save();

            // Ensure that we are actually getting serialize/deserialized.
            InstrumentSettings.Current.Clear();
            ConnectionSettings.Current.Clear();

            // Ask to deserialize.
            InstrumentSettings.Current.Invalidate();
            ConnectionSettings.Current.Invalidate();

            sw = InstrumentSettings.Current[0] as DummySwitchIntrument;
            rfCon = ConnectionSettings.Current[0] as RfConnection;
            Assert.AreEqual(rfCon.Via[0], sw.Positions[0]);
            Assert.AreEqual(rfCon.Via[1], sw.Position1);


            Log.Flush();
            Log.RemoveListener(trace);
            trace.AssertErrors();
        }

        [Test]
        public void SerializeResourcesWithResourcesTough()
        {
            // Tough because instruments contains references between them
            var i1 = new Instr1();
            var i2 = new Instr2();
            i1.Instr = i2;
            i2.Instr = i1;
            var orig = InstrumentSettings.GetSettingsDirectory("Bench");
            InstrumentSettings.SetSettingsProfile("Bench", orig + "InstrumentTestDir");
            InstrumentSettings.Current.Add(i1);
            InstrumentSettings.Current.Add(i2);
            try
            {
                InstrumentSettings.Current.Save();

                InstrumentSettings.SetSettingsProfile("Bench", orig);
                InstrumentSettings.Current.ToString();
                InstrumentSettings.SetSettingsProfile("Bench", orig + "InstrumentTestDir");
                Assert.IsTrue(InstrumentSettings.Current.OfType<Instr1>().Any());
                var arr = InstrumentSettings.Current.Cast<dynamic>().ToArray();
                Assert.AreEqual(arr[0], arr[1].Instr);
                Assert.AreEqual(arr[1], arr[0].Instr);
            }
            finally
            {
                InstrumentSettings.SetSettingsProfile("Bench", orig);
                Directory.Delete(orig + "InstrumentTestDir", true);
            }
        }

        [Test]
        public void SerializePlatformSettings()
        {
            var toSerialize = EngineSettings.Current;
            var prevStationName = toSerialize.StationName;
            toSerialize.StationName = "hello";
            string stringdata;
            using (MemoryStream m = new MemoryStream())
            {
                using (var x = XmlWriter.Create(m))
                    new TapSerializer().Serialize(x, toSerialize);
                stringdata = Encoding.UTF8.GetString(m.ToArray());
            }

            EngineSettings platformSettings;
            using (MemoryStream m = new MemoryStream(Encoding.UTF8.GetBytes(stringdata)))
                platformSettings = (EngineSettings)new TapSerializer().Deserialize(m);
            Assert.IsTrue(membersEquals(toSerialize, platformSettings));
        }

        [Test]
        public void DeserializeLegacyPlatformSettings()
        {
            EngineSettings settings = (EngineSettings)new TapSerializer().DeserializeFromString(Resources.GetEmbedded("LegacyPlatformSettings.xml"), TypeData.FromType(typeof(EngineSettings)));
            Assert.AreEqual(settings.OperatorName, "SomeOperator");
        }

        [Test, Ignore("We are not backwards compatible.")]
        public void DeserializeLegacyResultSettings()
        {
            ResultSettings settings = (ResultSettings)new TapSerializer().DeserializeFromString(Resources.GetEmbedded("LegacyResultSettings.xml"), TypeData.FromType(typeof(ResultSettings)));
            Assert.IsTrue(settings[0] is LogResultListener);
            Assert.IsTrue((settings[0] as LogResultListener).FilePath == "SomePath.txt");
            Assert.IsTrue((settings[0] as LogResultListener).FilterOptions ==
                          (LogResultListener.FilterOptionsType.Errors | LogResultListener.FilterOptionsType.Verbose | LogResultListener.FilterOptionsType.Info));
            var serialized = new TapSerializer().SerializeToString(settings);
            var deserialized = (ResultSettings)new TapSerializer().DeserializeFromString(serialized);
            Assert.IsTrue((settings[0] as LogResultListener).FilePath == (deserialized[0] as LogResultListener).FilePath);
        }

        [Test]
        public void DeserializeLegacyTapPlan()
        {
            var stream = File.OpenRead("TestTestPlans/FiveDelays.TapPlan");
            TestPlan plan = (TestPlan)new TapSerializer().Deserialize(stream, type: TypeData.FromType(typeof(TestPlan)));

            var childSteps = plan.ChildTestSteps.OfType<DelayStep>().ToArray();
            Assert.AreEqual(childSteps.Length, 5);
        }

        [Test]
        [Ignore("support for 7.x test plans is dropped.")]
        public void LoadLegacyTestPlanReference()
        {
            var ser = new TapSerializer();
            TestPlan plan = (TestPlan)ser.DeserializeFromFile("TestTestPlans\\LegacyRefPlan.TapPlan");
            Assert.AreEqual(4, plan.ChildTestSteps.Count,"Unexpected number of childsteps.");
            Assert.IsTrue(plan.ChildTestSteps[0].ChildTestSteps[0] is VerdictStep);
            Assert.IsTrue(plan.ChildTestSteps[1].ChildTestSteps[0] is VerdictStep);
            Assert.IsTrue(plan.ChildTestSteps[2].ChildTestSteps[0] is VerdictStep);
            Assert.IsTrue(plan.ChildTestSteps[3].ChildTestSteps[0] is VerdictStep);

        }

    }
}