//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap.Engine.UnitTests.TestTestSteps
{
    public class ThrowInDefer : TestStep
    {
        public bool Throw { get; set; }
        public int WaitMs { get; set; } = 500;
        public ThrowInDefer()
        {

        }

        public override void Run()
        {
            Results.Defer(() =>
            {
                TapThread.Sleep(WaitMs);
                if (Throw)
                    throw new InvalidOperationException("Intentional");
                else
                    UpgradeVerdict(Verdict.Error);

            });
        }
    }
}
