//Copyright 2012-2019 Keysight Technologies
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

// This example step gets connection details between two entities (which can be a combination of Instruments and DUTs).

// Steps to use this example:
// 1. Add a simulated Instrument and a DUT. In this case, TwoPortInstrument and FourPortDUT can be added.
// 2. Add a Connection in Settings >> Bench >> Connections using the Instruments/DUTs added in Step 1. It can contain a switch.
// 3. Optionally, set any of the connections as Active by adding a Switch Step (E.g. SetSwitchMatrixStep).

// Other Notes:
// It can be extended further to perform manipulation or measurements or operations using connections
// e.g. setting power on the DUT and measuring the same using an instrument.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenTap;

namespace OpenTap.Plugins.PluginDevelopment.InstrumentsAndDuts
{
    [Display("Connection Usage", Groups: new[] { "Examples", "Plugin Development", "InstrumentsAndDuts" }, Description: "Example step that operates on connections.")]
    public class ConnectionStep : TestStep
    {
        #region Settings
        public TwoPortInstrument SimInstrument { get; set; }
        public FourPortDut SimDut { get; set; }
        #endregion

        public override void Run()
        {
            // Find and display available connections between each of the instrument's ports and the DUT
            var ports = SimInstrument.GetConstProperties<Port>();

            foreach (var port in ports)
            {
                var connections = port.GetConnectionsTo(SimDut);

                if (connections.Count() > 0)
                {
                    foreach (var connection in connections)
                    {
                        string connectionName = String.Empty;

                        if(String.IsNullOrEmpty(connection.Name))
                        {
                            connectionName = "Not Specified";
                        }
                        
                        if (connection.Via.Count > 0) // Indirect connection
                        {
                            Log.Info("Name: {0}, Connection: {1} <-> {2} <-> {3}, IsActive: {4}",
                                connectionName, connection.Port1, FlattenViaPoints(connection.Via), connection.Port2, connection.IsActive);
                        }
                        else // Direct connection
                        {
                            Log.Info("Name: {0}, Connection: {1} <-> {2}", connectionName, connection.Port1, connection.Port2);
                        }
                    }
                }
                else
                {
                    Log.Info("{0} does not have a connection to {1}.", port.ToString(), SimDut.Name);
                }
            }
        }

        private string FlattenViaPoints(List<ViaPoint> viaPoints)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var viaPoint in viaPoints)
            {
                sb.Append(String.Format("{0}-", viaPoint));
            }
            
            return sb.ToString().TrimEnd('-' );
        }
    }
}
