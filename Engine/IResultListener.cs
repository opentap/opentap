//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap
{
    /// <summary>
    /// Interface forming the basis for all ResultListeners.
    /// </summary>
    [Display("Result Listener")]
    public interface IResultListener : IResource, ITapPlugin
    {
        /// <summary>
        /// Called just when test plan starts.
        /// </summary>
        /// <param name="planRun">Test plan run parameters.</param>
        void OnTestPlanRunStart(TestPlanRun planRun);

        /// <summary>
        /// Called when test plan finishes. At this point no more result will be sent 
        /// to the result listener from said test plan run.
        /// </summary>
        /// <param name="planRun">Test plan run parameters.</param>
        /// <param name="logStream">The log file from the test plan run as a stream.</param>
        void OnTestPlanRunCompleted(TestPlanRun planRun, System.IO.Stream logStream);

        /// <summary>
        /// Called just before a test step is started.
        /// </summary>
        /// <param name="stepRun">Step run parameters.</param>
        void OnTestStepRunStart(TestStepRun stepRun);

        /// <summary>
        /// Called when a test step run is completed.
        /// Result might still be propagated to the result listener after this event.
        /// </summary>
        /// <param name="stepRun">Step run parameters.</param>
        void OnTestStepRunCompleted(TestStepRun stepRun);

        /// <summary>
        /// When a result is received this method is called.
        /// </summary>
        /// <param name="stepRunID"> Step run parameters.</param>
        /// <param name="result">Result structure.</param>
        void OnResultPublished(Guid stepRunID, ResultTable result);
    }

    /// <summary>
    /// An enum containing the execution states of a step.
    /// </summary>
    public enum StepState
    {
        /// <summary>
        /// The step is not running.
        /// </summary>
        Idle,
        /// <summary>
        /// The step is executing its PrePlanRun code.
        /// </summary>
        PrePlanRun,
        /// <summary>
        /// The step is executing its Run code.
        /// </summary>
        Running,
        /// <summary>
        /// The step is executing deferred actions after Run.
        /// </summary>
        Deferred,
        /// <summary>
        /// The step is executing its PostPlanRun code.
        /// </summary>
        PostPlanRun
    }

    /// <summary>
    /// Interface to listen to when steps execute what.
    /// </summary>
    [Display("Execution Listener")]
    public interface IExecutionListener : IResultListener
    {
        /// <summary>
        /// Called whenever a step changes its execution state.
        /// </summary>
        /// <param name="stepId">The given step</param>
        /// <param name="stepRun">The given step run. For PrePlanRun and PostPlanRun this is null.</param>
        /// <param name="newState">The state that the teststep transitioned into.</param>
        /// <param name="changeTime">The precise timestamp of when the change happened.</param>
        void OnTestStepExecutionChanged(Guid stepId, TestStepRun stepRun, StepState newState, long changeTime);
    }
}
