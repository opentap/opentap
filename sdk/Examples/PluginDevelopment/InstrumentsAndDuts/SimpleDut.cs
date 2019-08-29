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
    // An example of a DUT.
    [Display("Simple DUT", Groups: new[] { "Examples", "Plugin Development" }, Description: "An example of a simple DUT.")]
    public class SimpleDut : Dut
    {
        // If the "Allow Meta Data Dialog" is enabled in the Engine settings, 
        // the user will be prompted for this metadata on test plan run. 
        // The metadata will be available to the ResultListeners via the Testplan.Parameters collection.
        // Settings marked as meta data can also be used in macro expansion.
        // The Dut base class already has two properties (Comment and Id) that are marked with the MetaData attribute.
        [MetaData(true)] 
        [Display("My Meta Data", Description: "Simple DUT Meta Data.")]
        public string MyMetaData { get; set; }

        public SimpleDut()
        {
            Name = "SimpleDut";
            MyMetaData = "SomeMetaData";
        }
        
        // From DUT base class.
        public override void Open()
        {
            base.Open(); // Sets IsConnected = true;
            Log.Info("Opening SimpleDut.");
        }

        // From Dut base class.
        public override void Close()
        {
            Log.Info("Closing SimpleDut.");
            base.Close(); // Sets IsConnected = false;
        }

        // Unique to this class.
        public void DoNothing()
        {
            OnActivity();   // Causes the GUI to indicate progress
            Log.Info("SimpleDut called.");
        }
    }
}
