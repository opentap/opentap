using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using OpenTap.Engine.UnitTests.TestTestSteps;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class StringConvertProviderTest
    {
        void ObjectAssertEqual(object expected, object actual)
        {
            Assert.AreEqual(expected, actual);
        }

        void ObjectAssertEqual(IEnabled expected, IEnabled actual)
        {
            Assert.AreEqual(expected.IsEnabled, actual.IsEnabled);
            dynamic _expected = expected, _actual = actual;
            ObjectAssertEqual(_expected.Value, _actual.Value);

        }

        void ObjectAssertEqual(IEnumerable expected, IEnumerable actual)
        {
            var l1 = expected.Cast<dynamic>().ToArray();
            var l2 = actual.Cast<dynamic>().ToArray();
            Assert.AreEqual(l1.Length, l2.Length);
            for (int i = 0; i < l1.Length; i++)
            {
                ObjectAssertEqual(l1[i], l2[i]);
            }
        }

        void DynObjectAssertEqual(dynamic expected, dynamic actual)
        {
            ObjectAssertEqual(expected, actual);
        }

        void testStringConvert(object value)
        {
            var strfmt = StringConvertProvider.GetString(value);
            var reparse = StringConvertProvider.FromString(strfmt, TypeData.FromType(value.GetType()), null);
            DynObjectAssertEqual(value, reparse);
        }

        [Test]
        public void StringConvertTest()
        {
            // IConvertible
            testStringConvert(5);
            testStringConvert("ASD");
            testStringConvert("");
            testStringConvert("µX_y_zµ");
            testStringConvert(new DateTime(2017, 3, 3, 3, 3, 3, 0)); // note: milliseconds are not parsed into the datetime.

            // Enabled
            testStringConvert(new Enabled<int>() { Value = 10, IsEnabled = false });
            testStringConvert(new Enabled<int>() { Value = 20, IsEnabled = true });

            // Enum
            testStringConvert(Verdict.Fail);
            testStringConvert(Verdict.Pass);
            testStringConvert(EngineSettings.AbortTestPlanType.Step_Error | EngineSettings.AbortTestPlanType.Step_Fail);

            // Resources
            var instr1 = new TestPortInstrument() { Name = "MyInstr" };

            var instr2 = new TestPortInstrument() { Name = "MyInstr 2" };
            var dut1 = new FourPortDut() { Name = "MyDut1" };

            InstrumentSettings.Current.Add(instr1);
            InstrumentSettings.Current.Add(instr2);
            DutSettings.Current.Add(dut1);
            try
            {
                testStringConvert(instr1);
                testStringConvert(instr2);
                testStringConvert(dut1);
                testStringConvert(new List<IResource> { instr1, instr2, dut1 });
                testStringConvert(new List<IResource> { });
            }
            finally
            {
                InstrumentSettings.Current.Remove(instr1);
                InstrumentSettings.Current.Remove(instr2);
                DutSettings.Current.Remove(dut1);
            }

            // Collections.
            testStringConvert(new Verdict[] { Verdict.Pass, Verdict.Fail, Verdict.Aborted });
            testStringConvert(new LogSeverity[] { LogSeverity.Debug, LogSeverity.Error, LogSeverity.Warning });
            testStringConvert(new EngineSettings.AbortTestPlanType[] { EngineSettings.AbortTestPlanType.Step_Error, EngineSettings.AbortTestPlanType.Step_Error | EngineSettings.AbortTestPlanType.Step_Fail });
            testStringConvert(new double[] { });
            testStringConvert(new double[] { 1, 2, 3, 4, 7, 8, 9, 0 });
            testStringConvert(new List<int> { 1, 2, 3, 4, 7, 8, 9, 0 });
            testStringConvert(new List<string> { "A A\"\" A A,", "B \" B B,", "C,D", "" });
            testStringConvert(new List<Verdict> { Verdict.Pass, Verdict.Fail,Verdict.Pass,Verdict.Aborted });
            testStringConvert(new List<Verdict> { });
            testStringConvert(new List<EngineSettings.AbortTestPlanType> { EngineSettings.AbortTestPlanType.Step_Error | EngineSettings.AbortTestPlanType.Step_Fail, EngineSettings.AbortTestPlanType.Step_Error });

            var reparse = (Verdict)StringConvertProvider.FromString("pass", TypeData.FromType(typeof(Verdict)), null);
            Assert.AreEqual(Verdict.Pass, reparse);
        }

        class SubObjTest
        {
            public double X { get; set; } 
            public double Y { get; set; } 
        } 

    
        [Test]
        public void FailingStringConvertTest()
        {
            // this should _not_ work
            var subobj = new SubObjTest();
            List<SubObjTest> subObjects = new List<SubObjTest>() {subobj};
            bool passed = StringConvertProvider.TryGetString(subObjects, out string result);
            Assert.IsFalse(passed);
        }

        public class AnyObjectClass : TestStep
        {
            public object Item { get; set; }
            public override void Run()
            {
                
            }
        }

        public struct Vec3d
        {
            public double X, Y, Z;
        }


        // To fully support IPaddresses, we also need to be able to serialize/deserialize them.
        // This plugin class takes care of that.
        public class Vec3dSerializer : ITapSerializerPlugin
        {
            public double Order => 5;

            public bool Deserialize(XElement node, ITypeData t, Action<object> setter)
            {
                if(t == TypeData.FromType(typeof(Vec3d)) == false) return false;
                var values = node.Value.Trim().TrimStart('(').TrimEnd(')')
                    .Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(double.Parse).ToArray();
                setter(new Vec3d{X = values[0], Y = values[1], Z = values[2]});
                return true;
            }

            public bool Serialize(XElement node, object obj, ITypeData expectedType)
            {
                if(obj is Vec3d v)
                {
                    node.Value = $"({v.X} {v.Y} {v.Z})";
                    return true;
                }
                return false;
            }
        }

        [Test]
        public void AnyObjectSerializeTest()
        {
            var obj = new AnyObjectClass() {Item = new Vec3d(){Y = 10}};
            var plan = new TestPlan();
            plan.Steps.Add(obj);
            TypeData.GetTypeData(obj).GetMember("Item").Parameterize(plan, obj, "Item");
            
            var str = new TapSerializer().SerializeToString(plan);
            var obj2 = (TestPlan)new TapSerializer().DeserializeFromString(str);
            var externalParameter = obj2.ExternalParameters.Get("Item");
            Assert.IsNotNull(externalParameter);
            Assert.AreEqual(10.0, ((Vec3d) externalParameter.Value).Y);
        }

        [Test]
        public void DeserializeLostInputProperty()
        {
            var plan1 = new TestPlan();
            var repeat = new RepeatStep();
            var log = new LogStep();
            plan1.ChildTestSteps.Add(repeat);
            repeat.ChildTestSteps.Add(log);
            var member = TypeData.GetTypeData(log).GetMember(nameof(LogStep.LogMessage));
            var outputMember = TypeData.GetTypeData(repeat).GetMember(nameof(RepeatStep.IterationInfo));
            InputOutputRelation.Assign(log, member, repeat, outputMember);

            var steps = new ITestStep[] {repeat, log};
            var xml = new TapSerializer().SerializeToString(steps);

            var ser2 = new TapSerializer();
            
            var deserialized = (ITestStep[]) ser2.DeserializeFromString(xml);
            var plan = new TestPlan();
            plan.Steps.AddRange(deserialized);
            
            var repeat2 = deserialized[0];
            var delay2 = deserialized[1];
            Assert.IsTrue(InputOutputRelation.IsOutput(repeat2, outputMember));
            Assert.IsTrue(InputOutputRelation.IsInput(delay2, member));
        }
    }
}