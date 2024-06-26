//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Collections.Generic;

namespace OpenTap
{
    /// <summary>
    /// A directional <see cref="Connection"/> that has an RF cable loss parameter.
    /// </summary>
    [Display("Directional RF Connection", "Directional RF connection modeled as a set of cable loss points with frequency and loss values and a direction.", "Basic Connections")]
    public class DirectionalRfConnection : RfConnection
    {
        /// <summary>
        /// Specifies the direction of a connection.
        /// </summary>
        public enum PortDirectionEnum
        {
            /// <summary>
            /// Directional RF connection from Port1 to Port2.
            /// </summary>
            [Display("From 1 to 2", "The signal in this connection flows from port 1 to port 2.")]
            FROM_1_TO_2,
            /// <summary>
            /// Directional RF connection from Port2 to Port1.
            /// </summary>
            [Display("From 2 to 1", "The signal in this connection flows from port 2 to port 1.")]
            FROM_2_TO_1
        };

        /// <summary>
        /// Direction of RF connection.
        /// </summary>
        [Display("Port Direction", Order: -1)]
        public PortDirectionEnum PortDirection { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectionalRfConnection"/> class.
        /// </summary>
        public DirectionalRfConnection()
        {
            Name = "DRF";
            CableLoss = new List<RfConnection.CableLossPoint>();

            Rules.Add(() => (PortDirection == PortDirectionEnum.FROM_1_TO_2) ? ((Port1 == null) || !(Port1 is InputPort)) : true, () => "Direction indicates an output port, but an input port is connected.", "Port1");
            Rules.Add(() => (PortDirection == PortDirectionEnum.FROM_2_TO_1) ? ((Port1 == null) || !(Port1 is OutputPort)) : true, () => "Direction indicates an input port, but an output port is connected.", "Port1");
            Rules.Add(() => (PortDirection == PortDirectionEnum.FROM_1_TO_2) ? ((Port2 == null) || !(Port2 is OutputPort)) : true, () => "Direction indicates an input port, but an output port is connected.", "Port2");
            Rules.Add(() => (PortDirection == PortDirectionEnum.FROM_2_TO_1) ? ((Port2 == null) || !(Port2 is InputPort)) : true, () => "Direction indicates an output port, but an input port is connected.", "Port2");
        }
    }

}
