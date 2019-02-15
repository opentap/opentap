//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using OpenTap;

namespace OpenTap.Engine.UnitTests
{
    public class PingActivityTest : TestStep
    {
        public PingingResource SelectedResource { get; set; }
        public int Iterations { get; set; }
        public override void Run()
        {
            for (int i = 0; i < Iterations; i++)
            {
                SelectedResource.PingActivity();
            }
        }
    }
    public class PingingResource : Instrument
    {
        public void PingActivity()
        {
            OnActivity();
        }
    }
}
