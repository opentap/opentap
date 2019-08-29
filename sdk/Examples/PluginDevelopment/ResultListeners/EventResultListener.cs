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
using OpenTap;

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Event Result Listener", 
        Group: "Plugin Development",
        Description: "Converts all the ResultListener function calls to equivalent events. Does NOT actually store any results.")]
    public class EventResultListener : ResultListener
    {
        public EventResultListener()
        {
            Name = "Evnt";
        }

        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            TestPlanRunStartedEventArgs e = new TestPlanRunStartedEventArgs(planRun);
            RaiseTestPlanRunStarted(e);
        }

        public override void OnTestStepRunStart(TestStepRun stepRun)
        {
            TestStepRunStartedEventArgs e = new TestStepRunStartedEventArgs(stepRun);
            RaiseTestStepRunStarted(e);
        }

        public override void OnResultPublished(Guid stepRun, ResultTable result)
        {
            ResultPublishedEventArgs e = new ResultPublishedEventArgs(stepRun, result);
            RaiseResultPublished(e);
            OnActivity();
        }

        public override void OnTestStepRunCompleted(TestStepRun stepRun)
        {
            TestStepRunCompletedEventArgs e = new TestStepRunCompletedEventArgs(stepRun);
            RaiseTestStepRunCompleted(e);
        }

        public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
        {
            TestPlanRunCompletedEventArgs e = new TestPlanRunCompletedEventArgs(planRun, logStream);
            RaiseTestPlanRunCompleted(e);
        }
       
        public override void Close()
        {
            base.Close();
            //Add resource close code.
        }

        #region Event related code  
        public event EventHandler<TestPlanRunStartedEventArgs> TestPlanRunStarted;
        protected void RaiseTestPlanRunStarted(TestPlanRunStartedEventArgs e)
        {
            var handler = TestPlanRunStarted;
            if (handler != null) handler(this, e);
        }

        public event EventHandler<TestStepRunStartedEventArgs> TestStepRunStarted;
        protected void RaiseTestStepRunStarted(TestStepRunStartedEventArgs e)
        {
            var handler = TestStepRunStarted;
            if (handler != null) handler(this, e);
        }
        
        public event EventHandler<ResultPublishedEventArgs> ResultPublished;
        protected void RaiseResultPublished(ResultPublishedEventArgs e)
        {
            var handler = ResultPublished;
            if (handler != null) handler(this, e);
        }
        
        public event EventHandler<TestStepRunCompletedEventArgs> TestStepRunCompleted;
        protected void RaiseTestStepRunCompleted(TestStepRunCompletedEventArgs e)
        {
            var handler = TestStepRunCompleted;
            if (handler != null) handler(this, e);
        }
        
        public event EventHandler<TestPlanRunCompletedEventArgs> TestPlanRunCompleted;
        protected void RaiseTestPlanRunCompleted(TestPlanRunCompletedEventArgs e)
        {
            var handler = TestPlanRunCompleted;
            if (handler != null) handler(this, e);
        }
        #endregion
    }
   
    public class TestPlanRunStartedEventArgs : EventArgs
    {
        public TestPlanRun TestPlanRun { get; set; }

        public TestPlanRunStartedEventArgs(TestPlanRun testplanRun)
        {
            TestPlanRun = testplanRun;
        }
    }

    public class TestStepRunStartedEventArgs : EventArgs
    {
        public TestStepRun TestStepRun { get; set; }

        public TestStepRunStartedEventArgs(TestStepRun testStepRun)
        {
            TestStepRun = testStepRun;
        }
    }

    public class ResultPublishedEventArgs : EventArgs
    {
        public Guid StepRunId { get; set; }
        public ResultTable ResultTable { get; set; }

        public ResultPublishedEventArgs(Guid stepRunId, ResultTable resultTable)
        {
            StepRunId = stepRunId;
            ResultTable = resultTable;
        }
    }

    public class TestStepRunCompletedEventArgs : EventArgs
    {
        public TestStepRun TestStepRun { get; set; }

        public TestStepRunCompletedEventArgs(TestStepRun testStepRun)
        {
            TestStepRun = testStepRun;
        }
    }

    public class TestPlanRunCompletedEventArgs : EventArgs
    {
        public TestPlanRun TestPlanRun { get; set; }
        public Stream LogStream { get; set; }

        public TestPlanRunCompletedEventArgs(TestPlanRun testPlanRun, Stream logStream)
        {
            TestPlanRun = testPlanRun;
            LogStream = logStream;
        }
    }
}
