using System;
using System.Xml.Linq;
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class ResourceReference
    {
        public class RefListener : ResultListener
        {
            public IInstrument InstrRef { get; set; }
        }

        [Test]
        public void SerializeResourceRefInResource()
        {
            try
            {
                // Make sure we have two profiles with a SCPIInstrument in each:
                ComponentSettings.SetSettingsProfile("Bench", "Test1");
                InstrumentSettings.Current.Clear();
                InstrumentSettings.Current.Add(new DummyInstrument { Name = "Dum" });
                InstrumentSettings.Current.Add(new ScpiInstrument { Name = "INST1" });
                InstrumentSettings.Current.Save();
                ComponentSettings.SetSettingsProfile("Bench", "Test2");
                InstrumentSettings.Current.Clear();
                InstrumentSettings.Current.Add(new ScpiInstrument { Name = "INST2" });
                InstrumentSettings.Current.Save();

                // Add a RefListener to ResultSettings
                var res = new RefListener();
                ResultSettings.Current.RemoveIf<IResultListener>(l => l is RefListener);
                ResultSettings.Current.Add(res);
                res.InstrRef = InstrumentSettings.Current.GetDefault<ScpiInstrument>();
                ResultSettings.Current.Save();

                Assert.AreEqual("INST2", res.InstrRef.Name);

                // Switch profile
                ComponentSettings.SetSettingsProfile("Bench", "Test1");

                // Reload ResultSettings
                var invRes = ResultSettings.Current.GetDefault<RefListener>();

                // Check that Instument reference now refers to instument from other profile
                Assert.IsNotNull(invRes.InstrRef);
                Assert.AreEqual("INST1", invRes.InstrRef.Name);
            }
            finally
            {
                ResultSettings.Current.Clear();
                ResultSettings.Current.Save();
                InstrumentSettings.Current.Clear();
            }
        }

        class CloneEnforcer : ITapSerializerPlugin
        {
            public double Order => 10;

            public bool Deserialize(XElement node, ITypeData t, Action<object> setter)
            {
                return false;
            }

            public Object TheObject;
            public bool Serialize(XElement node, object obj, ITypeData expectedType)
            {
                if (obj == TheObject)
                {
                    node.SetAttributeValue("Source", "None");
                }
                return false;
            }
        }

        [Test]
        public void SkipSerializePluginResourceTest()
        {
            var newinst = new LogResultListener() { FilePath = new MacroString() { Text = "hello" } };
            ResultSettings.Current.Add(newinst);
            var serializer = new TapSerializer();

            var clone1 = (LogResultListener)serializer.Clone(newinst); 
            // not actually a clone.
            Assert.AreSame(newinst, clone1);
            Assert.AreEqual(newinst.FilePath.Text, clone1.FilePath.Text);

            serializer.AddSerializers(new[] { new CloneEnforcer() { TheObject = newinst } });

            // a memberwise clone.
            var clone2 = (LogResultListener)serializer.Clone(newinst);
            Assert.AreNotSame(newinst, clone2);
            Assert.AreEqual(newinst.FilePath.Text, clone1.FilePath.Text);
        }

    }
}