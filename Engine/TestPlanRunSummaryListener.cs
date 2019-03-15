//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace OpenTap
{
    /// <summary>
    /// Class for generating the summary for a test plan.
    /// </summary>
    [Browsable(false)]
    internal class TestPlanRunSummaryListener : ResultListener
    {
        const int stepRunLimit = 10000;

        // null when stepRunLimit is exceeded.
        // parent run to child run.
        Dictionary<Guid, TestStepRun> stepRuns = new Dictionary<Guid, TestStepRun>();

        TestPlanRun planRun;

        static TraceSource summaryLog =  OpenTap.Log.CreateSource("Summary");

        /// <summary>Creates an instance and removes the ResultListener log from the TraceSources, so log messages from this listener wont go anywhere.</summary>
        public TestPlanRunSummaryListener()
        {
            Name = "Summary";
            OpenTap.Log.RemoveSource(Log);
        }

        /// <summary>Clears the memory.</summary>
        /// <param name="planRun"></param>
        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            stepRuns = new Dictionary<Guid, TestStepRun>();
            this.planRun = planRun;
        }

        /// <summary>Saves which steps are started.</summary>
        /// <param name="stepRun"></param>
        public override void OnTestStepRunStart(TestStepRun stepRun)
        {
            if (stepRun == null)
                throw new ArgumentNullException("stepRun");
            base.OnTestStepRunStart(stepRun);
            if (stepRuns == null) return;
            stepRuns[stepRun.Id] = stepRun;
            if (stepRuns.Count > stepRunLimit)
            {
                stepRuns = null;
            }
        }

        public override void OnTestStepRunCompleted(TestStepRun stepRun)
        { // this is mostly for updating stepRun's duration.
            
            if (stepRun == null)
                throw new ArgumentNullException("stepRun");
            base.OnTestStepRunCompleted(stepRun);
            if (stepRuns == null) return;
            stepRuns[stepRun.Id] = stepRun;
            if (stepRuns.Count > stepRunLimit)
            {
                stepRuns = null;
            }
        }

        int stepRunIndent(TestStepRun stepRun)
        {
            int indent = 0;
            TestStepRun p = stepRun;

            while (stepRuns.ContainsKey(p.Parent))
            {
                p = stepRuns[p.Parent];
                indent++;
            }

            return indent;
        }

        void printSummary(TestStepRun run, int maxIndent, int idx, ILookup<Guid, TestStepRun> lookup)
        {
            int indentcnt = stepRunIndent(run);
            string indent = new String(' ', indentcnt * 2);
            string inverseIndent = new String(' ', maxIndent * 2 - indentcnt * 2);
            
            var (timeString, unit) = ShortTimeSpan.FromSeconds(run.Duration.TotalSeconds).ToStringParts();
            string v = run.Verdict == Verdict.NotSet ? "" : run.Verdict.ToString();
            var name = run.TestStepName;
            summaryLog.Info("{4} {0,-43} {5}{1,5:0} {7,-4}{2,-7}", name, timeString, v, idx, indent, inverseIndent, 0, unit);

            int idx2 = 0;
            foreach (TestStepRun run2 in lookup[run.Id])
                printSummary(run2, maxIndent, idx2, lookup);
        }

        private readonly string separator = new string('-', 44);

        /// <summary>
        /// Prints the summary.
        /// </summary>
        public void PrintSummary()
        {
            if (planRun == null) return; //Something very wrong happened. In this case the use will be informed of an error anyway.
            bool hasOtherVerdict = planRun.Verdict != Verdict.Pass && planRun.Verdict != Verdict.NotSet;
            ILookup<Guid, TestStepRun> parentLookup = null;
            int maxIndent = 0;
            if (stepRuns != null)
            {
                parentLookup = stepRuns.Values.ToLookup(v => v.Parent);

                Func<Guid, int> getMaxIndent = null;
                getMaxIndent = guid => 1 + parentLookup[guid].Select(step => getMaxIndent(step.Id)).DefaultIfEmpty(0).Max();

                maxIndent = getMaxIndent(planRun.Id);
            }

            int maxVerdictLength = hasOtherVerdict ? planRun.Verdict.ToString().Length : 0;
            if (stepRuns != null)
            {
                foreach(TestStepRun run in parentLookup[planRun.Id])
                {
                    int max = getMaxVerdictLength(run, maxVerdictLength, parentLookup);
                    if (max > maxVerdictLength)
                    {
                        maxVerdictLength = max;
                    }
                }
            }

            int addPadLength = maxIndent + (int)Math.Round(maxVerdictLength / 2.0);
            int maxLength = -1;

            Func<string, int, string> formatSummaryHeader = (message, indentLength) => {
                string indent = new String('-', indentLength + 3);
                StringBuilder sb = new StringBuilder(indent);
                sb.Append(message);
                sb.Append(indent);
                maxLength = sb.Length;
                return sb.ToString();
            };

            Func<string, string> formatSummary = (message) => {
                int fillLength = (int)Math.Floor((maxLength - message.Length) / 2.0);
                StringBuilder sb = new StringBuilder(new string('-', fillLength));
                sb.Append(message);
                sb.Append('-', maxLength - sb.Length);
                return sb.ToString();
            };

            summaryLog.Info(formatSummaryHeader(String.Format(" Summary of test plan started {0} ", planRun.StartTime), addPadLength));
            if (stepRuns != null)
            {
                var baseruns = parentLookup[planRun.Id];
                int idx = 0;
                foreach (TestStepRun run in parentLookup[planRun.Id])
                    printSummary(run, maxIndent, idx, parentLookup);
            }
            else
            {
                summaryLog.Warning("Test plan summary skipped. Summary contains too many steps ({0}).", stepRunLimit);
            }
            summaryLog.Info(formatSummary(separator));
            if (!hasOtherVerdict)
            {
                summaryLog.Info(formatSummary(String.Format(" Test plan completed successfully in {0,6} ", ShortTimeSpan.FromSeconds(planRun.Duration.TotalSeconds).ToString())));
            }
            else
            {
                summaryLog.Info(formatSummary(String.Format(" Test plan completed with verdict {1} in {0,6} ", ShortTimeSpan.FromSeconds(planRun.Duration.TotalSeconds).ToString(), planRun.Verdict)));
            }
        }

        private int getMaxVerdictLength(TestStepRun run, int maxVerdictLength, ILookup<Guid, TestStepRun> lookup)
        {
            int maxLength = run.Verdict != Verdict.NotSet ? run.Verdict.ToString().Length : 0;
            foreach(TestStepRun run2 in lookup[run.Id])
            {
                int max = getMaxVerdictLength(run2, maxLength, lookup);
                if (max > maxLength)
                {
                    maxLength = max;
                }
            }

            return maxLength > maxVerdictLength ? maxLength : maxVerdictLength;
        }
    }
}
