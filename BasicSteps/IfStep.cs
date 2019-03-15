//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using OpenTap;  // Use Platform infrastructure/core components (log,TestStep definition, etc)

namespace OpenTap.Plugins.BasicSteps
{
    [Display("If Verdict", Group: "Flow Control", Description: "Runs its child steps only when the verdict of another step has a specific value.")]
    [AllowAnyChild]
    public class IfStep : TestStep
    {
        public enum IfStepAction
        {
            [Display("Run Children")]
            RunChildren,
            [Display("Break Loop")]
            BreakLoop,
            [Display("Abort Test Plan")]
            AbortTestPlan,
            [Display("Wait For User")]
            WaitForUser
        }

        #region Settings
        [Display("If", Order: 1)]
        public Input<Verdict> InputVerdict { get; set; }
        [Display("Equals", Order: 2)]
        public Verdict TargetVerdict { get; set; }
        [Display("Then", Order: 3)]
        public IfStepAction Action { get; set; }
        #endregion

        public IfStep()
        {
            InputVerdict = new Input<Verdict>();
            Rules.Add(() => InputVerdict.Step != null, "Input property must be set.", nameof(InputVerdict));
        }

        
        class Request
        {
            public string Name => "Waiting for user input";
            [Browsable(true)]
            [Layout(LayoutMode.FullRow)]
            public string Message { get; private set; } = "Continue?";
            [Submit]
            [Layout(LayoutMode.FloatBottom | LayoutMode.FullRow)]
            
            public WaitForInputResult1 Response { get; set; }
        }

        public override void Run()
        {
            // Get the targetStep
            if (InputVerdict == null)
                throw new ArgumentException("Could not locate target test step");
            
            if (InputVerdict.Value == TargetVerdict)
            {
                switch (Action)
                {
                    case IfStepAction.RunChildren:
                        Log.Info("Condition is true, running childSteps");
                        RunChildSteps();
                        break;
                    case IfStepAction.AbortTestPlan:
                        Log.Info("Condition is true, aborting TestPlan run.");
                        string msg = String.Format("TestPlan aborted by \"If\" Step ({2} of {0} was {1})", InputVerdict.Step.Name, InputVerdict.Value, InputVerdict.PropertyName);
                        PlanRun.MainThread.Abort();
                        break;
                    case IfStepAction.WaitForUser:
                        Log.Info("Condition is true, waiting for user input.");
                        var req = new Request();
                        UserInput.Request(req, false);
                        if (req.Response == WaitForInputResult1.No)
                        {
                            PlanRun.MainThread.Abort();
                        }
                        break;
                    case IfStepAction.BreakLoop:
                        Log.Info("Condition is true, breaking loop.");
                        var loopStep = GetParent<LoopTestStep>();
                        if(loopStep != null)
                        {
                            loopStep.BreakLoop();
                        }
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            else
            {
                Log.Info("Condition is false.");
            }
        }

    }
}
