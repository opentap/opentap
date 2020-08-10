//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;

namespace OpenTap.Plugins.BasicSteps
{
    [Display("Repeat", Group: "Flow Control", Description: "Repeats its child steps a fixed number of times or until the verdict of a child step changes to a specified state.")]
    [AllowAnyChild]
    public class RepeatStep : LoopTestStep
    {
        public enum RepeatStepAction
        {
            [Display("Fixed Count", "Repeat iteration a fixed number of times.")]
            Fixed_Count,
            [Display("While", "Repeat while the specified condition is met. This guarantees a minimum of one time.")]
            While,
            [Display("Until", "Repeat until the specified condition is met. This guarantees a minimum of one time.")]
            Until
        }

        #region Settings
        [Display("Repeat", Order: 0, Description: "Select if you want to repeat for a fixed number of times or you want to repeat while or until a certain step has a certain verdict.")]
        public RepeatStepAction Action { get; set; }

        [StepSelector(StepSelectorAttribute.FilterTypes.All)]
        [EnabledIf(nameof(Action), RepeatStepAction.While, RepeatStepAction.Until, HideIfDisabled = true)]
        [Display("Verdict Of", Order: 1, Description: "Select the step that you want to validate the verdict of.")]
        public ITestStep TargetStep { get; set; }
        
        [EnabledIf("Action", RepeatStepAction.While, RepeatStepAction.Until, HideIfDisabled = true)]
        [Display("Equals", Order: 2, Description: "Set the verdict you want to validate against.")]
        public Verdict TargetVerdict { get; set; }

        [EnabledIf("Action",RepeatStepAction.Fixed_Count, HideIfDisabled = true)]
        [Display("Count", Order: 1, Description: "Set the fixed number of times to repeat.")]
        public uint Count { get; set; }
        
        [Display("Retry", Order:3, Description: "Clear the verdict and try again if break conditions were reached for child steps.")]
        public bool Retry { get; set; }

        public Enabled<uint> maxCount = new Enabled<uint> { Value = 3, IsEnabled = false };

        [Display("Max Count", Order: 3, Description: "When enabled the children will only be repeated a maximum number of times.")]
        [EnabledIf("Action", RepeatStepAction.While, RepeatStepAction.Until, HideIfDisabled = true)]
        public Enabled<uint> MaxCount
        {
            get => maxCount;
            set => maxCount = value ?? throw new ArgumentNullException(nameof(value), "Cannot assign Max Count a null value.");
        }
        
        [Output]
        [Display("Iteration", "The current iteration.", Order: 4)]
        public string IterationInfo
        {
            get
            {
                
                uint _iteration = iteration;
                if(GetParent<TestPlan>().IsRunning == false)
                    _iteration = 0;

                if(MaxCount.IsEnabled && Action != RepeatStepAction.Fixed_Count)
                    return $"{_iteration} of {MaxCount.Value}";
                if(Action == RepeatStepAction.Fixed_Count)
                     return $"{_iteration} of {Count}";
                return $"{_iteration}";
            }
        }

        #endregion

        public RepeatStep()
        {
            TargetVerdict = Verdict.Fail;
            Action = RepeatStepAction.Fixed_Count;
            Count = 3;
            Rules.Add(() => TargetStep != null || Action == RepeatStepAction.Fixed_Count, "No step selected", "TargetStep");
        }
        uint iteration;

        Verdict getCurrentVerdict() => TargetStep?.Verdict ?? Verdict.NotSet;

        Verdict iterate()
        {
            TapThread.Current.AbortToken.ThrowIfCancellationRequested();
            if (Retry && Verdict == Verdict.Error) // previous break conditions were reached
                Verdict = Verdict.NotSet; 
            this.iteration += 1;
            OnPropertyChanged(nameof(IterationInfo));
            var AdditionalParams = new List<ResultParameter> { new ResultParameter("", "Iteration", this.iteration) };
            try
            {
                var runs = RunChildSteps(AdditionalParams, BreakLoopRequested);
                foreach (var r in runs)
                    r.WaitForCompletion();
            }
            catch(OperationCanceledException)
            {
                // break conditions reached for child steps.
                // if ClearVerdict is set, retry.
                
                if (Retry == false)
                    throw;
            }

            return getCurrentVerdict();
        }

        static bool IsChildOfOrSelf(ITestStep step, ITestStep parent)
        {
            if (step == parent) return true;
            step = step.Parent as ITestStep;
            while(step != null && step != parent)
                step = step.Parent as ITestStep;
            return step != null;
        }

        public override void Run()
        {
            base.Run();
            iteration = 0;
            
            if (Action != RepeatStepAction.Fixed_Count && TargetStep == null)
                throw new ArgumentException("Could not locate target test step");

            uint loopCount;
            if (Action == RepeatStepAction.Fixed_Count)
                loopCount = Count;
            else if (MaxCount.IsEnabled)
                loopCount = MaxCount.Value;
            else
                loopCount = uint.MaxValue;

            {   // Special case:
                // when TargetStep is not a child of this, we need to evaluate the verdict _before_ 
                // running the first iteration.

                if (Action != RepeatStepAction.Fixed_Count && !IsChildOfOrSelf(TargetStep, this))
                {
                    var currentVerdict = getCurrentVerdict();
                    if (currentVerdict != TargetVerdict && Action == RepeatStepAction.While)
                        return;
                    if (currentVerdict == TargetVerdict && Action == RepeatStepAction.Until)
                        return;
                }
            }
            

            switch (Action)
            {
                case RepeatStepAction.While:
                {
                    while (iterate() == TargetVerdict && iteration < loopCount && !BreakLoopRequested.IsCancellationRequested) { }

                    break;
                }
                case RepeatStepAction.Until:
                {
                    while (iterate() != TargetVerdict && iteration < loopCount && !BreakLoopRequested.IsCancellationRequested) { }

                    break;
                }
                case RepeatStepAction.Fixed_Count:
                {
                    while (iteration < loopCount && !BreakLoopRequested.IsCancellationRequested) 
                        iterate();
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
