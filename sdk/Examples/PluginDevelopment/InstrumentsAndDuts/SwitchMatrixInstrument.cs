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
using OpenTap;

// This example demonstrates how a SwitchMatrix instrument can be created with simulated functionality

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Switch Matrix", Groups: new[] { "Examples", "Plugin Development", "Connections" }, Description: "A simulated 4x4 switch matrix.")]
    public class SwitchMatrixInstrument : Instrument
    {
        private readonly SwitchMatrixPathCollection _Channels;
        private SwitchMatrixPath _ActiveChannel;
        #region Settings
        public SwitchMatrixPathCollection Channels { get { return _Channels; } }
        #endregion

        public SwitchMatrixInstrument()
        {
            // A custom Name for the switch can be specified here.
            // This can also be specified manually in the Connection Settings GUI.
            // In absence of this, "N/A" will be used.
            this.Name = "My_4x4_Matrix";

            _Channels = new SwitchMatrixPathCollection(this, 4, 4);
        }

        /// <summary>
        /// Open procedure for the instrument.
        /// </summary>
        public override void Open()
        {
            base.Open();
            // Open the connection to the instrument here.

            // Usually, initialization will also include a reset operation 
            // to ensure all matrix crosspoints are disconnected.
            resetSwitchConnections();
        }

        public void SetMatrixChannel(int row, int col)
        {
            if (_Channels[row, col] == _ActiveChannel)
                return;

            // Disable any active connections before enabling a new connection.
            resetSwitchConnection(_ActiveChannel);

            // Add hardware driver calls to enable the switch channel here.
            // E.g. Switch.Route.CloseChannel("m1r1c1");

            // Set the corresponding row and column as active.
            var channel = _Channels[row, col];
            channel.IsActive = true;
            _ActiveChannel = channel;
        }

        /// <summary>
        /// Close procedure for the instrument.
        /// </summary>
        public override void Close()
        {
            // Shut down the connection to the instrument here.
            resetSwitchConnection(_ActiveChannel);
            base.Close();
        }

        private void resetSwitchConnection(SwitchMatrixPath channel)
        {
            if (channel != null)
            {
                // Add hardware driver calls to disable the switch channel here.
                channel.IsActive = false;
            }
        }

        private void resetSwitchConnections()
        {
            // Add hardware driver calls to disable all switch channels here.
            _ActiveChannel = null;
        }
    }
}
