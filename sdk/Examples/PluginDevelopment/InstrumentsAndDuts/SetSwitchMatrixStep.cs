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
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using OpenTap;

// This example shows how a SwitchMatrix channel can be set in a Step

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Set Switch Matrix", Groups: new[] { "Examples", "Plugin Development", "InstrumentsAndDuts", "Connections" }, Description: "A TestStep that operates on a simulated 4x4 switch matrix.")]
    public class SetSwitchMatrixStep : TestStep
    {
        #region Settings
        public SwitchMatrixInstrument SwitchMatrix { get; set; }

        [XmlIgnore]
        public IEnumerable<SwitchMatrixPath> AvailableChannels
        {
            get
            {
                return SwitchMatrix == null ? null : SwitchMatrix.Channels;
            }
        }

        [Display("Channel", Order: 0)]
        [AvailableValues("AvailableChannels")]
        public SwitchMatrixPath SelectedChannel { get; set; }
        
        #endregion

        public override void Run()
        {
            if (SelectedChannel != null)
            {
                Log.Info("Setting switch '{0}' channel to '{1}'.", SwitchMatrix.Name, SelectedChannel.Name);
                SwitchMatrix.SetMatrixChannel(SelectedChannel.Row, SelectedChannel.Column);
            }
            else
            {
                Log.Warning("Please select a valid channel.");
            }

        }
    }
}
