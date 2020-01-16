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
using System.Xml.Serialization;

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Two Position Switch", Groups: new[] { "Examples", "Plugin Development", "Connections" }, Description: "A simulated single pole double throw switch.")]
    public class TwoPositionSwitchInstrument : Instrument
    {
        [XmlIgnore]
        public SwitchPosition Position1 { get; private set; }

        [XmlIgnore]
        public SwitchPosition Position2 { get; private set; }

        private SwitchPosition CurrentPosition { get; set; }

        public TwoPositionSwitchInstrument()
        {
            Name = "Switch";
            Position1 = new SwitchPosition(this, "PosA");
            Position2 = new SwitchPosition(this, "PosB");
            CurrentPosition = new SwitchPosition(this, "");
        }

        public void SetPosition(SwitchPosition newPosition)
        {
            CurrentPosition.IsActive = false;
            CurrentPosition = newPosition;
            newPosition.IsActive = true;
        }
    }
}
