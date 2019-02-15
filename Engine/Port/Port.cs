//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap
{
    /// <summary>
    /// Object representing a port on an instrument or DUT. Ports are connected with <see cref="Connection"/> objects.
    /// </summary>
    public class Port : IConstResourceProperty
    {
        /// <summary>
        /// The device (usually an <see cref="Instrument"/> or <see cref="Dut"/>) on which this port exists.
        /// </summary>
        [XmlIgnore] // avoid potential cycle in XML.
        public IResource Device { get; private set; }

        /// <summary>
        /// List of <see cref="Connection"/>s connected to this port.
        /// </summary>
        public IEnumerable<Connection> Connections
        {
            get
            {
                return ConnectionSettings.Current.Where(con => (con.Port1 != null && con.Port1.Equals(this)) || (con.Port2 != null && con.Port2.Equals(this)));
            }
        }

        /// <summary>
        /// The name of this port. (Should be unique among <see cref="Port"/> objects on the same device/resource).
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Port"/> class.
        /// </summary>
        public Port(IResource device, string name)
        {
            Device = device;
            Name = name;
        }

        /// <summary>
        /// Returns a list of the ports that this port has connections to.
        /// </summary>
        public IEnumerable<Port> GetConnectedPorts()
        {
            foreach (var connection in ConnectionSettings.Current)
            {
                if (connection.Port1 == this && connection.Port2 != null)
                {
                    yield return connection.Port2;
                }
                else if (connection.Port2 == this && connection.Port1 != null)
                {
                    yield return connection.Port1;
                }
            }
        }

        /// <summary>
        /// Returns a list of the ports that this port has connections to and that are active.
        /// </summary>
        public IEnumerable<Port> GetActiveConnectedPorts()
        {
            foreach (var connection in ConnectionSettings.Current)
            {
                if (!connection.IsActive)
                    continue;
                if (connection.Port1 == this && connection.Port2 != null)
                {
                    yield return connection.Port2;
                }
                else if (connection.Port2 == this && connection.Port1 != null)
                {
                    yield return connection.Port1;
                }
            }
        }

        /// <summary>
        /// Returns a list of connections from this port to a specified device.
        /// </summary>
        public IEnumerable<Connection> GetConnectionsTo(IResource device)
        {
            foreach (var connection in ConnectionSettings.Current)
            {
                if (connection.Port1 == this && connection.Port2 != null && connection.Port2.Device == device)
                {
                    yield return connection;
                }
                else if (connection.Port2 == this && connection.Port1 != null && connection.Port1.Device == device)
                {
                    yield return connection;
                }
            }

            foreach (var connection in ConnectionSettings.Current)
            {
                if (connection.Port1 == this || connection.Port2 == this)
                {
                    if (connection.Via.Any(vp => vp.Device == device))
                        yield return connection;
                }
            }
        }

        /// <summary>
        /// Returns a list of connections from this port to a specified port.
        /// </summary>
        public IEnumerable<Connection> GetConnectionsTo(Port otherPort)
        {
            foreach (var connection in ConnectionSettings.Current)
            {
                if (connection.Port1 == otherPort && connection.Port2 == this)
                {
                    yield return connection;
                }
                else if (connection.Port2 == otherPort && connection.Port1 == this)
                {
                    yield return connection;
                }
            }
        }

        /// <summary>
        /// Returns a string describing this port.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("{0} on {1}", Name, Device != null ? Device.Name : "<NULL>");
        }
    }

    /// <summary>
    /// An unidirectional port that is always an input.
    /// </summary>
    public class InputPort : Port
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InputPort"/> class.
        /// </summary>
        public InputPort(IResource device, string name) : base(device, name)
        {
        }
    }

    /// <summary>
    /// An unidirectional port that is always an output.
    /// </summary>
    public class OutputPort : Port
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OutputPort"/> class.
        /// </summary>
        public OutputPort(IResource device, string name) : base(device, name)
        {
        }
    }
}
