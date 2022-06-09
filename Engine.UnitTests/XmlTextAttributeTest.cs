using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.Engine.UnitTests
{
    public class XmlTextAttributeTest
    {
        public enum Mode
        {
            A,
            B,
            C
        }

        public class SimpleXmlTestAttribute
        {
            [XmlText(Type = typeof(Mode))] public Mode Value { get; set; }
        }

        [Test]
        public void SimpleXmlTextAttributeTest()
        {
            var myGroup1 = new SimpleXmlTestAttribute {Value = Mode.C};
            var str = new TapSerializer().SerializeToString(myGroup1);
            var xml = XDocument.Load(new MemoryStream(Encoding.UTF8.GetBytes(str)));


            var elem = xml.Element("SimpleXmlTestAttribute");
            Assert.IsNotNull(elem);
            Assert.AreEqual(elem.Value, nameof(Mode.C));
            // Should not have child element Value since it is serialized as XmlText
            Assert.IsNull(elem.Element("Value"));
        }

        public class SerializeConnectionTestStep : TestStep
        {
            [AvailableValues(nameof(availableInstruments))]
            public Instrument myInstrument_withAvailableValues { get; set; }

            [AvailableValues(nameof(availableConnections))]
            public Connection myConnection_withAvailableValues { get; set; }

            public List<Connection> availableConnections
            {
                get { return ConnectionSettings.Current.Select(x => x as Connection).Where(c => c != null).ToList(); }
            }

            public List<Instrument> availableInstruments
            {
                get { return InstrumentSettings.Current.Select(x => x as Instrument).Where(c => c != null).ToList(); }
            }

            public override void Run() => throw new NotImplementedException();
        }

        [Test]
        public void SerializeComponentSettingsTest()
        {

            try
            {

                ConnectionSettings.Current.Add(new RfConnection());
                InstrumentSettings.Current.Add(new ScpiInstrument { VisaAddress = "1234" });
                
                var testPlan = new TestPlan();
                var step = new SerializeConnectionTestStep();
                testPlan.ChildTestSteps.Add(step);

                var conn = step.availableConnections.First();
                var instr = step.availableInstruments.First();

                Assert.NotNull(conn);
                Assert.NotNull(instr);

                step.myConnection_withAvailableValues = conn;
                step.myInstrument_withAvailableValues = instr;

                Assert.AreSame(step.myConnection_withAvailableValues, conn);
                Assert.AreSame(step.myInstrument_withAvailableValues, instr);

                byte[] planData;

                using (MemoryStream ms = new MemoryStream(20000))
                {
                    testPlan.Save(ms);
                    planData = ms.ToArray();
                }

                using (MemoryStream ms = new MemoryStream(planData))
                    testPlan = testPlan.Reload(ms);

                var newStep = testPlan.ChildTestSteps.First() as SerializeConnectionTestStep;

                Assert.NotNull(newStep);
                Assert.NotNull(newStep.myConnection_withAvailableValues);
                Assert.NotNull(newStep.myInstrument_withAvailableValues);
                Assert.AreSame(newStep.myInstrument_withAvailableValues, instr);
                Assert.AreSame(newStep.myConnection_withAvailableValues, conn);
            }
            finally
            {
                ConnectionSettings.Current.Clear();
                InstrumentSettings.Current.Clear();
            }

        }

        [Test]
        public void NullInstrumentTest()
        {
            object outValue = new ObjectCloner(null).Clone(true, null, TypeData.FromType(typeof(ScpiInstrument)));
            Assert.IsNull(outValue);
        }

        [Test]
        public void NullValueTypeTest()
        {
            Assert.Throws<InvalidCastException>(() => new ObjectCloner("asd").Clone(true, 1, TypeData.FromType(typeof(int))));
            Assert.Throws<InvalidCastException>(() => new ObjectCloner("").Clone(true, 1, TypeData.FromType(typeof(int))));
            Assert.DoesNotThrow(() => new ObjectCloner("123").Clone(true, 1, TypeData.FromType(typeof(int))));
        }

        public class InPlaceProperty : ValidatingObject
        {
            int x;
            public int X
            {
                get => x;
                set
                {
                    x = value;
                    OnPropertyChanged(nameof(X));
                } 
            }

            public InPlaceProperty()
            {
                
            }
        }
        public class InPlacePropertyOwner
        {
            InPlaceProperty prop = new InPlaceProperty();

            [DeserializeInPlace]
            public InPlaceProperty Prop
            {
                get => prop;
                set => throw new Exception("This should not be set.");
            }

            public int XChanged = 0;
            public InPlacePropertyOwner()
            {
                prop.PropertyChanged += PropOnPropertyChanged;
            }

            void PropOnPropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                XChanged += 1;
            }
        }
        
        [Test]
        public void TestInPlaceProperty()
        {
            var a = new InPlacePropertyOwner();
            a.Prop.X += 1;
            a.Prop.X += 2;
            var xml = new TapSerializer().SerializeToString(a);
            var b = (InPlacePropertyOwner) new TapSerializer().DeserializeFromString(xml);
            Assert.AreEqual(1, b.XChanged);
            Assert.AreEqual(3, b.Prop.X);
        }

        [Test]
        public void TestBase64EncodeDecode()
        {
            var testMessage = "(SCPI:TRIG1:SEQ:AIQM:DEL:STAT) ";
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(testMessage));
            
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(new DialogStep() { Message = "(SCPI:TRIG1:SEQ:AIQM:DEL:STAT) "});
            
            var ser = new TapSerializer();
            var planXml = ser.SerializeToString(plan);
            
            StringAssert.Contains($"<Base64>{base64}</Base64>", planXml);

            var newPlan = ser.DeserializeFromString(planXml) as TestPlan;

            var dialog = newPlan.ChildTestSteps.First() as DialogStep;
            Assert.AreEqual(testMessage, dialog.Message);
        }
        
        [Test]
        public void TestBase64DecodeWithNamespace()
        {
            var testMessage = "(SCPI:TRIG1:SEQ:AIQM:DEL:STAT) ";
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(testMessage));
            var ns = "http://opentap.io/schemas/package";
            var xml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<TestPlan type=""OpenTap.TestPlan"" Locked=""false"" xmlns=""{ns}"">
  <Steps>
    <TestStep type=""OpenTap.Plugins.BasicSteps.DialogStep"" Version=""9.4.0-Development"" Id=""d656ee5b-e764-4c6f-be9f-e4f28bcdee13"">
      <Message>
        <Base64>{base64}</Base64>
      </Message>
      <Title>Title</Title>
      <Buttons>OkCancel</Buttons>
      <PositiveAnswer>NotSet</PositiveAnswer>
      <NegativeAnswer>NotSet</NegativeAnswer>
      <UseTimeout>false</UseTimeout>
      <Timeout>5</Timeout>
      <DefaultAnswer>NotSet</DefaultAnswer>
      <Enabled>true</Enabled>
      <Name>Dialog</Name>
      <ChildTestSteps />
      <BreakConditions>Inherit</BreakConditions>
    </TestStep>
  </Steps>
  <BreakConditions>Inherit</BreakConditions>
  <OpenTap.Description />
  <Package.Dependencies />
