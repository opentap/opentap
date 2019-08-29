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
using System.ComponentModel;
using OpenTap;
using System.Linq;
using System.Xml.Serialization;

// This example shows an instrument, which implements the IInstrument interface. It shows a minimal implementation with
// IsConnected property, and OnActivity().

// Implementing IInstrument interface for an instrument is useful, when the instrument plugins needs to inherit from
// an other class to reuse code and hence can not inherit from Instrument/ScpiInstrument. 

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Interface based Instrument", Groups: new[] { "Examples", "Plugin Development" }, 
        Description: "An example of an instrument implementing the IInstrument interface.")]
    public class InterfaceBasedInstrument : IInstrument, INotifyPropertyChanged, INotifyActivity
    { 
        private static TraceSource Log = OpenTap.Log.CreateSource("InterfaceBasedInstrument");

        public InterfaceBasedInstrument()
        {
            Name = "IInst";
        }

        // A name displayed in the user interface where space is limited.
        string _name = "N/A";
        [Display("Name", Group: "Common", Order: -3)]
        [Browsable(false)]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    if (Log != null)
                    {
                        OpenTap.Log.RemoveSource(Log);
                    }
                    Log = OpenTap.Log.CreateSource(_name);
                    RaisePropertyChanged("Name");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<EventArgs> Activity;

        // Closes connection to instrument
        public void Close()
        {
            // Close resource

            IsConnected = false;
        }

        // Opens connection to instrument
        public void Open()
        {
            // Open resource

            IsConnected = true;
        }

        private bool isConnected = false;
        // Indicates whether this resource is currently connected. This value should be set by Open() and Close().
        [XmlIgnore]
        [Browsable(false)]
        public bool IsConnected
        {
            get { return isConnected; }
            set
            {
                if (isConnected != value)
                {
                    isConnected = value;
                    RaisePropertyChanged("IsConnected");
                }
            }
        }

        protected void RaisePropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }

        // Triggers the ActivityStateChanged event. This causes that instrument activity is visible in 
        // Resource Bar of TAP GUI. 
        public void OnActivity()
        {
            if (Activity != null)
            {
                Activity.Invoke(this, new EventArgs());
            }
        }

        // Override ToString() to give more meaningful names.
        public override string ToString()
        {
            return Name ?? "NULL";
        }
    }
}
