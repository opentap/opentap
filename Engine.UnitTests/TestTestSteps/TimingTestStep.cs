//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap.Engine.UnitTests.TestTestSteps
{
    public class TimingTestStep : TestStep
    {
        public double PreRunDelay { get; set; }
        public double RunDelay { get; set; }
        public double DeferDelay { get; set; }
        public double PostRunDelay { get; set; }

        public TimingTestStep()
        {
            PreRunDelay = 0.1;
            PostRunDelay = 0.1;
            RunDelay = 0.1;
            DeferDelay = 0.1;
        }

        public override void PrePlanRun()
        {
            base.PrePlanRun();
            TapThread.Sleep(TimeSpan.FromSeconds(PreRunDelay));
        }

        public override void Run()
        {
            TapThread.Sleep(TimeSpan.FromSeconds(RunDelay));
            Results.Defer(() => TapThread.Sleep(TimeSpan.FromSeconds(DeferDelay)));
        }

        public override void PostPlanRun()
        {
            base.PostPlanRun();
            TapThread.Sleep(TimeSpan.FromSeconds(PostRunDelay));
        }
    }
}
