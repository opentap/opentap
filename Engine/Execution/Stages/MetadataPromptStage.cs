//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System.Linq;
using System.Threading;

namespace OpenTap
{
    class MetadataPromptStage : TestPlanExecutionStageBase
    {
        public CreateRunStage CreateRunStage { get; set; }

        public OpenResourcesStage OpenResourcesStage { get; set; } // Only run this after resources have been opened
        protected override void Execute(TestPlanExecutionContext context)
        {
            TestPlanRun run = CreateRunStage.execStage;
            
            var resources = ResourceManagerUtils.GetResourceNodes(run.ResourceManager.StaticResources.Cast<object>().Concat(run.ResourceManager.EnabledSteps));

            TestPlanExecutonHelpers.StartResourcePromptAsync(run, resources.Select(res => res.Resource));
            
            WaitHandle.WaitAny(new[] { run.PromptWaitHandle, TapThread.Current.AbortToken.WaitHandle });
        }
    }
}
