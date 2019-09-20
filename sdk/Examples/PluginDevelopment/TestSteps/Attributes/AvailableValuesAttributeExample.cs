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
using System.ComponentModel;
using System.Xml.Serialization;
using OpenTap;

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Available Values Example", 
        Groups: new[] { "Examples", "Plugin Development", "Attributes" }, 
        Description: "TestStep that uses AvailableValues attribute for two different settings.")]
    public class AvailableValuesAttributeExample : TestStep
    {
        private List<string> _listOfAvailableValues;
        [Display("Available Values", "An editable list of values.")]
        public List<string> ListOfAvailableValues
        {
            get { return _listOfAvailableValues; }
            set
            {
                _listOfAvailableValues = value;
                OnPropertyChanged("ListOfAvailableValues");
            }
        }

        [Display("Selected Value", "Shows the use of available values attribute.")]
        [AvailableValues("ListOfAvailableValues")]
        public string SelectedValue { get; set; }       
        
        // XmlIgnore and Browsable(false) are used to be able to select from resources without opening all of them.
        [XmlIgnore]
        [Browsable(false)]
        public IEnumerable<Dut> Duts
        {
            get
            {
                foreach (var someDut in DutSettings.Current)
                {
                    var explicitDut = (Dut) someDut;
                    if (explicitDut != null) yield return explicitDut;
                }
            }
        }

        [AvailableValues("Duts")]
        [Display("Selected Dut", "Shows the use of available values attribute. In this case, they are read dynamically.")]
        public Dut SelectedDut { get; set; }

        public AvailableValuesAttributeExample()
        {
            _listOfAvailableValues = new List<string> {"One", "Two", "Three"};
            SelectedValue = string.Empty;
        }

        public override void Run()
        {
            // Do nothing
        }
    }
}
