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

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("EnabledIf Attribute Example", Groups: new[] { "Examples", "Plugin Development", "Attributes" }, 
        Description: "Example step that uses the EnabledIf attribute.")]
    public class EnabledIfAttributeExample : TestStep
    {
        // Radio Standard to set DUT to transmit.
        [Display("Radio Standard", Group: "DUT Setup", Order: 1)]
        public RadioStandard Standard { get; set; }

        // This setting is only enabled when Standard == LTE || Standard == WCDMA.
        [Display("Measurement Bandwidth", Group: "DUT Setup", Order: 2.1)]
        [EnabledIf("Standard", RadioStandard.Lte, RadioStandard.Wcdma)]
        public double Bandwidth { get; set; }

        // Only enabled and visible when the Standard is set to GSM.
        [Display("Override Bandwidth", Group: "Advanced DUT Setup", Order: 3.1)]
        [EnabledIf("Standard", RadioStandard.Gsm, HideIfDisabled = true)]
        public bool BandwidthOverride { get; set; }
        
        // In this case, the setting will be enabled when Standard=GSM and  BandwidthOverride=true.
        [Display("Actual Bandwidth", Group: "Advanced DUT Setup", Order: 3.2)]
        [EnabledIf("Standard", RadioStandard.Gsm, HideIfDisabled = true)]
        [EnabledIf("BandwidthOverride", true, HideIfDisabled = true)]
        public double ActualBandwidth { get; set; }

        public enum RadioStandard
        {
            Gsm,
            Wcdma,
            Lte
        }

        public override void Run()
        {
            // Do nothing
        }
    }
}
