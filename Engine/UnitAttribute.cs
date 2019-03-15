//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap
{
    /// <summary>
    /// Identifies that units should be assigned to a property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class UnitAttribute : Attribute, IAnnotation
    {
        /// <summary>
        /// The unit e.g "Hz".
        /// </summary>
        public string Unit { get; private set; }

        /// <summary>
        /// Whether to use engineering prefix. E.g 1000000 Hz -> 1 MHz
        /// </summary>
        public bool UseEngineeringPrefix { get; private set; }

        /// <summary>
        /// Pre scaling of values.
        /// </summary>
        public double PreScaling { get; private set; }

        /// <summary>
        /// The format argument to string.Format.
        /// </summary>
        public string StringFormat { get; private set; } 

        /// <summary>
        /// Whether to use ranges to show arrays of numbers. For example, show 1, 2, 3, 4 as 1 : 4.
        /// </summary>
        public bool UseRanges { get; private set; }

        /// <summary>Constructor for <see cref="UnitAttribute"/>.</summary>
        /// <param name="Unit">The unit e.g "Hz".</param>
        /// <param name="UseEngineeringPrefix">Whether to use engineering prefix. E.g 1000000 Hz -> 1 MHz.</param>
        /// <param name="StringFormat">The format argument to string.Format.</param>
        /// <param name="UseRanges">Whether to use ranges to show arrays of numbers. For example, show 1, 2, 3, 4 as 1 : 4.</param>
        /// <param name="PreScaling">Pre scaling of values.</param>
        public UnitAttribute(string Unit, bool UseEngineeringPrefix = false, string StringFormat = "", bool UseRanges = true, double PreScaling = 1.0)
        {
            this.Unit = Unit;
            this.UseEngineeringPrefix = UseEngineeringPrefix;
            this.PreScaling = PreScaling;
            this.StringFormat = StringFormat;
            this.UseRanges = UseRanges;
        }
    }
}
