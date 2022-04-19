//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
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
            
            public Port B { get; set; }

            public TestRevolverSwitchInstrument()
            {
                SwitchPositions = new ViaPointCollection(this, 6);
                A = new RevolverSwitchPort(this, "A");
                B = new Port(this, "B");
            }
        }
        
        public class RevolverSwitchPort : OutputPort
        {
            public string Alias { get; set; }
            public RevolverSwitchPort(IResource device, string name) : base(device, name)
            {
                
            }
        }

        public class PortAliasTypeDataProvider : IStackedTypeDataProvider
        {
            public static bool Enabled = false;

            public ITypeData GetTypeData(string identifier, TypeDataProviderStack stack)
            {
                if (Enabled == false) return null;
                var portType = stack.GetTypeData(identifier);
                if (portType.DescendsTo(typeof(Port)))
                {
                    return new PortAliasTypeData(portType);
                }

                return null;
            }

            public ITypeData GetTypeData(object obj, TypeDataProviderStack stack)
            {
                if (obj is Port)
                {
                    var inner = stack.GetTypeData(obj);
                    return new PortAliasTypeData(inner);
                }

                return null;
            }

            public double Priority => 2;
        }

        class PortAliasTypeData : ITypeData
        {
            public PortAliasTypeData(ITypeData baseType) => BaseType = baseType;
            static PortAliasMember alias = new PortAliasMember();
            public IEnumerable<object> Attributes => BaseType.Attributes;
            public string Name => "alias:" + BaseType.Name;
            public ITypeData BaseType { get; }
            public IEnumerable<IMemberData> GetMembers() =>  new []{(IMemberData)alias}.Concat(BaseType.GetMembers());

            public IMemberData GetMember(string name)
            {
                if (name == alias.Name)
                    return alias;
                return BaseType.GetMember(name);
            }

            public object CreateInstance(object[] arguments) => BaseType.CreateInstance(arguments);

            public bool CanCreateInstance => BaseType.CanCreateInstance;
        }

        class PortAliasMember : IMemberData
        {
            public IEnumerable<object> Attributes => Array.Empty<object>();
            public string Name => "Port.Alias";
            public ITypeData DeclaringType { get; } = TypeData.FromType(typeof(Port));
            public ITypeData TypeDescriptor { get; } = TypeData.FromType(typeof(string));
            public bool Writable { get; } = true;
            public bool Readable { get; } = true;
            ConditionalWeakTable<object, string> values = new ConditionalWeakTable<object, string>();
            public void SetValue(object owner, object value)
            {
                values.Remove(owner);
                values.Add(owner, (string)value);
            }

            public object GetValue(object owner)
            {
                if (values.TryGetValue(owner, out var str))
                    return str;
                return "";
            }
        }
        
    
        [Test]
        public void TestRevolverSwitchInstrumentSerialization()
        {
            PortAliasTypeDataProvider.Enabled = true;
            try
            {
                var instrument = new TestRevolverSwitchInstrument();
                instrument.SwitchPositions[0].Alias = "test";
                instrument.A.Alias = "test2";
                var portType = TypeData.GetTypeData(instrument.B);
                var aliasMember = portType.GetMember("Port.Alias");
                aliasMember.SetValue(instrument.B, "test3");
                var serializer = new TapSerializer();
                var xml = serializer.SerializeToString(instrument);
                var instrument2 = (TestRevolverSwitchInstrument) new TapSerializer().DeserializeFromString(xml);
                Assert.AreEqual(instrument.SwitchPositions[0].Alias, instrument2.SwitchPositions[0].Alias);
                Assert.AreEqual(instrument.A.Alias, instrument2.A.Alias);
                Assert.AreEqual(aliasMember.GetValue(instrument.B), aliasMember.GetValue(instrument2.B));
                Assert.AreEqual(aliasMember.GetValue(instrument.B), "test3");
            }
            finally
            {
                PortAliasTypeDataProvider.Enabled = false;
            }
        }        
    }
}
