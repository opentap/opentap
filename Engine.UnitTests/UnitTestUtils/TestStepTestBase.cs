//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using OpenTap.Engine.UnitTests;
using OpenTap;

namespace OpenTap.EngineUnitTestUtils
{
    /// <summary>
    ///This is a base class for test classes intended to run a TestStep as a Unit Test
    ///</summary>
    [TestFixture]
    public abstract class TestStepTestBase
    {
        private List<String> allowedLogErrors = new List<string>();

        private TestContext testContextInstance;
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get { return testContextInstance; }
            set { testContextInstance = value; }
        }

        protected void RunTestStep(ITestStep step, List<string> allowedLogErrors)
        {
            this.allowedLogErrors = allowedLogErrors;
            RunTestStep(step);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="step"></param>
        protected void RunTestStep(ITestStep step)
        {
            TestTraceListener trace = new TestTraceListener();
            PlanRunCollectorListener pl = new PlanRunCollectorListener();
            Log.AddListener(trace);

            TestPlan plan = new TestPlan();
            SetupPlan(plan, step);
            TestPlanRun run;
            using (Mutex mux = new Mutex(false, "TAPDatabaseAccessMutex"))
            {
                mux.WaitOne();

                run = plan.Execute(ResultSettings.Current.Concat(new IResultListener[] { pl }));
                mux.ReleaseMutex();
            }
            Log.RemoveListener(trace);
            CheckForInconclusive(trace.ErrorMessage);
            trace.AssertErrors(allowedLogErrors);
            Assert.AreEqual(Verdict.Pass, pl.StepRuns.First().Verdict, "Step ran to completion but verdict was 'fail'.\r\nLog:\r\n" + trace.allLog.ToString());
        }

        protected virtual void CheckForInconclusive(List<string> errorMessage)
        {
            if (errorMessage.Any(em => em.Contains("Failed to open resource")))
                Assert.Inconclusive(string.Join(Environment.NewLine, errorMessage));
        }

        protected virtual void SetupPlan(TestPlan plan, ITestStep step)
        {
            plan.Steps.Add(step);
        }
    }
}
