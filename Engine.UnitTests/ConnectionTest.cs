//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class ConnectionTest
    {

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

        
        
        public class ViaPointCollection : IReadOnlyList<ViaPoint>
        {
            readonly SwitchPosition[] points;
            public ViaPointCollection(Instrument device, int count)
            {
                points = Enumerable.Range(1, count)
                    .Select(x => new SwitchPosition(device, $"Switch position {x}"))
                    .ToArray();
            }

            public IEnumerator<ViaPoint> GetEnumerator() => points.OfType<ViaPoint>().GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => points.GetEnumerator();

            public int Count => points.Length;

            public ViaPoint this[int index] => points[index];
        }

        public class TestRevolverSwitchInstrument : Instrument
        {
            public IReadOnlyList<ViaPoint> Ports { get; set; }
            public Port A { get; set; } 

            public TestRevolverSwitchInstrument()
            {
                Ports = new ViaPointCollection(this, 6);
                A = new OutputPort(this, "A");
            }
        }
    
        [Test]
        public void TestRevolverSwitchInstrumentSerialization()
        {
            var instrument = new TestRevolverSwitchInstrument();
            instrument.Ports[0].Alias = "test";
            instrument.A.Alias = "test2";
            var serializer = new TapSerializer();
            var xml = serializer.SerializeToString(instrument);
            var instrument2 = (TestRevolverSwitchInstrument) new TapSerializer().DeserializeFromString(xml);
            Assert.AreEqual(instrument.Ports[0].Alias, instrument2.Ports[0].Alias);
            Assert.AreEqual(instrument.A.Alias, instrument2.A.Alias);
        }        
    }
}
