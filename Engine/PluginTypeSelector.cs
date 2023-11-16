//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap
{
    /// <summary>
    /// Indicates that a property can select an instance of a plugin type deriving from the type of the property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class PluginTypeSelectorAttribute : Attribute
    {
        /// <summary>
        /// Where to get the available objects. This may be null in this case any derived type will be used.
        /// </summary>
        public string ObjectSourceProperty { get; set; }
    }
}
