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
using System.Xml.Serialization;
using OpenTap;

// This example shows how a Two Position Switch can be set in a Step

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Set Switch", Groups: new[] { "Examples", "Plugin Development", "InstrumentsAndDuts", "Connections" }, Description: "A TestStep that operates on a simulated two position switch.")]
    public class SetTwoPositionSwitchStep : TestStep
    {
        public TwoPositionSwitchInstrument Switch { get; set; }

        [XmlIgnore]
        public IEnumerable<SwitchPosition> AvailablePositions
        {
            get { return (Switch == null ? null : Switch.GetConstProperties<SwitchPosition>()); }
        }

        [AvailableValues("AvailablePositions")]
        public SwitchPosition Position { get; set; }

        public override void Run()
        {
            Log.Info("Setting switch '{0}' to '{1}'.", Switch.Name, Position.Name);
            Switch.SetPosition(Position);
        }
    }
}
