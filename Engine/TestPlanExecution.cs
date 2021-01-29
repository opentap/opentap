//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OpenTap
{
    // This part of the TestPlan class holds the public API for running a testplan.
    partial class TestPlan
    {
        private TestPlanExecutor executor;


        /// <summary>
        /// When true, prints the test plan run summary at the end of a run.  
        /// </summary>
        [XmlIgnore]
        public bool PrintTestPlanRunSummary
        {
            get => executor.PrintTestPlanRunSummary;
            set => executor.PrintTestPlanRunSummary = value;
        }

        /// <summary> True if this TestPlan is currently running. </summary>
        public bool IsRunning => executor.CurrentRun != null;

        /// <summary>
        /// Blocking Execute TestPlan. Uses ResultListeners from ResultSettings.Current.
        /// </summary>
        /// <returns>Result of test plan run as a TestPlanRun object.</returns>
        public TestPlanRun Execute()
        {
            return Execute(ResultSettings.Current, null);
        }

        /// <summary> </summary>
        /// <returns></returns>
        public Task<TestPlanRun> ExecuteAsync()
        {
            return ExecuteAsync(ResultSettings.Current, null,null, TapThread.Current.AbortToken);
        }
        
        /// <summary>
        /// Execute the TestPlan as specified.
        /// </summary>
        /// <param name="resultListeners">ResultListeners for result outputs.</param>
        /// <param name="metaDataParameters">Optional metadata parameters.</param>
        /// <param name="stepsOverride">Sub-section of test plan to be executed. Note this might include child steps of disabled parent steps.</param>
        /// <param name="cancellationToken">Cancellation token to abort the testplan</param>
        /// <returns>TestPlanRun results, no StepResults.</returns>
        public Task<TestPlanRun> ExecuteAsync(IEnumerable<IResultListener> resultListeners, IEnumerable<ResultParameter> metaDataParameters, HashSet<ITestStep> stepsOverride, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<TestPlanRun>();
            TapThread.Start(() =>
            {
                try
                {
                    cancellationToken.Register(TapThread.Current.Abort);
                    var testPlanRun = Execute(resultListeners, metaDataParameters, stepsOverride);
                    tcs.SetResult(testPlanRun);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            }, "Plan Thread");
            return tcs.Task;
        }

        /// <summary>
        /// Execute the TestPlan as specified. Blocking.
        /// </summary>
        /// <param name="resultListeners">ResultListeners for result outputs.</param>
        /// <param name="metaDataParameters">Optional metadata parameters.</param>
        /// <param name="stepsOverride">Sub-section of test plan to be executed. Note this might include child steps of disabled parent steps.</param>
        /// <returns>TestPlanRun results, no StepResults.</returns>
        public TestPlanRun Execute(IEnumerable<IResultListener> resultListeners, IEnumerable<ResultParameter> metaDataParameters = null, HashSet<ITestStep> stepsOverride = null)
        {
            return executor.Execute( resultListeners, metaDataParameters, stepsOverride);
        }


        /// <summary>
        /// Execute the TestPlan as specified. Blocking.
        /// </summary>
        /// <param name="resultListeners">ResultListeners for result outputs.</param>
        /// <param name="metaDataParameters">Metadata parameters.</param>
        /// <returns>TestPlanRun results, no StepResults.</returns>
        public TestPlanRun Execute(IEnumerable<IResultListener> resultListeners, IEnumerable<ResultParameter> metaDataParameters)
        {
            return Execute(resultListeners, metaDataParameters, null);
        }

        /// <summary> true if the plan is in its open state. </summary>
        public bool IsOpen { get { return executor.State != null; } }
        
        /// <summary>
        /// Opens all resources referenced in this TestPlan (Instruments/DUTs/ResultListeners). 
        /// This can be called before <see cref="TestPlan.Execute()"/> to manually control the opening/closing of the resources.
        /// </summary>
        public void Open()
        {
            Open(ResultSettings.Current);
        }

        /// <summary>
        /// Opens all resources referenced in this TestPlan (Instruments/DUTs/ResultListeners). 
        /// This can be called before <see cref="TestPlan.Execute()"/> to manually control the opening/closing of the resources.
        /// </summary>
        public void Open(IEnumerable<IResultListener> listeners)
        {
            executor.Open(listeners);
        }
        
        /// <summary>
        /// Closes all resources referenced in this TestPlan (Instruments/DUTs/ResultListeners). 
        /// This should be called if <see cref="TestPlan.Open()"/> was called earlier to manually close the resources again.
        /// </summary>
        public void Close()
        {
            executor.Close();
        }
    }
}
