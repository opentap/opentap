﻿//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

namespace OpenTap
{
    class ResetVerdictsStage : TestPlanExecutionStageBase
    {
        public StepOverrideStage StepsStage { get; set; }
        protected override void Execute(TestPlanExecutionContext context)
        {
            foreach (var step in StepsStage.allSteps)
            {
                if (step.Verdict != Verdict.NotSet)
                {
                    step.Verdict = Verdict.NotSet;
                    step.OnPropertyChanged("Verdict");
                }
            }
        }
    }
}
