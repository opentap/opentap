//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap
{

    /// <summary>
    /// Event args passed to pre execution hooks by <see cref="IPreTestPlanExecutionHook"/>.
    /// </summary>
#if ARBITER_FEATURES
    public
#endif
    class PreExecutionHookArgs
    {
        /// <summary>
        /// Can be used by a hook to indicate that the component settings should be reloaded from disk.
        /// </summary>
        public bool IsSettingsInvalid { get; set; }
        /// <summary>
        /// Can be used by a hook to change which testplan should be executed.
        /// </summary>
        public TestPlan TestPlan { get; set; }
    }

    /// <summary>
    /// Anyone implementing this interface will get a hook into the execution of testplan, but before the execution logic actually starts.
    /// This allows changing settings, and the testplan to be executed.
    /// </summary>
#if ARBITER_FEATURES
    public
#endif
    interface IPreTestPlanExecutionHook : ITapPlugin
    {
        /// <summary>
        /// The hook that will be called before a testplan is executed.
        /// </summary>
        /// <param name="hook"></param>
        /// <remarks>Throwing an exception from this method will cause the testplan not to be executed.</remarks>
        void BeforeTestPlanExecute(PreExecutionHookArgs hook);
    }

    /// <summary>
    /// This interface provides a way for plugins to listen to any testplans being executed.
    /// </summary>
#if ARBITER_FEATURES
    public
#endif
    interface ITestPlanExecutionHook : ITapPlugin
    {
        /// <summary>
        /// This will be called when a testplan actually starts executing.
        /// The testplan passed here will not have pre execution hooks called for it.
        /// </summary>
        /// <param name="executingPlan">The plan that will be executed.</param>
        /// <remarks>Throwing an exception from this method will cause the testplan not to be executed.</remarks>
        void BeforeTestPlanExecute(TestPlan executingPlan);

        /// <summary>
        /// This method is called after a testplan has executed, even if it failed to start.
        /// It has two testplan arguments as a pre execution hook might ask the engine to execute a different testplan instance, or a reloaded version of the same testplan.
        /// </summary>
        /// <param name="executedPlan">The testplan that was executed.</param>
        /// <param name="requestedPlan">The testplan that was initially asked to execute.</param>
        void AfterTestPlanExecute(TestPlan executedPlan, TestPlan requestedPlan);

        /// <summary>
        /// This is a hook that will be triggered before a teststep is going to be executed.
        /// </summary>
        /// <param name="testStep">The teststep that will be executed.</param>
        void BeforeTestStepExecute(ITestStep testStep);
        /// <summary>
        /// This is a hook that will be triggered after a teststep has been executed whether it failed or not.
        /// </summary>
        /// <param name="testStep">The teststep that was executed.</param>
        void AfterTestStepExecute(ITestStep testStep);
    }
}
