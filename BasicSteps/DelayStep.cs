//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.ComponentModel;

namespace OpenTap.Plugins.BasicSteps
{
    [Display("Delay", Group:"Basic Steps", Description: "Delays for a specified amount of time.")]
    public class DelayStep : TestStep
    {
        [Display("Time Delay", Description: "The time to delay in the test step.")]
        [Unit("s")]
        public double DelaySecs {
            get { return delaySecs; }
            set
            {
                if (value >= 0.0)
                    delaySecs = value;
                else throw new ArgumentException("Time Delay must be greater than 0 seconds.");
            }
        }

        double delaySecs = 0.1; // Set to 100 ms just to show the syntax for a normal timespan with sub second precision in the property grid.
        
        public override void Run()
        {
            TapThread.Sleep(Time.FromSeconds(DelaySecs));
        }
    }
}

