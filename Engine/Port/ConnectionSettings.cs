//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.ComponentModel;

namespace OpenTap
{
    /// <summary>
    /// Settings specifying how DUTs and Instruments are connected.
    /// </summary>
    [Display("Connections", "Connection Settings")]
    [ComponentSettingsLayoutAttribute(ComponentSettingsLayoutAttribute.DisplayMode.DataGrid)]
    [SettingsGroupAttribute("Bench", Profile: true)]
    public class ConnectionSettings : ComponentSettingsList<ConnectionSettings, Connection>
    {
    }
}
