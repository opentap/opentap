//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap
{

    /// <summary>
    /// Attribute used to specify how to display a <see cref="ComponentSettingsList"/> class in a GUI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ComponentSettingsLayoutAttribute : Attribute
    {
        /// <summary>
        /// Enum used to specify how to display a <see cref="ComponentSettingsList"/> class in a GUI.
        /// </summary>
        public enum DisplayMode
        {
            /// <summary>
            /// Show the list of objects as a master details view (list on the left, details for selected element in list on the right).
            /// </summary>
            MasterDetail,
            /// <summary>
            /// Show the list of objects as rows in a table.
            /// </summary>
            DataGrid
        }
        /// <summary>
        /// Specifies how to display the decorated class in a GUI.
        /// </summary>
        public DisplayMode Mode { get; private set; }
        /// <summary>
        /// Creates a new instance of this class.
        /// </summary>
        /// <param name="mode">Specifies how to display the decorated class in a GUI.</param>
        public ComponentSettingsLayoutAttribute(DisplayMode mode)
        {
            Mode = mode;
        }
    }
}
