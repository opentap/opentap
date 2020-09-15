//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap.Plugins.BasicSteps
{
    [Display("Delay", Group: "Basic Steps",
        Description: "Waits a user defined amount of time before continuing.")]
    public class DelayStep : TestStep
    {
        [Display("Time Delay", Description: "The amount of time to wait before continuing."), Unit("s")]
        public double DelaySecs {
            get => delaySecs;
            set
            {
                if (value < 0.0)
                    throw new ArgumentException("Delay must be a positive value.");
                delaySecs = value;
            }
        }

        double delaySecs = 0.1; // Set to 100 ms just to show the syntax for a normal timespan with sub second precision in the property grid.
        
        public override void Run()
        {
            TapThread.Sleep(Time.FromSeconds(DelaySecs));
        }
    }
}

