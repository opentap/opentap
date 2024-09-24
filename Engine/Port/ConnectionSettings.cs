//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections;
namespace OpenTap
{
    /// <summary>
    /// Settings specifying how DUTs and Instruments are connected.
    /// </summary>
    [Display("Connections", "Connection Settings")]
    [ComponentSettingsLayout(ComponentSettingsLayoutAttribute.DisplayMode.DataGrid)]
    [SettingsGroup("Bench", Profile: true)]
    public class ConnectionSettings : ComponentSettingsList<ConnectionSettings, Connection>
    {
        /// <summary>
        /// Removes a port whose device has been deleted from a settings list.
        /// </summary>
        internal void UpdateConnectionPorts(IList oldItems)
        {
            foreach (var connection in this)
            {
                if (connection.Port1?.Device is IResource res && oldItems.Contains(res))
                    connection.Port1 = null;
                if (connection.Port2?.Device is IResource res2 && oldItems.Contains(res2))
                    connection.Port2 = null;
            }
        }
    }
}
