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
    // Below shows how to access all TAP and custom Component Settings that are defined. 
    // On top of what is shown, the generic ComponentSettings<Type> can always be used.
    [Display("Accessing Component Settings", Groups: new[] { "Examples", "Plugin Development", "Step Execution" }, Order: 10000,
        Description: "Shows how to retrieve settings.")]
    public class SettingsRetrieval : TestStep
    {
        public override void Run()
        {
            // These settings always exist.
            Log.Info("Component Settings directory={0}", ComponentSettings.SettingsDirectoryRoot);
            Log.Info("Session log Path={0}", EngineSettings.Current.SessionLogPath);
            Log.Info("Result Listener Count={0}", ResultSettings.Current.Count);

            // DUT Setting can be used to find a specific DUT is the default is not desired.
            if (DutSettings.Current.Count > 0)
            {
                string s = DutSettings.GetDefaultOf<Dut>().Name;
                Log.Info("The first DUT found has a name of {0}", s);
            }

            // Similar to DutSettings, can be used to find Instruments other than the default.
            if (InstrumentSettings.Current.Count > 0)
            {
                string s = InstrumentSettings.GetDefaultOf<Instrument>().Name;
                Log.Info("The first instrument found has a name of {0}", s);
            }

             // An example of user defined settings, which show up as individual tabs.
             // Default values will be used, if none exist.
             // Defined in ExampleSettings.cs
              Log.Info("DifferentSettings as string={0}", ExampleSettings.Current.ToString());

             // An example of custom Bench settings.
             // This is similar to the DUT or Instrument editors.
             // Only use the values if something exists.
               if (CustomBenchSettingsList.Current.Count > 0)
               {
                Log.Info("Custom Bench Settings List Count={0}", CustomBenchSettingsList.Current.Count);
                Log.Info("First instance of Custom Bench setting as string={0}",
                    CustomBenchSettingsList.GetDefaultOf<CustomBenchSettings>());
                foreach (var customBenchSetting in CustomBenchSettingsList.Current)
                {
                    Log.Info("Type={0} Time={1} MyProperty={2}", customBenchSetting.GetType(), customBenchSetting.MyTime,
                        customBenchSetting.MyProperty);
                }
            }
        }
    }
}
