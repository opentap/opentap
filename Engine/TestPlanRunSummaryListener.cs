//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTap
{
    /// <summary> Class for generating the summary for a test plan. </summary>
    [Browsable(false)]
    class TestPlanRunSummaryListener : ResultListener, IArtifactListener
    {
        private int stepRunLimit => EngineSettings.Current.SummaryTestStepLimit;

        // Avoid holding on to references to TestStepRuns (fat objects) and only store the information 
        // we need.
        struct TestStepRunData
        {
            public readonly Guid Parent;
            public readonly Guid Id;
            public readonly Verdict Verdict;
            public readonly string TestStepName;
            public readonly TimeSpan Duration;
            TestStepRunData(TestStepRun run)
            {
                Parent = run.Parent;
                Verdict = run.Verdict;
                Id = run.Id;
                TestStepName = run.TestStepName;
                Duration = run.Duration;
            }

            public static TestStepRunData FromTestStepRun(TestStepRun run)
            {
                return new TestStepRunData(run);
            }
        }

        // null when stepRunLimit is exceeded.
        // parent run to child run.
        Dictionary<Guid, TestStepRunData> stepRuns = new Dictionary<Guid, TestStepRunData>();

        TestPlanRun planRun;

        static readonly TraceSource summaryLog =  OpenTap.Log.CreateSource("Summary");

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
            stepRuns = new Dictionary<Guid, TestStepRunData>();
            this.planRun = planRun;

            foreach (var f in tempFiles)
            {
                f.Dispose();
            }
            tempFiles.Clear();
            artifacts.Clear();
        }

        /// <summary>Saves which steps are started.</summary>
        /// <param name="stepRun"></param>
        public override void OnTestStepRunStart(TestStepRun stepRun)
        {
            if (stepRun == null)
                throw new ArgumentNullException(nameof(stepRun));
            base.OnTestStepRunStart(stepRun);
            if (stepRuns == null) return;
            stepRuns[stepRun.Id] = TestStepRunData.FromTestStepRun(stepRun);
            if (stepRuns.Count > stepRunLimit)
            {
                stepRuns = null;
            }
        }
        
        public override void OnTestStepRunCompleted(TestStepRun stepRun)
        { // this is mostly for updating stepRun's duration.
            
            if (stepRun == null)
                throw new ArgumentNullException(nameof(stepRun));
            base.OnTestStepRunCompleted(stepRun);
            if (stepRuns == null)
                return;
            stepRuns[stepRun.Id] = TestStepRunData.FromTestStepRun(stepRun);
            if (stepRuns.Count > stepRunLimit)
            {  
                // Too many steps, so we skip the summary.
                stepRuns = null;
            }
        }

        static int stepRunIndent(IDictionary<Guid, TestStepRunData> stepRuns, TestStepRunData stepRun)
        {
            int indent = 0;
            TestStepRunData p = stepRun;

            while (stepRuns.ContainsKey(p.Parent))
            {
                p = stepRuns[p.Parent];
                indent++;
            }

            return indent;
        }

        static void printSummary(IDictionary<Guid, TestStepRunData> stepRuns, TestStepRunData run, int maxIndent, int idx, ILookup<Guid, TestStepRunData> lookup)
        {
            int indentcnt = stepRunIndent(stepRuns, run);
            string indent = new String(' ', indentcnt * 2);
            string inverseIndent = new String(' ', maxIndent * 2 - indentcnt * 2);

            string v = run.Verdict == Verdict.NotSet ? "" : run.Verdict.ToString();
            var name = run.TestStepName;
            // this prints something like this: '12:42:16.302  Summary       Delay                                             106 ms'
            summaryLog.Info($"{indent} {name,-43} {inverseIndent}{ShortTimeSpan.LongTimeSpanFormat(run.Duration)} {v,-7}");

            int idx2 = 0;
            foreach (TestStepRunData run2 in lookup[run.Id])
                printSummary(stepRuns, run2, maxIndent, idx2, lookup);
        }

        private readonly string separator = new string('-', 44);

        static string FormatSize(long size)
        {
            
            if (size < 1000)
                return $"{size} B";
            
            if (size < 1000000)
                return $"{Math.Round(((double)size) / 1000, 1)} kB";
            
            return $"{Math.Round(((double)size) / 1000_000, 1)} MB";
        } 
        
        /// <summary> Prints the artifact summary. </summary>
        public void PrintArtifactsSummary()
        {
            string formatSummary(string message)
            {
                int fillLength = (int)Math.Floor((maxLength - message.Length) / 2.0);
                StringBuilder sb = new StringBuilder(new string('-', fillLength));
                sb.Append(message);
                sb.Append('-', maxLength - sb.Length);
                return sb.ToString();
            }

            if (artifacts.Count > 0)
            {
                summaryLog.Info(formatSummary($" {artifacts.Count} artifacts registered. "));
                foreach (var artifact in artifacts)
                {
                    var artifactSize = new FileInfo(artifact).Length;
                    summaryLog.Info($" - {Path.GetFullPath(artifact)}  [{FormatSize(artifactSize)}]");
                }
            }
            else
            {
                //summaryLog.Info(formatSummary($" No artifacts registered. "));
            }
        }
        int maxLength = -1;
        /// <summary> Prints the summary. </summary>
        public void PrintSummary()
        {
            Dictionary<Guid, TestStepRunData> thisRuns = null;
            Utils.Swap(ref thisRuns, ref stepRuns);
            
            maxLength = -1;
            if (planRun == null) return; //Something very wrong happened. In this case the use will be informed of an error anyway.
            bool hasOtherVerdict = planRun.Verdict != Verdict.Pass && planRun.Verdict != Verdict.NotSet;
            ILookup<Guid, TestStepRunData> parentLookup = null;
            int maxIndent = 0;
            if (thisRuns != null)
            {
                parentLookup = thisRuns.Values.ToLookup(v => v.Parent);

                Func<Guid, int> getMaxIndent = null;
                getMaxIndent = guid => 1 + parentLookup[guid].Select(step => getMaxIndent(step.Id)).DefaultIfEmpty(0).Max();

                maxIndent = getMaxIndent(planRun.Id);
            }

            int maxVerdictLength = hasOtherVerdict ? planRun.Verdict.ToString().Length : 0;
            if (thisRuns != null)
            {
                foreach(TestStepRunData run in parentLookup[planRun.Id])
                {
                    int max = getMaxVerdictLength(run, maxVerdictLength, parentLookup);
                    if (max > maxVerdictLength)
                    {
                        maxVerdictLength = max;
                    }
                }
            }

            int addPadLength = maxIndent + (int)Math.Round(maxVerdictLength / 2.0);
            

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

            summaryLog.Info(formatSummaryHeader($" Summary of test plan started {planRun.StartTime} ", addPadLength));
            if (thisRuns != null)
            {
                int idx = 0;
                foreach (TestStepRunData run in parentLookup[planRun.Id])
                    printSummary(thisRuns, run, maxIndent, idx, parentLookup);
            }
            else
            {
                summaryLog.Warning("Test plan summary skipped. Summary contains too many steps ({0}).", stepRunLimit);
            }
            summaryLog.Info(formatSummary(separator));
            

            
            if (!hasOtherVerdict)
            {
                summaryLog.Info(formatSummary($" Test plan completed successfully in {ShortTimeSpan.LongTimeSpanFormat(planRun.Duration)} "));
            }
            else
            {
                summaryLog.Info(formatSummary(string.Format(" Test plan completed with verdict {1} in {0,6} ", ShortTimeSpan.LongTimeSpanFormat(planRun.Duration), planRun.Verdict)));
            }
        }

        int getMaxVerdictLength(TestStepRunData run, int maxVerdictLength, ILookup<Guid, TestStepRunData> lookup)
        {
            int maxLength = run.Verdict != Verdict.NotSet ? run.Verdict.ToString().Length : 0;
            foreach(TestStepRunData run2 in lookup[run.Id])
            {
                int max = getMaxVerdictLength(run2, maxLength, lookup);
                if (max > maxLength)
                {
                    maxLength = max;
                }
            }

            return maxLength > maxVerdictLength ? maxLength : maxVerdictLength;
        }

        // keeps track of published artifacts.
        HashSet<string> artifacts { get; set; } = new HashSet<string>();
        
        // these tmp files are used to keep temporary (stream) files alive until the next test plan run.
        readonly List<FileStream> tempFiles = new List<FileStream>();
        readonly string targetLoc = Path.Combine(Path.GetTempPath(), "OpenTAP", "Temporary Artifacts", Guid.NewGuid().ToString());
        public void OnArtifactPublished(TestRun run, Stream artifactStream, string artifactName)
        {
            // when an artifact is published, keep track of it.
            // if it is a file artifact (FileStream), we just keep track of the names.
            // if is a non-artifact stream, persist it on disk for a while.
            
            using var _ = artifactStream;
            if (artifactStream is FileStream originFileStream)
            {
                artifacts.Add(originFileStream.Name);
                return;
            }
            var tmpFileName = Path.Combine(targetLoc, Path.GetFileName(artifactName));
            if (artifacts.Contains(tmpFileName)) return;
            FileSystemHelper.EnsureDirectoryOf(tmpFileName);
            
            // DeleteOnClose: persist the file until it is closed (next test plan run).
            var fileStream = new FileStream(tmpFileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
            
            artifactStream.CopyTo(fileStream);
            fileStream.Flush();
            
            artifacts.Add(tmpFileName);
            tempFiles.Add(fileStream);
        }
    }
}
