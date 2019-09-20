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
using OpenTap;

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Pause with events", Groups: new[] { "Examples", "Plugin Development", "Step Settings" } , 
        Description: "A test step that calls OfferBreak to offer pause/break to GUI.")]
  
    [AllowAnyChild]
    public class WithEvents : TestStep
    {
        public int Count { get; set; }

        public double DelaySecs { get; set; }

        public WithEvents()
        {
            Count = 3;
            DelaySecs = 1;
        }

        public override void Run()
        {
            for (var i = 0; i < Count; i++)
            {
                // Do work.
                TapThread.Sleep(TimeSpan.FromSeconds(DelaySecs));

                // Offer GUI to break at this point.
                OfferBreak();
            }

            // GUI can automatically break when running its child steps.
            RunChildSteps();
        }
    }
}
