using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenTap.UnitTests
{
    public class RecordAllResultListener : ResultListener
    {
        public Dictionary<Guid, TestRun> Runs { get; set; } = new Dictionary<Guid, TestRun>();
        public Dictionary<Guid, string> planLogs = new Dictionary<Guid, string>();
        public List<ResultTable> Results = new List<ResultTable>();

        public override void OnTestStepRunStart(TestStepRun stepRun)
        {
            Runs[stepRun.Id] = stepRun;
            base.OnTestStepRunStart(stepRun);
        }

        public override void OnTestStepRunCompleted(TestStepRun stepRun)
        {
            Runs[stepRun.Id] = stepRun;
            base.OnTestStepRunCompleted(stepRun);
        }

        public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
        {
            Runs[planRun.Id] = planRun;
            planLogs[planRun.Id] = new StreamReader(logStream,Encoding.UTF8, true, 4096, true).ReadLine();
            base.OnTestPlanRunCompleted(planRun, logStream);
        }

        public override void OnResultPublished(Guid stepRunId, ResultTable result)
        {
            base.OnResultPublished(stepRunId, result);
            Results.Add(result);
        }
    }
}