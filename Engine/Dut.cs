//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.ComponentModel;

namespace OpenTap
{
    /// <summary>
    /// Base class for DUT drivers. 
    /// </summary>
    public abstract class Dut : Resource, IDut
    {
        private string _ID;

        /// <summary>
        /// Sets the Name of the DUT.
        /// </summary>
        public Dut()
        {
            Name = "DUT";
        }

        /// <summary>
        /// Identifier that uniquely identifies the DUT, such as its serial number. 
        /// </summary>
        [Display("ID", "ID identifying the DUT associated with results in the database.", "Common")]
        [MetaData(true, "DUT ID")]
        public string ID
        {
            get { return _ID; }
            set
            {
                if (value == _ID) return;
                _ID = value;
                OnPropertyChanged("ID");
            }
        }

        /// <summary>
        /// User-supplied comment about DUT. Entered in the Bench Settings > Instrument dialog in the OpenTAP GUI.
        /// </summary>
        [Display("Comment", "A comment related to the DUT associated with results in the database.", "Common", Order: 1)]
        [MetaData(true)]
        public string Comment { get; set; }
    }
}
