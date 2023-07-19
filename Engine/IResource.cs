//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.ComponentModel;
using System.Xml.Serialization;

namespace OpenTap
{
    /// <summary>
    /// Base interface for resources. Specializations include Dut, Instrument and ResultListener.
    /// </summary>
    public interface IResource : INotifyPropertyChanged
    {
        /// <summary>
        /// A short name to display in the user interface in areas with limited space.
        /// </summary>
        [Display("Name",Group: "Common", Order: -1)]
        string Name { get; set; }

        /// <summary>
        /// When overridden in a derived class, opens a connection to the resource represented by this class.
        /// </summary>
        void Open();

        /// <summary>
        /// When overridden in a derived class, closes the connection made to the resource represented by this class.
        /// </summary>
        void Close();

        /// <summary>
        /// Indicates whether this DUT is currently connected.
        /// This value should be set by Open() and Close().
        /// </summary>
        [XmlIgnore]
        [Browsable(false)]
        bool IsConnected { get; }
    }

    /// <summary>
    /// Resources that can be enabled and disabled. Currently this is only supported for ResultListeners.
    /// </summary>
    public interface IEnabledResource : IResource
    {
        /// <summary>
        /// Gets or sets if this resources is enabled.
        /// </summary>
        bool IsEnabled { get; set; }
    }
}
