//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap
{
    /// <summary>
    /// Indicates that a property on a <see cref="TestStep"/> (a step setting) should be visible within a Test Plan editor UI.
    /// </summary>
    public class ColumnDisplayNameAttribute : Attribute
    {
        /// <summary>
        /// The header name of the column.
        /// </summary>
        public string ColumnName { get; private set; }
        /// <summary>
        /// Used for managing the order in which the columns appear.
        /// </summary>
        public double Order { get; private set; }

        /// <summary>
        /// Whether the property as a column is read-only.
        /// </summary>
        public bool IsReadOnly { get; private set; }

        /// <summary> 
        /// Create a new instance of ColumnDisplayNameAttribute. 
        /// To be used on TestStep properties to show that they should appear in the test plan editor.
        /// </summary>
        /// <param name="ColumnName">The header name of the column.</param>
        /// <param name="Order">Used for managing the order in which the columns appear.</param>
        /// <param name="IsReadOnly">Whether the property as a column is read-only.</param>
        public ColumnDisplayNameAttribute(string ColumnName = null, double Order = 0, bool IsReadOnly = false)
        {
            this.ColumnName = ColumnName;
            this.Order = Order;
            this.IsReadOnly = IsReadOnly;
        }
    }
}
