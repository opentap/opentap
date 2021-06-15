//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using Mono.Collections.Generic;
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
            con.CableLoss.Add(new RfConnection.CableLossPoint {Frequency = 100, Loss = 2});
            con.CableLoss.Add(new RfConnection.CableLossPoint {Frequency = 200, Loss = 4});
            double loss = con.GetInterpolatedCableLoss(150);
            Assert.AreEqual(3, loss);

            con = new RfConnection();
            con.CableLoss.Add(new RfConnection.CableLossPoint {Frequency = 100, Loss = 0});
            con.CableLoss.Add(new RfConnection.CableLossPoint {Frequency = 200, Loss = 4});
            loss = con.GetInterpolatedCableLoss(175);
            Assert.AreEqual(3, loss);

            // check extrapolation (zero order):
            con = new RfConnection();
            con.CableLoss.Add(new RfConnection.CableLossPoint {Frequency = 100, Loss = 2});
            con.CableLoss.Add(new RfConnection.CableLossPoint {Frequency = 200, Loss = 4});
            loss = con.GetInterpolatedCableLoss(300);
            Assert.AreEqual(4, loss);
            loss = con.GetInterpolatedCableLoss(50);
            Assert.AreEqual(2, loss);

            // check exact match
            con = new RfConnection();
            con.CableLoss.Add(new RfConnection.CableLossPoint {Frequency = 100, Loss = 2});
            con.CableLoss.Add(new RfConnection.CableLossPoint {Frequency = 200, Loss = 4});
            con.CableLoss.Add(new RfConnection.CableLossPoint {Frequency = 300, Loss = 5});
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

            public IEnumerator<RevolverSwitchPosition> GetEnumerator() =>
                points.OfType<RevolverSwitchPosition>().GetEnumerator();

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

        public class SubViaPoint
        {
            public string Name { get; protected set; }
            public override string ToString() => Name;
        }

        [DebuggerDisplay("{Name}")]
        public class RowViaPoint : SubViaPoint
        {
            public int RowNumber { get; }

            public RowViaPoint(int rowNumber)
            {
                RowNumber = rowNumber;
                Name = $"Row {RowNumber}";
            }

            public override bool Equals(object obj)
            {
                if (obj is RowViaPoint r) return r.RowNumber == RowNumber;
                return base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return RowNumber.GetHashCode();
            }
        }

        [DebuggerDisplay("{Name}")]
        public class ColumnViaPoint : SubViaPoint
        {
            public int ColumnNumber { get; }
            public ColumnViaPoint(int columnNumber)
            {
                ColumnNumber = columnNumber;
                Name = $"Column {ColumnNumber}";
            }
        }

        public class Matrix2ViaPoint : ViaPoint
        {
            public new string Name
            {
                get => base.Name;
                set => base.Name = value;
            }

            public IEnumerable<ColumnViaPoint> AvailableColumns => matrix.Columns;

            [AvailableValues(nameof(AvailableColumns))]
            [XmlIgnore]
            public ColumnViaPoint Column { get; set; }
            
            [Browsable(false)]
            public int ColumId
            {
                get => Column.ColumnNumber;
                set => Column = AvailableColumns.First(x => x.ColumnNumber == value);
            }

            public IEnumerable<RowViaPoint> AvailableRows => matrix.Rows;

            [AvailableValues(nameof(AvailableRows))]
            [XmlIgnore]
            public RowViaPoint Row { get; set; }

            [Browsable(false)]
            public int RowId
            {
                get => Row.RowNumber;
                set => Row = AvailableRows.First(x => x.RowNumber == value);
            }
            SwitchMatrix2 matrix;

            public Matrix2ViaPoint(SwitchMatrix2 matrix)
            {
                Device = matrix;
                this.matrix = matrix;
                Name = "New Via Point";
                Row = matrix.Rows.FirstOrDefault();
                Column = matrix.Columns.FirstOrDefault();
            }
        }

        public class Matrix2ViaPointCollection : List<Matrix2ViaPoint>, ICreateElement
        {
            SwitchMatrix2 instrument;

            public Matrix2ViaPointCollection(SwitchMatrix2 instrument)
            {
                this.instrument = instrument;
            }

            public object CreateElement() => new Matrix2ViaPoint(instrument);
        }

        public class SwitchMatrix2 : Instrument
        {
            public ReadOnlyCollection<RowViaPoint> Rows { get; }
            public ReadOnlyCollection<ColumnViaPoint> Columns { get; }

            [DeserializeInPlace] [NoReference] public Matrix2ViaPointCollection ViaPoints { get; set; }

            public SwitchMatrix2()
            {
                Rows = new ReadOnlyCollection<RowViaPoint>(Enumerable.Range(0, 128).Select(i => new RowViaPoint(i))
                    .ToArray());
                Columns = new ReadOnlyCollection<ColumnViaPoint>(Enumerable.Range(0, 128)
                    .Select(i => new ColumnViaPoint(i))
                    .ToArray());
                ViaPoints = new Matrix2ViaPointCollection(this);
            }
        }

        [Test]
        public void SubSwitchMatrixTest()
        {
            using (Session.Create(SessionOptions.OverlayComponentSettings))
            {
                var matrix = new SwitchMatrix2();
                var viaPoints = AnnotationCollection.Annotate(matrix).GetMember(nameof(matrix.ViaPoints));
                Assert.IsNull(viaPoints.Get<IAvailableValuesAnnotation>());
                var viaPointsCol = viaPoints.Get<ICollectionAnnotation>();
                var newElement = viaPointsCol.NewElement();
                viaPointsCol.AnnotatedElements = new[] {newElement};
                viaPoints.Write();
                Assert.AreEqual(1, matrix.ViaPoints.Count());

                var connection = new RfConnection();

                ConnectionSettings.Current.Add(connection);
                InstrumentSettings.Current.Add(matrix);

                var availableVias = AnnotationCollection.Annotate(connection).GetMember(nameof(connection.Via))
                    .Get<IAvailableValuesAnnotation>()
                    .AvailableValues.OfType<ViaPoint>();
                Assert.IsTrue(availableVias.Contains(matrix.ViaPoints.FirstOrDefault()));
                
                using (Session.Create(SessionOptions.OverlayComponentSettings))
                {
                    var innerMatrix = InstrumentSettings.Current.OfType<SwitchMatrix2>().First();
                    Assert.IsFalse(ReferenceEquals(innerMatrix, matrix));
                    Assert.AreEqual(1, innerMatrix.ViaPoints.Count);
                }
            }
        }
    }
}
