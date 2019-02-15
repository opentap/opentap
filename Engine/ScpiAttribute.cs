//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
namespace OpenTap
{
    /// <summary>
    /// Identifies that a property should be converted to a SCPI string.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Enum, AllowMultiple = true)]
    public class ScpiAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the SCPI string.
        /// </summary>
        public string ScpiString { get; private set; }

        /// <summary>
        /// Creates a <see cref="ScpiAttribute"/>
        /// </summary>
        public ScpiAttribute(string scpiString)
        {
            ScpiString = scpiString;
        }
    }
}
