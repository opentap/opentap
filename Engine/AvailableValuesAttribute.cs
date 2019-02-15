//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap
{
    /// <summary>
    /// Marks that a property should be selected from a list in the UI.
    /// Points to another property that contains the list of possible values.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class AvailableValuesAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the property containing the available values.
        /// </summary>
        public string PropertyName { get; private set; }

        /// <summary>
        /// Creates a new AvailableValuesAttribute that points to a property by name.
        /// </summary>
        /// <param name="propertyName">The property name pointing to the available values.</param>
        public AvailableValuesAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }
    }
}
