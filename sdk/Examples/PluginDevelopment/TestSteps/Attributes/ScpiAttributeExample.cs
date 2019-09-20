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

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("SCPI Attribute Example", Groups: new[] { "Examples", "Plugin Development", "Attributes" }, 
        Description: "Shows how the SCPI attribute can be used to easily integrated SCPI behavior into a code controlling a SCPI instrument.")]
    public class ScpiAttributeExample : TestStep
    {
        // Here the SCPI attribute is used to define an alias to a SCPI command.
        public enum TriggerType
        {
            [Scpi("IMM")] // The SCPI command.
            Immediate,    // An alias shown in GUI.
            [Scpi("INT")] Internal,
            [Scpi("KEY")] Key,
            [Scpi("BUS")] Bus,
            [Scpi("EXTernal2")] External2
        }
        
        public enum LinkType
        {   
            [Scpi("UP")]
            Uplink,

            // Only a single SCPI attribute may be used, but it may include multiple SCPI commands.
            [Scpi("DOWN; SomeOtherScpi; EvenMoreScpi")]
            Downlink,
        }
        
        // The SCPI attribute here allows the conversion of SCPI ON|OFF to C# true|false by the SCPI class.
        [Scpi("DISPlay:STATe")]
        public bool DisplayState { get; set; }
        
        // Examples that use the SCPI attribute as defined in the enumeration above.
        [Display("Link Direction", Order: 1)]
        public LinkType LinkDirection { get; set; }

        [Display("Trigger", Order: 2)]
        public TriggerType Trigger { get; set; }

        public ScpiAttributeExample()
        {
            LinkDirection = LinkType.Downlink;
            Trigger = TriggerType.Immediate;
        }

        public override void Run()
        {
            // The SCPI class's format command takes the value of the enum (marked with the SCPI attribute), 
            // and returns the associated SCPI command.
            Log.Info("The {0} property is set to {1}. Corresponding SCPI: {2}",
                        "LinkDirection", LinkDirection, Scpi.Format("{0}", LinkDirection));
            
            Log.Info("The {0} property is set to {1}. Corresponding SCPI: {2}",
                        "Trigger", Trigger, Scpi.Format("{0}", Trigger));

            // Like a C# string format, the SCPI class's format can take multiple parameters.
            string formattedScpi = Scpi.Format("Cmd1={0} Cmd2={1}", Trigger, LinkDirection);
            Log.Info("The combined SCPI commands are {0}", formattedScpi);

            // The SCPI class's Format command converts a boolean into the appropriate ON|Off values.
            // This could then be sent to an instrument.
            formattedScpi = Scpi.Format("DISPlay:STATe {0}", DisplayState);
            Log.Info("The formatted Display State SCPI is {0}", formattedScpi);
            
            // The SCPI parse command takes an "ON" return value and converts it into a boolean.
            string scpiOnOff = "ON";
            bool csBoolean = Scpi.Parse<bool>(scpiOnOff);
            Log.Info("The SCPI {0} is converted to a C# boolean of {1}", scpiOnOff, csBoolean);
            
            // The SCPI Parse command takes a enumeration value and SCPI command, and returns the human readable string for the enumeration.
            string myScpi = "IMM";  // Case sensitive
            var myAlias = Scpi.Parse<TriggerType>(myScpi);
            Log.Info("The SCPI is {0} and the property value is {1}", myScpi, myAlias);
            
            // The SCPI parse command takes a SCPI array, and convert it into a C# array.
            // This simulates reading some information from a device, and loading it into C# for further processing.
            string commaSeparatedValues = "1.1,2.2,3.3,4,5";
            double [] myArray = Scpi.Parse<double[]>(commaSeparatedValues);
            string myArrayAsString = string.Join(",", myArray);
            Log.Info("The SCPI arrays is {0} and the property value is {1}", commaSeparatedValues, myArrayAsString);
        }
    }
}
