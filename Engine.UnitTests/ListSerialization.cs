using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class ListSerialization 
    {
        public class StringTemp
        {
            public string Test { get; set; }
        }

        public class StringTempListStep : TestStep
        {
            public List<StringTemp> TestProp { get; set; }
            public System.Collections.ObjectModel.ReadOnlyCollection<string> NullList { get; set; }


            public StringTempListStep()
            {
                TestProp = new List<StringTemp>();
            }

            public override void Run()
            {
            }
        }
        public enum TestEnum
        {
            A, B, C
        }
        public class StringListStep : TestStep
        {

            public List<String> TestProp { get; set; }

            public IList List { get; set; }

            public HashSet<int> Set { get; set; }

            public HashSet<TestEnum> EnumSet { get; set; }

            public Dictionary<string, int> Dict { get; set; }

            public StringListStep()
            {
                TestProp = new List<String>();
                List = new List<double> { 1, 2, 3 };
                Set = new HashSet<int>();
                EnumSet = new HashSet<TestEnum>();
                Dict = new Dictionary<string, int>();
            }

            public override void Run()
            {
            }
        }

        [Test]
        public void StringTempListSerialization()
        {
            TestPlan target = new TestPlan();
            var targetStep = new StringTempListStep { TestProp = new List<StringTemp> { new StringTemp { Test = "123" }, new StringTemp { Test = "abc" } } };

            target.Steps.Add(targetStep);

            using (var ms = new MemoryStream())
            {
                target.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                TestPlan deserialized = TestPlan.Load(ms, target.Path);
                var step = deserialized.ChildTestSteps.First() as StringTempListStep;

                Assert.IsNotNull(step.TestProp);
                Assert.AreEqual(targetStep.TestProp.Count, step.TestProp.Count);

                for (int i = 0; i < targetStep.TestProp.Count; i++)
                    Assert.AreEqual(targetStep.TestProp[i].Test, step.TestProp[i].Test);
            }
        }

        [Test]
        public void StringListSerialization()
        {
            TestPlan target = new TestPlan();

            var specList = new List<double> { 5, 6, 7 };
            var hashSet = new HashSet<TestEnum>(new TestEnum[] { TestEnum.C });
            var targetStep = new StringListStep
            {
                TestProp = new List<String> { "123", "abc" },
                List = specList,
                EnumSet = hashSet,
                Set = new HashSet<int>(Enumerable.Range(100, 10)),
                Dict = new Dictionary<string, int>() { }
            };
            targetStep.Dict["asd"] = 5;
            targetStep.Dict[""] = 15;

            target.Steps.Add(targetStep);

            TestPlan deserialized;

            using (var ms = new MemoryStream())
            {
                target.Save(ms);
                ms.Seek(0, SeekOrigin.Begin);
                deserialized = TestPlan.Load(ms, target.Path);
            }
            var step = deserialized.ChildTestSteps.First() as StringListStep;

            Assert.IsNotNull(step.TestProp);
            Assert.AreEqual(targetStep.TestProp.Count, step.TestProp.Count);

            for (int i = 0; i < targetStep.TestProp.Count; i++)
                Assert.AreEqual(targetStep.TestProp[i], step.TestProp[i]);
            Assert.IsTrue(specList.SequenceEqual(step.List.Cast<double>()));
            Assert.IsTrue(step.EnumSet.Except(hashSet).Count() == 0);
            Assert.IsTrue(step.Set.OrderBy(x => x).SequenceEqual(targetStep.Set.OrderBy(x => x)));
            Assert.AreEqual(5, step.Dict["asd"]);
            Assert.AreEqual(15, step.Dict[""]);
        }

        public class InstStep : TestStep
        {
            public List<IInstrument> Instrs { get; set; }

            public override void Run()
            {
            }
        }

        public class SomeotherInstrument : Instrument
        {

        }

        void loadDummyInstruments(int count)
        {
            for (int i = 0; i < count; i++)
            {
                InstrumentSettings.Current.Add(new ScpiDummyInstrument() { Tag = (i + 1).ToString() });
                InstrumentSettings.Current.Add(new SomeotherInstrument());
            }
        }

        void unloadDummyInstruments()
        {
            InstrumentSettings.Current.RemoveIf<IInstrument>(instr => instr is ScpiDummyInstrument && ((ScpiDummyInstrument)instr).Tag != null);
            InstrumentSettings.Current.RemoveIf<IInstrument>(instr => instr is SomeotherInstrument);
        }

        /// <summary>
        /// Tests deserialization/serialzation of a list of instruments.
        /// </summary>
        [Test]
        public void ListInstrumentSerialization()
        {
            try
            {
                loadDummyInstruments(10);
                TestPlan target = new TestPlan();
                Random rnd = new Random(0);
                var targetStep = new InstStep { Instrs = InstrumentSettings.Current.OrderBy(item => rnd.Next()).ToList() };

                target.Steps.Add(targetStep);

                using (var ms = new MemoryStream())
                {
                    target.Save(ms);

                    ms.Seek(0, SeekOrigin.Begin);

                    TestPlan deserialized = TestPlan.Load(ms, target.Path);
                    var step = deserialized.ChildTestSteps.First() as InstStep;

                    Assert.IsNotNull(step.Instrs);
                    Assert.AreEqual(targetStep.Instrs.Count, step.Instrs.Count);

                    for (int i = 0; i < targetStep.Instrs.Count; i++)
                        Assert.AreEqual(targetStep.Instrs[i], step.Instrs[i]);
                }
            }
            finally
            {
                unloadDummyInstruments();
            }
        }

        public class NestedInstStep : TestStep
        {
            public List<List<IInstrument>> Instrs { get; set; }

            public NestedInstStep()
            {
                Instrs = new List<List<IInstrument>>();
            }

            public override void Run()
            {
            }
        }

        /// <summary>
        /// This test step tests serializing/deserializing a list of lists of instruments.
        /// Since Instruments are from ComponentSettingsLists, they should all convert to indexes.
        /// </summary>
        [Test]
        public void ListListInstrumentSerialization()
        {
            loadDummyInstruments(10);
            try
            {
                TestPlan target = new TestPlan();
                Random rnd = new Random(0);
                var randomSeq = InstrumentSettings.Current.OrderBy(item => rnd.Next());
                var targetStep = new NestedInstStep { Instrs = new List<List<IInstrument>> { randomSeq.ToList(), randomSeq.ToList(), randomSeq.ToList() } };

                target.Steps.Add(targetStep);

                using (var ms = new MemoryStream())
                {
                    target.Save(ms);
                    ms.Seek(0, SeekOrigin.Begin);

                    TestPlan deserialized = TestPlan.Load(ms, target.Path);
                    var step = deserialized.ChildTestSteps.First() as NestedInstStep;

                    Assert.IsNotNull(step.Instrs);
                    Assert.AreEqual(targetStep.Instrs.Count, step.Instrs.Count);

                    for (int i = 0; i < targetStep.Instrs.Count; i++)
                    {
                        Assert.AreEqual(targetStep.Instrs[i].Count, step.Instrs[i].Count);

                        for (int i2 = 0; i2 < targetStep.Instrs[i].Count; i2++)
                        {
                            Assert.AreEqual(targetStep.Instrs[i][i2], step.Instrs[i][i2]);
                        }
                    }
                }

            }
            finally
            {
                unloadDummyInstruments();
            }
        }

        public class DualNestedInstStep : TestStep
        {
            public List<List<List<IInstrument>>> Instrs { get; set; }

            public DualNestedInstStep()
            {
                Instrs = new List<List<List<IInstrument>>>();
            }

            public override void Run()
            {
            }
        }

        /// <summary>
        /// This test tests serializing / deserializing of lists of lists of lists of instrments.
        /// </summary>
        [Test]
        public void ListListListInstrumentSerialization()
        {
            loadDummyInstruments(10);
            try
            {
                TestPlan target = new TestPlan();
                Random rnd = new Random(0);
                var randomSeq = InstrumentSettings.Current.OrderBy(item => rnd.Next());
                var targetStep = new DualNestedInstStep { Instrs = new List<List<List<IInstrument>>> { new List<List<IInstrument>> { randomSeq.ToList(), randomSeq.ToList() }, new List<List<IInstrument>> { randomSeq.ToList(), randomSeq.ToList(), randomSeq.ToList(), randomSeq.ToList() } } };

                target.Steps.Add(targetStep);

                using (var ms = new MemoryStream())
                {
                    target.Save(ms);

                    ms.Seek(0, SeekOrigin.Begin);

                    TestPlan deserialized = TestPlan.Load(ms, target.Path);
                    var step = deserialized.ChildTestSteps.First() as DualNestedInstStep;

                    Assert.IsNotNull(step.Instrs);
                    Assert.AreEqual(targetStep.Instrs.Count, step.Instrs.Count);

                    for (int i = 0; i < targetStep.Instrs.Count; i++)
                    {
                        Assert.AreEqual(targetStep.Instrs[i].Count, step.Instrs[i].Count);

                        for (int i2 = 0; i2 < targetStep.Instrs[i].Count; i2++)
                        {
                            Assert.AreEqual(targetStep.Instrs[i][i2].Count, step.Instrs[i][i2].Count);

                            for (int i3 = 0; i3 < targetStep.Instrs[i][i2].Count; i3++)
                            {
                                Assert.AreEqual(targetStep.Instrs[i][i2][i3], step.Instrs[i][i2][i3]);
                            }
                        }
                    }
                }

            }
            finally
            {
                unloadDummyInstruments();
            }
        }

        public class DeserializedCallbackTestStep : TestStep, IDeserializedCallback
        {
            public bool WasDeserialized = false;
            public void OnDeserialized()
            {
                WasDeserialized = true;
            }

            public override void Run()
            {

            }
        }

        public class DeserializedCallbackInstrument : Instrument, IDeserializedCallback
        {
            public bool WasDeserialized = false;
            public void OnDeserialized()
            {
                WasDeserialized = true;
            }
        }

        [Browsable(false)]
        public class DeserializedCallbackSettings : ComponentSettings, IDeserializedCallback
        {
            public bool WasDeserialized = false;
            public void OnDeserialized()
            {
                WasDeserialized = true;
            }
        }

        [Test]
        public void TestIDeserializedCallback()
        {
            {// test deserializing plan
                TestPlan plan = new TestPlan();
                DeserializedCallbackTestStep step = new DeserializedCallbackTestStep();
                plan.ChildTestSteps.Add(step);
                using (var tmpFile = new MemoryStream())
                {
                    plan.Save(tmpFile);
                    tmpFile.Position = 0;
                    plan = TestPlan.Load(tmpFile, plan.Path);
                }
                Assert.IsFalse(step.WasDeserialized);
                step = (DeserializedCallbackTestStep)plan.ChildTestSteps[0];
                Assert.IsTrue(step.WasDeserialized);
            }

            bool testSerializeStandaloneInstrument = false;
            if (testSerializeStandaloneInstrument)
            {
                // This cannot work, because there is no ComponentSettings referencing inst.
                // Previously this worked because of an error.

                // test deserializing instrument
                DeserializedCallbackInstrument inst = new DeserializedCallbackInstrument();
                string xml = new TapSerializer().SerializeToString(inst);
                var inst2 = (DeserializedCallbackInstrument)new TapSerializer().DeserializeFromString(xml, TypeData.GetTypeData(inst));
                Assert.IsTrue(inst2.WasDeserialized);
                Assert.IsFalse(inst.WasDeserialized);
            }

            { // test deserializing settings
                DeserializedCallbackSettings settings = new DeserializedCallbackSettings();
                string xml = new TapSerializer().SerializeToString(settings);
                var settings2 = (DeserializedCallbackSettings)new TapSerializer().DeserializeFromString(xml, TypeData.GetTypeData(settings));
                Assert.IsTrue(settings2.WasDeserialized);
                Assert.IsFalse(settings.WasDeserialized);
            }
        }

        class PrivateComponentSettingsList : ComponentSettingsList<PrivateComponentSettingsList, IInstrument>
        {

        }

        [Test]
        public void SerializeDeserializePrivateType()
        {
            var settings = new PrivateComponentSettingsList();
            settings.Add(new ScpiDummyInstrument() { VisaAddress = "test" });
            var xml = new TapSerializer().SerializeToString(settings);

            var settings2 = (PrivateComponentSettingsList)(new TapSerializer().DeserializeFromString(xml, TypeData.FromType(typeof(PrivateComponentSettingsList))));
            var inst = (ScpiDummyInstrument)settings2[0];
            Assert.AreEqual("test", inst.VisaAddress);
        }

        [Test]
        public void SerializeDeserializeNullResource()
        {
            IInstrument instr = new ScpiDummyInstrument();
            InstrumentSettings.Current.Add(instr);
            try
            {
                ScpiTestStep step = new ScpiTestStep();
                step.Instrument = null;
                var xml = new TapSerializer().SerializeToString(step);
                Assert.IsTrue(xml.Contains($"<Instrument />"));
                var deserializedStep = new TapSerializer().DeserializeFromString(xml) as ScpiTestStep;
                Assert.AreEqual(step.Instrument, deserializedStep.Instrument);
            }
            finally
            {
                InstrumentSettings.Current.Remove(instr);
            }
        }

        public class StringObject
        {
            public string TheString { get; set; }
        }

        [Test]
        public void SerializeDeserializeProblematicString()
        {
            {
                StringBuilder sb = new StringBuilder("test:");
                var ser = new TapSerializer();
                for (int i = 0; i < 512; i++)
                    sb.Append((char) i);

                var st = new StringObject() {TheString = sb.ToString()};
                var stt = ser.SerializeToString(st);
                var rev = (StringObject) ser.DeserializeFromString(stt);
                Assert.IsTrue(string.Compare(st.TheString, rev.TheString) == 0);
                
            }
            {
                StringBuilder sb = new StringBuilder("test::::");
                var ser = new TapSerializer();
                for (int i = 0; i < 512; i++)
                    sb.Append((char) i);
                var st = new StringObject() { TheString = sb.ToString() };
                var stt = ser.SerializeToString(st);
                var rev = (StringObject)ser.DeserializeFromString(stt);
                Assert.IsTrue(string.Compare(st.TheString, rev.TheString) == 0);
                
            }

        }

        public class SimpleClass
        {
            public SimpleClass(object obj)
            {
                
            }
        }
        public class UnbalancedListStep : TestStep
        {
            [Display("Tx Command Sequences", "A predefined list of TX command sequences to control the DUT.",
                Order: 10.2)]
            public IReadOnlyList<SimpleClass> ReadOnlyList { get; set; } = new List<SimpleClass> {new SimpleClass(default), new SimpleClass(default)}.AsReadOnly();

            public override void Run()
            {
                throw new NotImplementedException();
            }
        }
        
        [Test]
        public void DeserializeUnbalancedList()
        {
            using (Session.Create())
            {
                var trace = new EngineUnitTestUtils.TestTraceListener();
                Log.AddListener(trace);

                var plan = new TestPlan();
                plan.ChildTestSteps.Add(new UnbalancedListStep());

                var ser = new TapSerializer();
                var planXml = ser.SerializeToString(plan);
                CollectionAssert.IsEmpty(ser.Errors);

                var currentLength = planXml.Length;

                var itemElem = $"<{nameof(SimpleClass)}></{nameof(SimpleClass)}>";
                // Remove one of the two items from the xml
                planXml = planXml.Remove(planXml.IndexOf(itemElem, StringComparison.Ordinal), itemElem.Length);
                
                // Ensure an element was actually removed
                Assert.AreEqual(currentLength - itemElem.Length, planXml.Length);
                
                var deserializedPlan = ser.DeserializeFromString(planXml) as TestPlan;

                CollectionAssert.IsEmpty(ser.Errors);

                // Deserialization should succeed even though we expect an element which was missing
                Assert.IsTrue(
                    deserializedPlan.ChildTestSteps.First() is UnbalancedListStep s && s.ReadOnlyList.Count == 2,
                    "Expected list to contain 2 element.");
                
                Assert.AreEqual(1, trace.WarningMessage.Count);
                CollectionAssert.Contains(trace.WarningMessage, "Deserialized unbalanced list.");
            }
        }
    }
}