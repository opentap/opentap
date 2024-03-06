//Copyright 2012-2023 Keysight Technologies
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// This result listener is useful in that it logs a subset of all results to the log window.
// This simplifies seeing the results of calls to PublishResults by 
// immediately making the results visible in the log window.

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Example Result Listener", 
        Group: "Plugin Development",
        Description: "Listens to results and writes the recorded data into the log. This shows the basic principles of working with result listeners.")]
    public class ExampleResultListener : ResultListener
    {
        public ExampleResultListener()
        {
            Name = "Example";
        }

        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            Log.Info("Test plan \"{0}\" started", planRun.TestPlanName);
        }

        // Keeps track of all the currently running test steps.
        readonly Dictionary<Guid, TestStepRun> activeTestStepRuns = new Dictionary<Guid, TestStepRun>();
        
        public override void OnTestStepRunStart(TestStepRun stepRun)
        {
            Log.Info("Test step \"{0}\" started", stepRun.TestStepName);
            
            // Add the run to the lookup table.
            activeTestStepRuns[stepRun.Id] = stepRun;
        }

        public override void OnResultPublished(Guid stepRunId, ResultTable resultTable)
        {
            // This is where results are processed. This call was initiated by a call to Results.Publish.
            // all calls to this are guaranteed to happen between OnTestStepRunStart and OnTestStepRunCompleted for a given test step run.
            // all calls to the methods in a result listener are also guaranteed to be called sequentially (not in parallel). 
            
            base.OnResultPublished(stepRunId, resultTable);

            // get the run information corresponding to the run ID.
            TestStepRun stepRun = activeTestStepRuns[stepRunId];
            
            // Write some data from the result table.
            Log.Info("ResultTableName={0} (TestStep={1})", resultTable.Name, stepRun.TestStepName);

            // Write out the result table column names.
            StringBuilder sb = new StringBuilder();
            foreach (ResultColumn rc in resultTable.Columns)
            {
                sb.AppendFormat("\t{0}", rc.Name);
            }
            Log.Info(sb.ToString());

            // Write out the rows for each column.  
            for (int rowIndex = 0; rowIndex < resultTable.Rows; rowIndex++)
            {
                sb.Clear();
                sb.AppendFormat("Row={0}\t", rowIndex);
                foreach (ResultColumn rc in resultTable.Columns)
                {
                    // Make sure to check to make sure each column has enough rows.
                    if (rowIndex < rc.Data.Length)
                    {
                        sb.AppendFormat("{0}\t", rc.Data.GetValue(rowIndex));
                    }
                    else
                    {
                        sb.AppendFormat("\t");
                    }
                }
                sb.Append(Environment.NewLine);
                Log.Info(sb.ToString());
            }
        }

        public override void OnTestStepRunCompleted(TestStepRun stepRun)
        {
            Log.Info("Test step \"{0}\" completed in {1} milliseconds", stepRun.TestStepName,
                stepRun.Duration.TotalMilliseconds);
            
            // Remove the test step run from the lookup table.
            // Since it won't produce more results, it is no longer needed.
            // Note there is a distinction between TestStep and TestStepRun:
            //    A given TestStepRun corresponds to a specific TestStep, but a single TestStep may be
            //    executed multiple times during the test plan, resulting in many TestStepRuns (different IDs).
            activeTestStepRuns.Remove(stepRun.Id);
        }

        public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
        {
           Log.Info("Test plan completed, run duration = {0} seconds, with {1} params", planRun.Duration.TotalSeconds,
                planRun.Parameters.Count);
        }
    }
}
