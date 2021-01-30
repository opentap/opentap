//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OpenTap
{
    class TestPlanStagedExecutor
    {
        StagedExecutor executor = new StagedExecutor(TypeData.FromType(typeof(IExecutionStage)));

        public void Execute(IEnumerable<IResultListener> resultListeners, IEnumerable<ResultParameter> metaDataParameters, HashSet<ITestStep> stepsOverride)
        {
            // Todo: add actual stages...
            executor.Execute();
        }
    }
}
