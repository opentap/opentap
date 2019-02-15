//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.ComponentModel;
namespace OpenTap
{
    /// <summary>
    /// ComponentSettings that contains an expandable list of instruments.
    /// </summary>
    [Display("Instruments", "Instrument Settings")]
    [SettingsGroupAttribute("Bench", Profile: true)]
    public class InstrumentSettings : ComponentSettingsList<InstrumentSettings, IInstrument>
    {
    }
}
