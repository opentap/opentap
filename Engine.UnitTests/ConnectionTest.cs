//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class ConnectionTest
    {
        public class VirtualPortInstrument : Instrument
        {
            public class VirtualPort : Port
            {   
                public VirtualPort(IResource device, int index) : base(device, "P" + index)
                {
                    Index = index;
                }
                public readonly int Index;
                public override bool Equals(object obj) => obj is VirtualPort vp && vp.Index == Index && vp.Device == Device;
                public override int GetHashCode() =>  (Device?.GetHashCode() ?? 0) + 13 * Index;
            }
            
            public int NPorts { get; set; } = 1;
            
            public IEnumerable<Port> Ports 
            {
                get
                {
                    for (int i = 0; i < NPorts; i++)
                        yield return new VirtualPort(this, i);
                }
            }    
        }
        
        /// <summary>
        /// This test show how virtual ports can be used (ports that represents the same without having reference equality).
        /// </summary>
        [Test]
        public void VirtualPortTest()
        {
            var instr = new VirtualPortInstrument()
            {
                NPorts = 2
            };

            var c1 = new RfConnection
            {
                Port1 = instr.Ports.First(),
                Port2 = instr.Ports.Last()
            };

            var p1 = instr.Ports.First();
            var p2 = instr.Ports.Last();
            
            Assert.AreNotEqual(p1,p2);
            Assert.AreEqual(c1.GetOtherPort(p1), p2);
            Assert.AreEqual(c1.GetOtherPort(p2), p1);
        }

        [Test]
        public void CableLossInterpolationTest()
        {
            // check linear (first order) interpolation:
            RfConnection con = new RfConnection();
            con.CableLoss.Add(new RfConnection.CableLossPoint { Frequency = 100, Loss = 2 });
            con.CableLoss.Add(new RfConnection.CableLossPoint { Frequency = 200, Loss = 4 });
            double loss = con.GetInterpolatedCableLoss(150);
            Assert.AreEqual(3, loss);

            con = new RfConnection();
            con.CableLoss.Add(new RfConnection.CableLossPoint { Frequency = 100, Loss = 0 });
            con.CableLoss.Add(new RfConnection.CableLossPoint { Frequency = 200, Loss = 4 });
            loss = con.GetInterpolatedCableLoss(175);
            Assert.AreEqual(3, loss);

            // check extrapolation (zero order):
            con = new RfConnection();
            con.CableLoss.Add(new RfConnection.CableLossPoint { Frequency = 100, Loss = 2 });
            con.CableLoss.Add(new RfConnection.CableLossPoint { Frequency = 200, Loss = 4 });
            loss = con.GetInterpolatedCableLoss(300);
            Assert.AreEqual(4, loss);
            loss = con.GetInterpolatedCableLoss(50);
            Assert.AreEqual(2, loss);

            // check exact match
            con = new RfConnection();
            con.CableLoss.Add(new RfConnection.CableLossPoint { Frequency = 100, Loss = 2 });
            con.CableLoss.Add(new RfConnection.CableLossPoint { Frequency = 200, Loss = 4 });
            con.CableLoss.Add(new RfConnection.CableLossPoint { Frequency = 300, Loss = 5 });
            loss = con.GetInterpolatedCableLoss(100);
            Assert.AreEqual(2, loss);
            loss = con.GetInterpolatedCableLoss(200);
            Assert.AreEqual(4, loss);
            loss = con.GetInterpolatedCableLoss(300);
            Assert.AreEqual(5, loss);
        }

        
        public class RevolverSwitchPosition : ViaPoint
        {
            public string Alias { get; set; }

            public RevolverSwitchPosition(IInstrument device, string name)
            {
                this.Name = name;
                this.Device = device;
            }
            
        }
        
        public class ViaPointCollection : IReadOnlyList<RevolverSwitchPosition>
        {
            readonly RevolverSwitchPosition[] points;
            public ViaPointCollection(Instrument device, int count)
            {
                points = Enumerable.Range(1, count)
                    .Select(x => new RevolverSwitchPosition(device, $"Switch position {x}"))
                    .ToArray();
            }

            public IEnumerator<RevolverSwitchPosition> GetEnumerator() => points.OfType<RevolverSwitchPosition>().GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => points.GetEnumerator();

            public int Count => points.Length;

            public RevolverSwitchPosition this[int index] => points[index];
        }

        public class TestRevolverSwitchInstrument : Instrument
        {
            public IReadOnlyList<RevolverSwitchPosition> SwitchPositions { get; set; }
            public RevolverSwitchPort A { get; set; } 

            public TestRevolverSwitchInstrument()
            {
                SwitchPositions = new ViaPointCollection(this, 6);
                A = new RevolverSwitchPort(this, "A");
            }
        }
        
        public class RevolverSwitchPort : OutputPort
        {
            public string Alias { get; set; }
            public RevolverSwitchPort(IResource device, string name) : base(device, name)
            {
                
            }
        }
    
        [Test]
        public void TestRevolverSwitchInstrumentSerialization()
        {
            var instrument = new TestRevolverSwitchInstrument();
            instrument.SwitchPositions[0].Alias = "test";
            instrument.A.Alias = "test2";
            var serializer = new TapSerializer();
            var xml = serializer.SerializeToString(instrument);
            var instrument2 = (TestRevolverSwitchInstrument) new TapSerializer().DeserializeFromString(xml);
            Assert.AreEqual(instrument.SwitchPositions[0].Alias, instrument2.SwitchPositions[0].Alias);
            Assert.AreEqual(instrument.A.Alias, instrument2.A.Alias);
        }        
    }
}
