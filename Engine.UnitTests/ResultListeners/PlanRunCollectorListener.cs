using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;

namespace OpenTap.Engine.UnitTests
{
    [DisplayName("Test\\Test Plan Collector")]
    [System.ComponentModel.Description("Gathers Test Plans and Test Steps into lists for manipulation in code.")]
    public class PlanRunCollectorListener : ResultListener
    {
        [XmlIgnore]
        public List<TestPlanRun> PlanRuns = new List<TestPlanRun>();
        [XmlIgnore]
        public List<TestStepRun> StepRuns = new List<TestStepRun>();

        public string LogString;

        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            base.OnTestPlanRunStart(planRun);
            Results.Clear();
        }

        public override void OnTestPlanRunCompleted(TestPlanRun planRun, System.IO.Stream logStream)
        {
            PlanRuns.Add(planRun);
            LogString = new System.IO.StreamReader(logStream).ReadToEnd();
        }

        public override void OnTestStepRunStart(TestStepRun stepRun)
        {
            base.OnTestStepRunStart(stepRun);
        }

        public override void OnTestStepRunCompleted(TestStepRun stepRun)
        {
            StepRuns.Add(stepRun);
        }
        
        public PlanRunCollectorListener()
        {
            Name = "Collector";
        }
        public bool WasOpened { get; private set; }
        
        public override void Open()
        {
            base.Open();
            WasOpened = true;
        }

        public bool CollectResults = false;

        public class CollectedResult
        {
            public Guid StepRunId;
            public ResultTable Result;

            public override string ToString() => $"Collected Result, {Result.Rows} rows, {Result.Columns.Length} columns.";
        }

        public List<CollectedResult> Results = new List<CollectedResult>();

        public override void OnResultPublished(Guid stepRunId, ResultTable result)
        {
            if (CollectResults)
            {
                Results.Add(new CollectedResult { StepRunId = stepRunId, Result = result });
            }
            base.OnResultPublished(stepRunId, result);

        }
    }
}