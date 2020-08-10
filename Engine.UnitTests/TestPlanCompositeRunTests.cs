//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using System.Xml.Serialization;
using OpenTap.EngineUnitTestUtils;
using OpenTap;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class TestPlanCompositeRunTests 
    {
        
        private string[] filterLog(string allLog, bool removeSpaces = false)
        {
            allLog = Regex.Replace(allLog, "[0-9]+", "0");
            allLog = Regex.Replace(allLog, "0 [mnuμ]s", "0 s");
            allLog = Regex.Replace(allLog, "0.0 s", "0 s");
            allLog = Regex.Replace(allLog, "--+", ""); // removes -- decorators

            string[] allLines = allLog.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            // lines from "System" module needs to be ignores as they apear randomly in the log (happening on a background thread)
            allLines = allLines.Where(line => !line.Contains(": System")).ToArray();
            // also remove lines from "PluginManager" module (licensing messages happening async)
            allLines = allLines.Where(line => !line.Contains(": PluginManager")).ToArray();
            allLines = allLines.Where(line => !line.Contains(": License")).ToArray();
            allLines = allLines.Where(line => !line.Contains(": Session")).ToArray();
            allLines = allLines.Where(line => !line.Contains(" loaded from ")).ToArray();
            allLines = allLines.Where(line => !line.Contains("No settings file exists for ")).ToArray();

            if (removeSpaces)
            {
                allLines = allLines.Select(line => line.Replace(" ", "")).ToArray();
            }

            return allLines;
        }

        [Test]
        public void RunCompositeLogComparison()
        {
            // ## Fragile Test
            // If the settings directory has been loaded in the meantime
            // Log messages that new files are being created will appear on first run
            // ComponentSettings are lazily loaded so that first happens when the test plan runs.
            // We do one run to clear the state of the engine.
            {
                TestPlan target2 = getTestTestPlan();
                Log.Flush();
                target2.PrintTestPlanRunSummary = true;
                target2.Execute();
                Log.Flush();
            }

            TestTraceListener trace1 = new TestTraceListener();
            Log.AddListener(trace1);
            TestPlan target = getTestTestPlan();
            target.PrintTestPlanRunSummary = true;
            target.Execute();
            Log.RemoveListener(trace1);

            TestTraceListener trace2 = new TestTraceListener();
            Log.AddListener(trace2);
            target = getTestTestPlan();
            target.PrintTestPlanRunSummary = true;
            target.Open();
            target.Execute();
            target.Close();
            Log.RemoveListener(trace2);

            string allLog1 = trace1.allLog.ToString();
            string allLog2 = trace2.allLog.ToString();
            string[] log1Lines = filterLog(allLog1);
            string[] log2Lines = filterLog(allLog2);

            string[] log2LinesNoSpaces = filterLog(allLog2, true);

            Assert.AreEqual(log1Lines.Count() + 2, log2Lines.Count(), allLog1 + Environment.NewLine + "##########" + Environment.NewLine + allLog2);
            for (int i = 0; i < log1Lines.Length; i++)
            {
                var line = log1Lines[i].Replace(" ", "");
                if (!log2LinesNoSpaces.Contains(line)) // We compare lines with removed spaces to avoid flakyness in CI.
                {
                    // Print actual comparison data
                    Console.WriteLine($"Could not find '{line}' in following logs:");
                    foreach (var linez in log2LinesNoSpaces)
                        Console.WriteLine($"{linez}");

                    Console.WriteLine($"--------------- Printing logs without spaces removed ---------------");

                    // Print log data without their spaces removed
                    Console.WriteLine($"First run logs:");
                    foreach (var linez in log1Lines)
                        Console.WriteLine($"- {linez}");

                    Console.WriteLine($"Second run logs:");
                    foreach (var linez in log2Lines)
                        Console.WriteLine($"- {linez}");

                    Assert.Fail($"The logs from two testplan executions does not match...");
                }
            }
        }

        [Test]
        public void ShortTimeSpanTest()
        {
            var v = new[] { 0.01, 0.001, 0.0001, 10e-6, 1e-6, 1e-9, 1e-10 };
            var s = new[] { "10 ms", "1 ms", "100 us", "10 μs", "1 μs", "1 ns", "0 s" };

            for (int i = 0; i < v.Length; i++)
            {
                var s1 = ShortTimeSpan.FromSeconds(v[i]);
                var s2 = ShortTimeSpan.FromString(s[i]);
                StringAssert.AreEqualIgnoringCase(s1.ToString(), s2.ToString());

            }
        }


        [Test]
        public void RunCompositeFollowedByRun()
        {
            TestPlan target = getTestTestPlan();
            
            target.Open();
            target.Execute();
            target.Close();
            Log.Flush();
            TestTraceListener trace2 = new TestTraceListener();
            Log.AddListener(trace2);
            target.Open();
            target.Execute();
            target.Close();
            Log.RemoveListener(trace2);
            TestTraceListener trace1 = new TestTraceListener();
            Log.AddListener(trace1);
            target.Execute();
            Log.RemoveListener(trace1);

            string allLog1 = trace1.allLog.ToString();
            string allLog2 = trace2.allLog.ToString();
            string[] log1Lines = filterLog(allLog1);
            string[] log2Lines = filterLog(allLog2);


            Assert.AreEqual(log1Lines.Count() + 2, log2Lines.Count(), allLog2);
            for (int i = 0; i < log1Lines.Length; i++)
            {
                CollectionAssert.Contains(log2Lines, log1Lines[i]);
            }
        }

        [Test]
        public void RunCompositeOpenClose()
        {
            TestPlan target = getTestTestPlan();
            target.Open();
            target.Close();
            target.Open();
            target.Close();
        }

        [Test]
        public void RunCompositeRunTwice()
        {
            TestTraceListener trace = new TestTraceListener();
            Log.AddListener(trace);
            TestPlan target = getTestTestPlan();
            target.Open();
            target.Execute();
            target.Execute();
            target.Close();
            Log.RemoveListener(trace);

            trace.AssertErrors();

            TestTestStep step = target.Steps[0] as TestTestStep;
            Assert.AreEqual(2, step.PrePlanRunCount, "PrePlanRun was not called the correct number of times.");
            Assert.AreEqual(2, step.PostPlanRunCount, "PostPlanRun was not called the correct number of times.");
        }

        [Test]
        public void RunCompositeCloseWithoutOpen()
        {
            TestTraceListener trace = new TestTraceListener();
            Log.AddListener(trace);
            TestPlan target = getTestTestPlan();
            Assert.Throws(typeof(InvalidOperationException), target.Close);
            Log.RemoveListener(trace);
        }

        [Test]
        public void RunCompositeCloseWhileRunning()
        {
            TestTraceListener trace = new TestTraceListener();
            Log.AddListener(trace);
            TestPlan target = getTestTestPlan();
            var t1 = Task.Factory.StartNew(() => target.Execute());
            try
            {
                Thread.Sleep(30);
                Assert.Throws(typeof(InvalidOperationException),target.Close);
            }
            finally
            {
                Log.RemoveListener(trace);
                t1.Wait();
            }
        }

        [Test]
        public void RunCompositeStartTime()
        {
            PlanRunCollectorListener listener = new PlanRunCollectorListener();
            ResultSettings.Current.Add(listener);

            TestTraceListener trace = new TestTraceListener();
            Log.AddListener(trace);

            TestPlan target = getTestTestPlan();
            target.Open();
            target.Execute();
            listener.StepRuns.Clear();
            target.Execute();
            target.Close();

            Log.RemoveListener(trace);

            ResultSettings.Current.Remove(listener);

            Assert.AreEqual(2, listener.PlanRuns.Select(run => run.StartTimeStamp).Distinct().Count());
            Assert.AreEqual(1, listener.StepRuns.Count());
        }

        [Test]
        public void RunCompositeMetaData()
        {
            PlanRunCollectorListener listener = new PlanRunCollectorListener();
            ResultSettings.Current.Add(listener);

            TestTraceListener trace = new TestTraceListener();
            Log.AddListener(trace);

            TestPlan target = getTestTestPlan();
            TestPlanRun run = target.Execute();
            Log.RemoveListener(trace);

            ResultSettings.Current.Remove(listener);

            Assert.IsTrue(run.Parameters.Any(par => par.Value.ToString() == "Test Instrument"));
            Assert.IsFalse(run.Parameters.Any(par => par.Name == "Comment")); 
        }

        [Test]
        public void RunCompositeMetaData2()
        {
            PlanRunCollectorListener listener = new PlanRunCollectorListener();
            ResultSettings.Current.Add(listener);

            TestTraceListener trace = new TestTraceListener();
            Log.AddListener(trace);

            TestPlan target = getTestTestPlan();
            target.Open();
            target.Execute();
            target.Execute();
            target.Close();
            Log.RemoveListener(trace);

            ResultSettings.Current.Remove(listener);

            Assert.IsTrue(listener.PlanRuns.First().Parameters.Any(par => par.Value.ToString() == "Test Instrument"));
            Assert.AreEqual(1, listener.PlanRuns.Last().Parameters.Count(par => par.Value.ToString() == "Test Instrument"));
        }

        [Test]
        public void RunCompositeAddInstrumentAfterOpen()
        {
            PlanRunCollectorListener listener = new PlanRunCollectorListener();
            ResultSettings.Current.Add(listener);

            TestTraceListener trace = new TestTraceListener();
            Log.AddListener(trace);

            TestPlan target = getTestTestPlan();
            target.Open();

            TestInstrument instr = InstrumentSettings.Current.FirstOrDefault(i => i is TestInstrument && (i as TestInstrument).Name.EndsWith("2")) as TestInstrument;
            if (instr == null)
            {
                instr = new TestInstrument { Name = "Test Instrument 2" };
                InstrumentSettings.Current.Add(instr);
            }
            (target.Steps[0] as TestTestStep).Instr = instr;

            target.Execute();
            listener.StepRuns.Clear();
            target.Execute();
            target.Close();

            Log.RemoveListener(trace);

            ResultSettings.Current.Remove(listener);

            Assert.AreEqual(2, listener.PlanRuns.Select(run => run.StartTimeStamp).Distinct().Count());
            Assert.AreEqual(1, listener.StepRuns.Count());
        }


        #region Helper Methods and Classes/Plugins
        private TestPlan getTestTestPlan()
        {
            if (!InstrumentSettings.Current.Any(instr => instr is TestInstrument))
                InstrumentSettings.Current.Add(new TestInstrument());
            TestPlan target = new TestPlan();
            target.Steps.Add(new TestPlanCompositeRunTests.TestTestStep());
            return target;
        }

        public class TestTestStep : TestStep
        {
            public TestInstrument Instr { get; set; }
            public int PostPlanRunCount = 0;
            public int PrePlanRunCount = 0;
            public override void PrePlanRun()
            {
                PrePlanRunCount++;
            }

            public override void Run()
            {
                if (!Instr.isOpen)
                    throw new Exception("TestStep run before instrument was opened.");
                Log.Info("Running Test TestStep.");
            }

            public override void PostPlanRun()
            {
                PostPlanRunCount++;
            }
        }

        public class TestInstrument : OpenTap.Instrument
        {
            [MetaData]
            public new string Name { get; set; }

            //[MetaData(MetaDataOptions.Ignore)]
            public string Comment { get; set; }

            public TestInstrument()
            {
                Name = "Test Instrument";
            }

            public bool isOpen = false;

            public override void Open()
            {
                base.Open();
                TapThread.Sleep(50);
                isOpen = true;
                Log.Info(Name + " Opened.");
            }

            public override void Close()
            {
                if (!isOpen)
                    throw new Exception("Instrument was closed but not opened.");
                isOpen = false;
                base.Close();
                Log.Info(Name + " Closed.");
            }
        }


        #endregion
    }

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
