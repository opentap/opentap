//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    class StepOverrideStage : TestPlanExecutionStageBase
    {
        public IList<ITestStep> steps { get; set; }
        public List<ITestStep> allSteps { get; private set; }
        public List<ITestStep> allEnabledSteps { get; private set; }

        protected override void Execute(TestPlanExecutionContext context)
        {
            if (context.stepsOverride == null)
                steps = context.Plan.Steps;
            else
            {
                // Remove steps that are already included via their parent steps.
                foreach (var step in context.stepsOverride)
                {
                    if (step == null)
                        throw new ArgumentException("stepsOverride may not contain null", "stepsOverride");

                    var p = step.GetParent<ITestStep>();
                    while (p != null)
                    {
                        if (context.stepsOverride.Contains(p))
                            throw new ArgumentException("stepsOverride may not contain steps and their parents.", "stepsOverride");
                        p = p.GetParent<ITestStep>();
                    }
                }
                steps = Utils.FlattenHeirarchy(context.Plan.Steps, step => step.ChildTestSteps).Where(context.stepsOverride.Contains).ToList();
            }

            allSteps = Utils.FlattenHeirarchy(steps, step => step.ChildTestSteps);
            allEnabledSteps = Utils.FlattenHeirarchy(steps.Where(x => x.Enabled), step => step.GetEnabledChildSteps());

        }
    }
}
