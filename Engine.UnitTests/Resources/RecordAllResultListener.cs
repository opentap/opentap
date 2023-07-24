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
        
        // 1:1 with the Results list.
        public List<Guid> ResultTableGuids = new List<Guid>();
        public Action OnTestStepRunStartAction = () => { };

        public override void OnTestStepRunStart(TestStepRun stepRun)
        {
            OnTestStepRunStartAction();
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
            planLogs[planRun.Id] = new StreamReader(logStream,Encoding.UTF8, true, 4096, true).ReadToEnd();
            base.OnTestPlanRunCompleted(planRun, logStream);
        }

        public override void OnResultPublished(Guid stepRunId, ResultTable result)
        {
            //TapThread.Sleep(1);
            base.OnResultPublished(stepRunId, result);
            Results.Add(result);
            ResultTableGuids.Add(stepRunId);
        }
    }
}