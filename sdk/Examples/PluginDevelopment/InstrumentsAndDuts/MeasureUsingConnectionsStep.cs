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
using System;
using System.Linq;
using OpenTap;
using System.Xml.Serialization;
using System.Collections.Generic;

// This example covers the following:
// - Usage of user defined Instrument and DUT objects in a Step.
// - Accessing and manipulating connection information in a Step.
// - Retrieval of Cable Loss information from a connection.
// - Interpolation of Cable Loss values.

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Measurement using Connection", Groups: new[] { "Examples", "Plugin Development", "InstrumentsAndDuts", "Connections" }, 
        Description: "An example that shows how a TestStep can use a connection to perform a simulated measurement.")]
    public class MeasureUsingConnectionsStep : TestStep
    {
        #region Settings
        [Display("Instrument", Group: "Hardware")]
        public TwoPortInstrument Generator { get; set; }

        [Display("DUT", Group: "Hardware")]
        public FourPortDut DUT { get; set; }

        [Display("Connection", Group: "Connection", Order: -10)]
        [AvailableValues("AvailableConnections")]
        public Connection SelectedConnection { get; set; }
        [XmlIgnore]
        public List<Connection> AvailableConnections
        {
            get
            {
                if (Generator == null || DUT == null)
                    return null;
                else // Get the available connections between the Generator and the DUT.
                    return Generator.PortA.GetConnectionsTo(DUT).ToList();
            }
        }

        [Display("Power", Group: "Settings", Order: 10)]
        [Unit("dBm")]
        public double Power { get; set; }

        [Display("Frequency", Group: "Settings", Order: 10)]
        [Unit("Hz", true)]
        public double Frequency { get; set; }

        #endregion

        public MeasureUsingConnectionsStep()
        {
            Power = -50;
            Frequency = 1e9;
        }

        public override void Run()
        {
            // Verify the currently active connection between the Generator and the DUT.
            if (SelectedConnection.IsActive == false)
            {
                throw new Exception(
                    String.Format(
                        "Connection {0} is not active. Please use SetSwitchMatrix / SetSwitch step to activate a connection.",
                        SelectedConnection));
            }

            // Display the user specified connection Name in the log.
            // This property can be set/changed either in the Connections GUI or in code.
            Log.Info(String.Format("Active Connection '[{0}] {1}'.",
                String.IsNullOrEmpty(SelectedConnection.Name) ? "N/A" : SelectedConnection.Name,
                SelectedConnection));

            var rfConnection = SelectedConnection as RfConnection;
            if (rfConnection != null)
            {
                // Retrieve loss and interpolate it for the current frequency value.
                var loss = rfConnection.GetInterpolatedCableLoss(Frequency);
                Generator.SetupInstrument(Power + loss);
            }

            // Perform simulated operations on the DUT
            DUT.SetupDut(Frequency);
            DUT.PerformMeasurement();

            // If no verdict is used, the verdict will default to NotSet.
            // You can change the verdict using UpgradeVerdict() as shown below.
            UpgradeVerdict(Verdict.Pass);
        }
    }
}
