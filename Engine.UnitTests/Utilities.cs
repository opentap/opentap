using System;
using System.Collections.Generic;
using System.IO;
namespace OpenTap.Engine.UnitTests;

static class Utilities
{
    public struct RunData
    {
        public TestPlanRun PlanRun;
        public TestStepRun[] StepRuns;
        public (Guid, ResultTable)[] Results;
        public (Guid runId, string artifactName, byte[] artifactData)[] Artifacts;
        public string Log;
    }

    class RunDataResultListener : ResultListener, IArtifactListener
    {
        List<TestStepRun> stepRuns = new();
        List<(Guid, ResultTable)> results = new();
        List<(Guid runId, string artifactName, byte[] artifactData)> artifacts = new();
        TestPlanRun planRun;
        string log;
        
        public override void OnTestStepRunCompleted(TestStepRun stepRun)
        {
            base.OnTestStepRunCompleted(stepRun);
            stepRuns.Add(stepRun);
        }

        public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
        {
            base.OnTestPlanRunCompleted(planRun, logStream);
            this.planRun = planRun;
            log = new StreamReader(logStream).ReadToEnd();
        }

        public override void OnResultPublished(Guid stepRunId, ResultTable result)
        {
            base.OnResultPublished(stepRunId, result);
            results.Add((stepRunId, result));
        }

        public RunData GetData()
        {
            return new RunData
            {
                Artifacts = artifacts.ToArray(),
                Results = results.ToArray(),
                StepRuns = stepRuns.ToArray(),
                PlanRun = planRun,
                Log = log
            };

        }
        
        public void OnArtifactPublished(TestRun run, Stream artifactStream, string artifactName)
        {
            using (artifactStream)
            {
                artifacts.Add((run.Id, artifactName, artifactStream.GetBytes()));
            }
        }
    }
    
    public static RunData ExecuteReturnData(this TestPlan plan)
    {
        var runListener = new RunDataResultListener();
        plan.Execute(new IResultListener[]
        {
            runListener
        });

        return runListener.GetData();

    }
    
    public static byte[] GetBytes(this Stream stream)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();

    }
}
