//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Collections.Specialized;
namespace OpenTap
{
    /// <summary>
    /// Settings governing configured instruments. These are usually configured by the user..
    /// </summary>
    [Display("Instruments", "Instrument Settings")]
    [SettingsGroup("Bench", Profile: true)]
    public class InstrumentSettings : ComponentSettingsList<InstrumentSettings, IInstrument>
    {
        /// <summary>
        /// When the instrument settings list is modified some ports being used by connections might disappear.
        ///  In that case we need to remove the ports from the connections.
        /// </summary>
        internal protected override void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(sender, e);

            var removedItems = e.OldItems;

            if (removedItems == null) return;

            var con = ConnectionSettings.CurrentFromCache;
            con?.UpdateConnectionPorts(removedItems);
        }
    }
}
