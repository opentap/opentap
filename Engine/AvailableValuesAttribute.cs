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
        /// <summary> Gets the name of the property with available values.</summary>
        public string PropertyName { get; private set; }

        /// <summary>
        /// Creates a new AvailableValuesAttribute that points to a property by name.
        /// </summary>
        /// <param name="propertyName">The name of the property with the possible values.</param>
        public AvailableValuesAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }
    }
    /// <summary>
    /// Marks that a property can be selected from a list in the UI.
    /// Points to another property that contains the list of suggested values.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SuggestedValuesAttribute : Attribute
    {
        /// <summary> Gets the name of the property with suggested values.</summary>
        public readonly string PropertyName;
        /// <summary>
        /// Creates a new SuggestedValuesAttribute that points to a property by name.
        /// </summary>
        /// <param name="propertyName">The name of the property with the suggested values.</param>
        public SuggestedValuesAttribute(string propertyName)
        {
            this.PropertyName = propertyName;
        }
    }
}
