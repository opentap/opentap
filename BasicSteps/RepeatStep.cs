//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using OpenTap;

namespace OpenTap.Plugins.BasicSteps
{
    public abstract class LoopTestStep : TestStep
    {
        protected CancellationTokenSource breakLoopToken { get; private set; }

        [Browsable(false)]
        protected CancellationToken BreakLoopRequested { get { return breakLoopToken.Token; } }

        public LoopTestStep()
        {
            breakLoopToken = new CancellationTokenSource();
        }
        
        public void BreakLoop()
        {
            breakLoopToken.Cancel();
        }

        /// <summary> Always call base.Run in LoopTestStep inheritors. </summary>
        public override void Run()
        {
            breakLoopToken = new CancellationTokenSource();
        }
    }


    [Display("Repeat", Group: "Flow Control", Description: "Repeats its child steps a fixed number of times or until the verdict of a child step changes to a specified state.")]
    [AllowAnyChild]
    public class RepeatStep : LoopTestStep
    {
        public enum RepeatStepAction
        {
            [Display("Fixed Count")]
            Fixed_Count,
            While,
            Until
        }

        #region Settings
        [Display("Repeat", Order: 0, Description: "Select if you want to repeat for a fixed number of times or you want to repeat while or until a certain step has a certain verdict.")]
        public RepeatStepAction Action { get; set; }


        [StepSelector(StepSelectorAttribute.FilterTypes.AllExcludingSelf)]
        [EnabledIf("Action", RepeatStepAction.While, RepeatStepAction.Until, HideIfDisabled = true)]
        [Display("Verdict Of", Order: 1, Description: "Select the step that you want to validate the verdict of.")]
        public ITestStep TargetStep { get; set; }
        
        [EnabledIf("Action", RepeatStepAction.While, RepeatStepAction.Until, HideIfDisabled = true)]
        [Display("Equals", Order: 2, Description: "Set the verdict you want to validate against.")]
        public Verdict TargetVerdict { get; set; }

        [EnabledIf("Action",RepeatStepAction.Fixed_Count, HideIfDisabled = true)]
        [Display("Count", Order: 1, Description: "Set the fixed number of times to repeat.")]
        public uint Count { get; set; }

        public Enabled<uint> maxCount = new Enabled<uint> { Value = 3, IsEnabled = false };

        [Display("Max Count", Order: 3, Description: "When enabled the children will only be repeated a maximum number of times.")]
        [EnabledIf("Action", RepeatStepAction.While, RepeatStepAction.Until, HideIfDisabled = true)]
        public Enabled<uint> MaxCount
        {
            get { return maxCount; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value", "Cannot assign Max Count a null value.");
                maxCount = value;
            }
        }
        
        [Output]
        [Display("Iteration", "The current iteration.", Order: 4)]
        public string IterationInfo
        {
            get
            {
                if(MaxCount.IsEnabled && Action != RepeatStepAction.Fixed_Count)
                    return string.Format("{0} of {1}", iteration, MaxCount.Value);
                if(Action == RepeatStepAction.Fixed_Count)
                     return string.Format("{0} of {1}", iteration, Count);
                return string.Format("{0}", iteration);
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

        Verdict getCurrentVerdict()
        {
            if (TargetStep == null)
                return Verdict.NotSet;
            return TargetStep.Verdict;
        }

        Verdict iterate()
        {
            this.iteration += 1;
            OnPropertyChanged("IterationInfo");
            var AdditionalParams = new List<ResultParameter> { new ResultParameter("", "Iteration", this.iteration) };
            var runs = RunChildSteps(AdditionalParams, BreakLoopRequested);
            foreach (var r in runs)
                r.WaitForCompletion();
            return getCurrentVerdict();
        }

        static bool isChildOf(ITestStep step, ITestStep parent)
        {
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

                if (Action != RepeatStepAction.Fixed_Count && !isChildOf(TargetStep, this))
                {
                    var currentVerdict = getCurrentVerdict();
                    if (currentVerdict != TargetVerdict && Action == RepeatStepAction.While)
                        return;
                    if (currentVerdict == TargetVerdict && Action == RepeatStepAction.Until)
                        return;
                }
            }

            if (Action == RepeatStepAction.While)
                while (iterate() == TargetVerdict && iteration < loopCount && !BreakLoopRequested.IsCancellationRequested) { }
            else if (Action == RepeatStepAction.Until)
                while (iterate() != TargetVerdict && iteration < loopCount && !BreakLoopRequested.IsCancellationRequested) { }
            else while (iteration < loopCount && !BreakLoopRequested.IsCancellationRequested) iterate();
        }
    }
}
