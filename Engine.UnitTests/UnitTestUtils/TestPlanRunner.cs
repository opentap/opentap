//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OpenTap.Engine.UnitTests;
using OpenTap;

namespace OpenTap.EngineUnitTestUtils
{
    public class TestPlanRunner
    {
        private TestPlanRunner() { }

        TestTraceListener trace;
        TestPlan plan;

        void RunTestPlanCommon()
        {
            // Add database result output:
            PlanRunCollectorListener pl = new PlanRunCollectorListener();
           
            const string outputDir = @"c:\PlatformTests\";
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            // Set log file path:
            LogResultListener log = ResultSettings.Current.GetDefault<LogResultListener>();
            if (log == null)
            {
                log = new LogResultListener();
                ResultSettings.Current.Add(log);
            }
            string logFilePath = String.Format(@"c:\PlatformTests\{0}.log", plan.Name);
            log.FilePath.Text = logFilePath;
            TestPlanRun planRun;
            using (Mutex mux = new Mutex(false, "TAPDatabaseAccessMutex"))
            {
                mux.WaitOne();
                planRun = plan.Execute(ResultSettings.Current.Concat(new IResultListener[] { pl }));
                mux.ReleaseMutex();
            }
            List<string> allowedMessages = new List<string>
            {
                "DUT chipset is not verified to work with this application."
            };

            ResultSettings.Current.Remove(log);
            Log.RemoveListener(trace);
            trace.AssertErrors(allowedMessages);
            foreach (var stepRun in pl.StepRuns)
            {
                Assert.IsTrue(stepRun.Verdict <= Verdict.Pass, "TestPlan ran to completion but verdict was '{1}' on step '{0}'.\r\nLog:\r\n{2}",
                    stepRun.TestStepName,
                    stepRun.Verdict, trace.GetLog());
            }
        }

        static TestPlanRunner runTestPlanPre()
        {
            TestTraceListener trace = new TestTraceListener();
            Log.AddListener(trace);
            return new TestPlanRunner { trace = trace };
        }

        public static void RunTestPlan(TestPlan plan)
        {
            var runner = runTestPlanPre();
            runner.plan = plan;
            runner.RunTestPlanCommon();
        }

        public static void RunTestPlan(string resourceNamespaceName, string testPlanName)
        {
            var runner = runTestPlanPre();
            TestTraceListener trace = new TestTraceListener();
            Log.AddListener(trace);

            AutoResetEvent plugInSearchComplete = new AutoResetEvent(false);
            var searching = PluginManager.SearchAsync();

            Assembly asm = Assembly.GetCallingAssembly();
            Stream planRes = asm.GetManifestResourceStream(resourceNamespaceName + "." + testPlanName);
            using (TextReader reader = new StreamReader(planRes))
            {
                File.WriteAllText(testPlanName, reader.ReadToEnd());
            }
            
            TestPlan plan = TestPlan.Load(testPlanName);
            runner.plan = plan;
            runner.RunTestPlanCommon();
        }
    }
}
