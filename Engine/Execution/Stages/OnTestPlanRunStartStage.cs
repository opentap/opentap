//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;

namespace OpenTap
{
    class OnTestPlanRunStartStage : TestPlanExecutionStageBase
    {
        internal static readonly TraceSource Log = OpenTap.Log.CreateSource("Executor(TPRS)");

        public CreateRunStage CreateRun { get; set; }
        public MetadataPromptStage MetadataPromptStage { get; set; } // wait for prompts before executing

        public SerializePlanStage SerializePlanStage { get; set; } // Wait for the TestPlanXml property in the TestPlanRun to be filled before calling OnTestPlanRunStart

        protected override void Execute(TestPlanExecutionContext context)
        {
            TestPlanRun execStage = CreateRun.execStage;
            Exception resultListenerError = null;
            execStage.ScheduleInResultProcessingThread<IResultListener>(resultListener =>
            {
                try
                {
                    using (TimeoutOperation.Create(() => TestPlanExecutonHelpers.PrintWaitingMessage(new List<IResource>() { resultListener })))
                        execStage.ResourceManager.WaitUntilResourcesOpened(TapThread.Current.AbortToken, resultListener);
                    try
                    {
                        // some resources might set metadata in the Open methods.
                        // this information needs to be propagated to result listeners as well.
                        // this returns quickly if its a lazy resource manager.
                        using (TimeoutOperation.Create(
                            () => TestPlanExecutonHelpers.PrintWaitingMessage(new List<IResource>() { resultListener })))
                            execStage.ResourceManager.WaitUntilAllResourcesOpened(TapThread.Current.AbortToken);
                    }
                    catch // this error will also be handled somewhere else.
                    {

                    }

                    foreach (var res in execStage.PromptedResources)
                        execStage.Parameters.AddRange(ResultParameters.GetMetadataFromObject(res));
                    resultListener.OnTestPlanRunStart(execStage);
                }
                catch (OperationCanceledException) when (execStage.MainThread.AbortToken.IsCancellationRequested)
                {
                    // test plan thread was aborted, this is OK.
                }
                catch (Exception ex)
                {
                    Log.Error("Error in OnTestPlanRunStart for '{0}': '{1}'", resultListener, ex.Message);
                    Log.Debug(ex);
                    resultListenerError = ex;
                }

            }, true);

            if (resultListenerError != null)
            {
                execStage.FailedToStart = true;
                throw resultListenerError;
            }
        }
    }
}
