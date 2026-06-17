//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace OpenTap
{
    /// <summary>
    /// Represents a specific setting/mode/position of a switch.
    /// </summary>
    public class SwitchPosition : ViaPoint
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SwitchPosition"/> class.
        /// </summary>
        public SwitchPosition(Instrument device, string name)
        {
            Name = name;
            Device = device;
        }
    }

    /// <summary>
    /// Base class representing a point through which a connection passes. There is a list of these in <see cref="Connection.Via"/>.
    /// These usually represent a state that a connection switch element/instrument can be in. Implementations include <see cref="SwitchPosition"/> and <see cref="SwitchMatrixPath"/>
    /// </summary>
    public abstract class ViaPoint : IEquatable<ViaPoint>, IConstResourceProperty, IViaPoint
    {
        /// <summary>
        /// The name of this state/mode/position in the switch. (Should be unique among <see cref="ViaPoint"/> objects on the same device/resource).
        /// </summary>
        public virtual string Name { get; protected set; }

        /// <summary>
        /// Indicates whether the switch is currently in this position. 
        /// Should be set by the Device implementation.
        /// </summary>
        [XmlIgnore]
        public virtual bool IsActive { get; set; }

        /// <summary>
        /// The device (usually an <see cref="Instrument"/>) on which this switch position exists.
        /// </summary>
        [XmlIgnore]
        public IResource Device { get; set; }
        
        /// <summary>
        /// Returns a string describing this switch position (string.Format("{0}.{1}", this.Device.Name, this.Name)).
        /// </summary>
        public override string ToString()
        {
            if (Device != null)
                return $"{Device.Name}.{Name}";
            return Name;
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        public override int GetHashCode()
        {
            return this.Name.GetHashCode() ^ this.Device.GetHashCode();
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj.GetType().IsSubclassOf(typeof(ViaPoint)))
            {
                ViaPoint other = (ViaPoint)obj;
                return this.Name == other.Name && this.Device == other.Device;
            }
            return false;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        public virtual bool Equals(ViaPoint other)
        {
            if (other == null)
                return false;
            return this.Name == other.Name && this.Device == other.Device;
        }
    }


    /// <summary>
    /// Represents a specific path through a switch matrix.
    /// </summary>
    public class SwitchMatrixPath : ViaPoint
    {
        /// <summary>
        /// Row in the matrix that describes this path 
        /// </summary>
        public int Row { get; private set; }
        /// <summary>
        /// Column in the matrix that describes this path 
        /// </summary>
        public int Column { get; private set; }

        /// <summary>
        /// The name of this switch path. (Note the name uses 1-based indexing to refer to the Row/Column)
        /// </summary>
        public override string Name { get => $"R{Row+1}\u2194C{Column+1}"; protected set => throw new NotSupportedException(); }
        /// <summary>
        /// Initializes a new instance of the <see cref="SwitchPosition"/> class.
        /// </summary>
        public SwitchMatrixPath(Instrument device, int row, int column)
        {
            Device = device;
            Row = row;
            Column = column;
        }
    }

    /// <summary>
    /// Collecion of <see cref="SwitchMatrixPath"/>s that belong to a switch matrix. 
    /// This is a lazy collection that is only populated with actual <see cref="SwitchMatrixPath"/> when each element is accessed or the 
    /// </summary>
    public class SwitchMatrixPathCollection : IEnumerable<SwitchMatrixPath>
    {
        /// <summary>
        /// Enumerator for <see cref="SwitchMatrixPathCollection"/>
        /// </summary>
        private class SwitchMatrixPathCollectionEnumerator : IEnumerator<SwitchMatrixPath>
        {
            SwitchMatrixPathCollection col;
            int currentRow = -1; // Enumerators are positioned before the first element until the first MoveNext() call.
            int currentColumn = 0;

            /// <summary>
            /// Gets the element in the collection at the current position of the enumerator.
            /// </summary>
            public SwitchMatrixPath Current => col.Get(currentRow, currentColumn);

            object IEnumerator.Current => Current;

            /// <summary>
            /// Advances the enumerator to the next element of the collection.
            /// </summary>
            /// <returns>
            /// true if the enumerator was successfully advanced to the next element; false if
            /// the enumerator has passed the end of the collection.
            /// </returns>
            public bool MoveNext()
            {
                if (currentRow < col.RowCount-1)
                {
                    currentRow++;
                    return true;
                }
                if (currentColumn < col.ColumnCount-1)
                {
                    currentColumn++;
                    currentRow = 0;
                    return true;
                }
                return false;
            }

            /// <summary>
            /// Sets the enumerator to its initial position, which is before the first element
            ///     in the collection.
            /// </summary>
            public void Reset()
            {
                currentRow = -1;
                currentColumn = 0;
            }

            /// <summary>
            /// Has no effect in this implementation.
            /// </summary>
            public void Dispose()
            {
                //nothing to do here
            }

            internal SwitchMatrixPathCollectionEnumerator(SwitchMatrixPathCollection collection)
            {
                this.col = collection;
            }
        }

        private Dictionary<(int, int), SwitchMatrixPath> paths = new Dictionary<(int, int), SwitchMatrixPath>();
        private Instrument device;
        /// <summary>
        /// Number of row in the switch matrix
        /// </summary>
        public int RowCount { get; private set; }
        /// <summary>
        /// Number of columns in the switch matrix
        /// </summary>
        public int ColumnCount { get; private set; }

        /// <summary>
        /// Gets the <see cref="SwitchMatrixPath"/> corresponding to a particular row and column in the matrix.
        /// </summary>
        public SwitchMatrixPath this[int row,int column]
        {
            get
            {
                return Get(row,column);
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="SwitchMatrixPathCollection"/>
        /// </summary>
        /// <param name="device"></param>
        /// <param name="rowCount"></param>
        /// <param name="columnCount"></param>
        public SwitchMatrixPathCollection(Instrument device, int rowCount, int columnCount)
        {
            this.device = device;
            this.RowCount = rowCount;
            this.ColumnCount = columnCount;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        public IEnumerator<SwitchMatrixPath> GetEnumerator()
        {
            return new SwitchMatrixPathCollectionEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new SwitchMatrixPathCollectionEnumerator(this);
        }

        /// <summary>
        /// Gets the <see cref="SwitchMatrixPath"/> corresponding to a particular row and column in the matrix.
        /// </summary>
        private SwitchMatrixPath Get(int row, int column)
        {
            if (row >= RowCount || row < 0)
                throw new ArgumentOutOfRangeException("row");
            if (column >= ColumnCount || column < 0)
                throw new ArgumentOutOfRangeException("column");
            SwitchMatrixPath path;
            if (paths.TryGetValue((row, column),out path))
            {
                return path;
            }
            path = new SwitchMatrixPath(device, row, column);
            paths.Add((row, column), path);
            return path;
        }
    }
}
