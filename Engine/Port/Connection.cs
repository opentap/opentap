//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    /// <summary>
    /// Object representing a connection between two <see cref="Port"/>s. Can be extended to add properties.
    /// </summary>
    [Display("Generic Connection")]
    public abstract class Connection : ValidatingObject, ITapPlugin
    {
        /// <summary>
        /// A name for the connection to be displayed in the user interface.
        /// </summary>
        [Display("Name", Order: 0 - 100000)]
        [Layout(MinWidth=10)]
        public string Name { get; set; }

        /// <summary>
        /// The port at the first end of the connection.
        /// </summary>
        [Display("Port 1", Order: 1 - 100000)]
        [Layout(MinWidth=13)] // "Port A on DUT" : 13 characters. 
        public Port Port1 { get; set; }

        /// <summary> returns true if the port is used by this connection. </summary>
        internal bool HasPort(Port port) => Equals(Port1, port) || Equals(Port2, port);

        /// <summary>
        /// The port at the second end of the connection.
        /// </summary>
        [Display("Port 2", Order: 3 - 100000)]
        [Layout(MinWidth=13)]
        public Port Port2 { get; set; }
        
        /// <summary>
        /// Gets the list of <see cref="ViaPoint"/>s that this connection goes through. 
        /// </summary>
        [Layout(MinWidth=13)]
        [Display("Via", Order: 2 - 100000)]
        public List<ViaPoint> Via { get; set; }

        /// <summary>
        /// Returns true when a connection going through one or more switches (set using the <see cref="Via"/> property) is "Active" (all switches are in the correct position). 
        /// </summary>
        public virtual bool IsActive => Via.All(pos => pos.IsActive);

        /// <summary>
        /// Initializes a new instance of the <see cref="Connection"/> class.
        /// </summary>
        public Connection()
        {
            Name = "Conn";
            Via = new List<ViaPoint>();
        }

        /// <summary>
        /// Returns the other port when given either <see cref="Port1"/> or <see cref="Port2"/>.
        /// </summary>
        public Port GetOtherPort(Port p)
        {
            var port1 = Port1;
            var port2 = Port2;
            if (Equals(p, port1))
                return port2;
            if (Equals(p, port2))
                return port1;
            
            throw new ArgumentException("Argument must be either Port1 or Port2");
        }

        /// <summary>
        /// Returns a string representation of this connection which names the ports in each end.
        /// </summary>
        public override string ToString()
        {
            if (String.IsNullOrWhiteSpace(Name))
                return $"{(Port1?.ToString() ?? "N/A")} <-> { (Port2?.ToString() ?? "N/A")}";
            
            return Name;
        }

        public void Activate()
        {
            (Port1 as IActivatedPort)?.Activate();
            foreach (var pt in Via)
            {
                if(pt is IActivatedViaPoint activatedVia)
                    activatedVia.Activate();
            }
            (Port2 as IActivatedPort)?.Activate();
        }
    }
}
