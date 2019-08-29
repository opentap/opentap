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
using OpenTap;

namespace OpenTap.Plugins.PluginDevelopment.TestSteps.Step_Settings
{
    // This example shows how to display a list of objects by implementing a custom class.
    // The editor (used for a list of objects) supports copy and paste of rows/columns.
    [Display("List of Objects Example", Groups: new[] { "Examples", "Plugin Development", "Step Settings" },
    Description: "An example of property editor for list of objects.")]
    public class ListOfObjects : TestStep
    {
        public class Screw
        {
            [Display("Number")]
            public int No { get; set; }
            [Display("Alias Name")]
            public string Name { get; set; }
        }

        [Display("Screw Settings")]
        public List<Screw> Screws { get; set; }

        public ListOfObjects()
        {
            Screws = new List<Screw>
            {
                new Screw{ No = 101, Name = "TX1"},
                new Screw{ No = 102, Name = "RX1"}
            };
        }

        public override void Run()
        {
            // Do nothing
        }
    }
}