</TestPlan>
";
            var ser = new TapSerializer();
            var plan = ser.DeserializeFromString(xml) as TestPlan;
            var dialog = plan.ChildTestSteps.First() as DialogStep;
            Assert.AreEqual(testMessage, dialog.Message);
        }

        [Test]
        public void TestSerializeProcessStepWithNullDefaultValue()
        {
            // When DefaulValue is used and the property value is null an exception was thrown.
            var plan = new TestPlan();
            var step = new ProcessStep {LogHeader = null};
            plan.ChildTestSteps.Add(step);
            plan.SerializeToString(true);
        }

        /// <summary> This class just inhertis from test plan </summary>
        [AllowAnyChild] 
        public class TestPlanTest2 : TestPlan
        {
            
        }

        /// <summary>
        /// A problem existed where if inheriting from TestPlan,
        /// Name was not set to the right value after deserializing from a stream.
        /// </summary>
        [Test]
        public void TestSerializeInheritTestPlan()
        {
            var tp = new TestPlanTest2();
            // this once threw an exception because of an error in ChildTestSteps.Add.
            tp.ChildTestSteps.Add(new DelayStep());
            var str = new MemoryStream();
            tp.Save(str);
            str.Seek(0, SeekOrigin.Begin);
            tp = (TestPlanTest2)TestPlan.Load(str, "testplantest2.TapPlan");
            // before the fix, this would be set "Untitled"
            Assert.AreEqual(tp.Name, "testplantest2");
        }
        
        [Test]
        public void TestSerializerErrors()
        {
            var x = new Enabled<Enabled<Enabled<int>>>();
            x.Value = new Enabled<Enabled<int>>();
            x.Value.Value = new Enabled<int>();
            x.Value.Value.Value = 4;
            var ser = new TapSerializer();
            var str = ser.SerializeToString(x);
            Assert.IsFalse(ser.XmlErrors.Any());
            var str2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <EnabledOfEnabledOfEnabledOfInt32 type=""OpenTap.Enabled`1[[OpenTap.Enabled`1[[OpenTap.Enabled`1[[System.Int32]], OpenTap, Version=9.4.0.0, Culture=neutral, PublicKeyToken=null]], OpenTap, Version=9.4.0.0, Culture=neutral, PublicKeyToken=null]]"">
                <Value>
                <Value>
                <Value>abc</Value>
                <IsEnabled>false</IsEnabled>
                </Value>
                <IsEnabled>false</IsEnabled>
                </Value>
                <IsEnabled>false</IsEnabled>
                </EnabledOfEnabledOfEnabledOfInt32>";
            var r= ser.DeserializeFromString(str2, TypeData.GetTypeData(x));
            Assert.AreEqual(1, ser.XmlErrors.Count());
            var err = ser.XmlErrors.First();
            // error was that asd could not be parsed as an int.
            Assert.AreEqual("abc", err.Element.Value);
            // error occured on line number 5
            Assert.AreEqual(5, (err.Element as IXmlLineInfo).LineNumber);
        }
    }
}