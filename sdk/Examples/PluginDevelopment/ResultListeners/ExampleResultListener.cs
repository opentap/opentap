//Copyright 2012-2019 Keysight Technologies
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
using System.IO;
using System.Text;
using OpenTap;

// This result listener is useful in that it logs a subset of all results to the log window.
// This simplifies seeing the results of calls to PublishResults by 
// immediately making the results visible in the log window.

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Example Result Listener", 
        Group: "Plugin Development",
        Description: "Listens to results and pushes data into the log. Useful for simple debugging of PublishResults calls.")]
    public class ExampleResultListener : ResultListener
    {
        public enum ResultLogDepth
        {
            None,
            All,
            Defined
        };
        
        [Display("Result Log Depth Choice", Order: 1, Description: "Choose to log any test step results.")]
        public ResultLogDepth ResultLogDepthChoice { get; set; }

        [EnabledIf("ResultLogDepthChoice", ResultLogDepth.Defined)]
        [Display("Result Log Depth Size", Order: 2, Description: "The maximum number of rows to display per PublishResults call.")]
        public int ResultLogDepthSize { get; set; } 

        public ExampleResultListener()
        {
            Name = "Example";
            ResultLogDepthChoice = ResultLogDepth.Defined;
            ResultLogDepthSize = 5;
        }

        public override void Open()
        {
            base.Open();
        }
        
        public override void Close()
        {
            base.Close();
        }

        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            Log.Info("Test plan \"{0}\" started", planRun.TestPlanName);
        }

        public override void OnTestStepRunStart(TestStepRun stepRun)
        {
            Log.Info("Test step \"{0}\" started", stepRun.TestStepName);
        }

        public override void OnResultPublished(Guid stepRunId, ResultTable resultTable)
        {
            // This is where results are processed. This call was initiated by a call to Results.Publish.
            base.OnResultPublished(stepRunId, resultTable);
            if (ResultLogDepthChoice == ResultLogDepth.None) return;

            int maxRowIndex = MaxRowCount(resultTable);
            switch (ResultLogDepthChoice)
            {
                case ResultLogDepth.None:
                    maxRowIndex = 0;
                    break;
                case ResultLogDepth.All:
                    maxRowIndex = Math.Max(maxRowIndex, 0);
                    break;
                case ResultLogDepth.Defined:
                    maxRowIndex = Math.Max(Math.Min(ResultLogDepthSize, maxRowIndex), 0);
                    break;
            }

            // Write some data from the result table.
            Log.Info("ResultTableName={0}", resultTable.Name);

            // Write out the result table column names.
            StringBuilder sb = new StringBuilder();
            foreach (ResultColumn rc in resultTable.Columns)
            {
                sb.AppendFormat("\t{0}", rc.Name);
            }
            Log.Info(sb.ToString());

            // Write out the rows for each column.  
            for (int rowIndex = 0; rowIndex < maxRowIndex; rowIndex++)
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
        }

        public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
        {
           Log.Info("Test plan completed, run duration = {0} seconds, with {1} params", planRun.Duration.TotalSeconds,
                planRun.Parameters.Count);
        }
                
        private int MaxRowCount(ResultTable resultTable)
        {
            int maxRowCount = 0;
            foreach (ResultColumn rc in resultTable.Columns)
            {
                maxRowCount = Math.Max(maxRowCount, rc.Data.Length);
            }
            return maxRowCount;
        }
    }
}
