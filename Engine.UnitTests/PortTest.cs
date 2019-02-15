//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using OpenTap;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap.Engine.UnitTests
{
    public class TestPort : Port
    {
        public double PortImpedance { get; set; }

        public TestPort(IResource res, string name) : base(res, name)
        {
            PortImpedance = 50;
        }
    }

    public class TestPortInstrument : Instrument
    {
        [XmlIgnore]
        public TestPort Port1 { get; set; }
        [XmlIgnore]
        public TestPort Port2 { get; set; }
        public TestPortInstrument()
        {
            Port1 = new TestPort(this, "Port1");
            Port2 = new TestPort(this, "Port2");
        }

    }

    public class TestPortStep : TestStep
    {
        public IEnumerable<Port> AvailPorts
        {
            get { return InstrumentSettings.Current.OfType<TestPortInstrument>().SelectMany(inst => new Port[] { inst.Port1, inst.Port2 }); }
        }

        Port selectedPort;
        [AvailableValues("AvailPorts")]
        public Port SelectedPort { get { return selectedPort; } set { selectedPort = value; } }
        public override void Run()
        {
            Assert.IsTrue(SelectedPort is TestPort);
        }
    }

    [TestFixture]
    public class PortTest
    {
        [Test]
        public void TestPortSerialization()
        {
            var currentSettingsDir = ComponentSettings.GetSettingsDirectory("Bench");
            // Try to invoke the ComponentSettings serializer/deserializer for connections
            // Important thing is that a TestPort is still a TestPort after serialization.
            ComponentSettings.SetSettingsProfile("Bench", "test1");
            InstrumentSettings.Current.ToList();
            TestPortInstrument Instr = new TestPortInstrument();
            InstrumentSettings.Current.Add(Instr);
            RfConnection conn = new RfConnection() { Port1 = Instr.Port1, Port2 = Instr.Port2 };
            ConnectionSettings.Current.Clear();
            ConnectionSettings.Current.Add(conn);
            ComponentSettings.SaveAllCurrentSettings();
            ComponentSettings.SetSettingsProfile("Bench", "test2");
            InstrumentSettings.Current.ToList(); // make componentsettings reload.
            ComponentSettings.SetSettingsProfile("Bench", "test1");

            var instr2 = InstrumentSettings.Current.OfType<TestPortInstrument>().First();
            Assert.IsFalse(object.ReferenceEquals(instr2, Instr));
            Instr = instr2;
            conn = ConnectionSettings.Current.OfType<RfConnection>().First(con => (con.Port1 is TestPort));

            // Now try to serialize a test plan with a Port inside it.
            try
            {
                TestPlan plan = new TestPlan { };
                plan.ChildTestSteps.Add(new TestPortStep() { SelectedPort = Instr.Port1 });
                TestPlan newplan;

                using (var str = new MemoryStream(1000))
                {
                    plan.Save(str);
                    str.Position = 0;
                    newplan = TestPlan.Load(str,plan.Path);
                }
                TestPortStep step = (TestPortStep)newplan.ChildTestSteps.First();
                Assert.IsTrue(step.SelectedPort is TestPort);
            }
            finally
            {
                // Cleanup
                ConnectionSettings.Current.Remove(conn);
                InstrumentSettings.Current.Remove(Instr);
                ComponentSettings.SaveAllCurrentSettings();

                ComponentSettings.SetSettingsProfile("Bench", currentSettingsDir);
            }
        }

    }
}
