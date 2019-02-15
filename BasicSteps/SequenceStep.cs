//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.ComponentModel;
using OpenTap;

namespace OpenTap.Plugins.BasicSteps
{
    [Display("Sequence", Group: "Flow Control", Description: "Runs its child steps sequentially. Verdict reflect the verdict of the child steps.")]
    [AllowAnyChild]
    public class SequenceStep : TestStep
    {
        public override void Run()
        {
            RunChildSteps();
        }
    }
}
