//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.ComponentModel;
using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Diagnostics;
using OpenTap;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class UserCodeTest 
    {
        [Test]
        public void ResultListeners()
        {
            resultListenerCrash[] crashers = new resultListenerCrash[]{
                new resultListenerCrash{CrashResultPhase = resultListenerCrash.ResultPhase.PlanRunStart},
                new resultListenerCrash{CrashResultPhase = resultListenerCrash.ResultPhase.StepRunStart},
                new resultListenerCrash{CrashResultPhase = resultListenerCrash.ResultPhase.Result},
                new resultListenerCrash{CrashResultPhase = resultListenerCrash.ResultPhase.StepRunCompleted},
                new resultListenerCrash{CrashResultPhase = resultListenerCrash.ResultPhase.PlanRunCompleted},
                new resultListenerCrash{CrashResultPhase = resultListenerCrash.ResultPhase.Open},
                new resultListenerCrash{CrashResultPhase = resultListenerCrash.ResultPhase.Close}
            };

            TestPlan testplan = new TestPlan();
            testplan.Steps.Add(new TestStepTest());
            foreach (var crasher in crashers)
            {
                testplan.Execute(new IResultListener[] { crasher });
            }
        }

        [Test]
        [Ignore("Since the result thread is not synced with the test plan thread, the test plan might complete before the abort is used from the result listeners.")]
        public void ResultListenerAbortPlan()
        {
            // since the 

            resultListenerCrash[] crashers = new resultListenerCrash[]{
                new resultListenerCrash{CrashResultPhase = resultListenerCrash.ResultPhase.PlanRunStart, AbortPlan = true},
                new resultListenerCrash{CrashResultPhase = resultListenerCrash.ResultPhase.StepRunStart, AbortPlan = true},
                new resultListenerCrash{CrashResultPhase = resultListenerCrash.ResultPhase.Result, AbortPlan = true},
                new resultListenerCrash{CrashResultPhase = resultListenerCrash.ResultPhase.StepRunCompleted, AbortPlan = true},
                // The plan cannot be aborted on PlanRunCompleted. The at that point it will be ignored.
                new resultListenerCrash{CrashResultPhase = resultListenerCrash.ResultPhase.PlanRunCompleted, AbortPlan = true}
            };

            TestPlan testplan = new TestPlan();
            testplan.Steps.Add(new TestStepTest());
            foreach (var c in crashers)
            {
                c.FinalVerdict = Verdict.NotSet;
                var expectedVerdict = Verdict.Aborted;

                if (c.CrashResultPhase != resultListenerCrash.ResultPhase.PlanRunStart)
                    expectedVerdict = Verdict.Pass;

                // Simply running the plan.
                var planrun = testplan.Execute(new IResultListener[] { c });
                Assert.AreEqual(expectedVerdict, planrun.Verdict);
                Assert.AreEqual(expectedVerdict, c.FinalVerdict);

                // Test that it works in composite runs.
                // Here it's important that the abort does not spill into the next run.
                // Which is why the plan is run twice.
                testplan.Open(new IResultListener[] { c });
                c.FinalVerdict = Verdict.NotSet;
                Assert.AreEqual(expectedVerdict, testplan.Execute(new IResultListener[] { c }).Verdict);
                Assert.AreEqual(expectedVerdict, c.FinalVerdict);
                c.CrashResultPhase = resultListenerCrash.ResultPhase.None;
                c.FinalVerdict = Verdict.NotSet;
                Assert.AreEqual(Verdict.Pass, testplan.Execute(new IResultListener[] { c }).Verdict);
                Assert.AreEqual(Verdict.Pass, c.FinalVerdict);
                testplan.Close();
            }
            

        }
        

        [Test]
        public void VersionProperties()
        {
            TestPlan tp = new TestPlan();
            tp.ChildTestSteps.Add(new Plugins.BasicSteps.DelayStep());
            var tpr = tp.Execute();

            Assert.IsTrue(tpr.Parameters.Any(p => p.Group == "Version" && p.Name == "OpenTap"), "No engine version parameter found");
        }

        [Test]
        public void InstrumentTestMethod()
        {
            InstrumentTest openCrash = new InstrumentTest { CrashPhase = InstrumentTest.InstrPhase.Open };
            InstrumentTest closeCrash = new InstrumentTest { CrashPhase = InstrumentTest.InstrPhase.Close };
            foreach (var instr in new IInstrument[] { openCrash, closeCrash })
            {
                InstrumentTestStep step = new InstrumentTestStep { Instrument = instr };
                TestPlan plan = new TestPlan();
                plan.Steps.Add(step);
                plan.Execute();
            }
        }

        [Test]
        public void DutInheritingFromIDut()
        {
            try
            {
                var dut = new InterfaceTestDut();
                DutSettings setting = new DutSettings();
                setting.Add(dut);
            }
            catch (Exception ex)
            {
                Assert.Fail("Unit test for duts inheriting from the IDut interface has trown an exception: " + ex.Message);
            }
        }

        [Test]
        public void ResultListenerInheritingFromIResultListener()
        {
            try
            {
                var resultListener = new InterfaceTestResultListenser();
                var setting = new ResultSettings();
                setting.Add(resultListener);
            }
            catch (Exception ex)
            {
                Assert.Fail("Unit test for result listener inheriting from the IResultListener interface has trown an exception: " + ex.Message);
            }
        }

        public void InstrumentInheritingFromIInstrument()
        {
            try
            {
                var instrument = new InterfaceTestInstrument();
                var setting = new InstrumentSettings();
                setting.Add(instrument);
            }
            catch (Exception ex)
            {
                Assert.Fail("Unit test for instruments inheriting from the IInstrument interface has trown an exception: " + ex.Message);
            }
        }
    }

    public class InstrumentTestStep : TestStep
    {
        public IInstrument Instrument { get; set; }
        public override void Run()
        {

        }
    }

    public class InstrumentTest : Instrument
    {
        public InstrumentTest()
        {
            Name = "InstrTest";
        }

        public enum InstrPhase
        {
            Open,
            Close
        }

        public InstrPhase CrashPhase { get; set; }

        public override void Open()
        {
            base.Open();
            tryCrash(InstrPhase.Open);
        }

        public override void Close()
        {
            base.Close();
            tryCrash(InstrPhase.Close);
        }

        void tryCrash(InstrPhase phase)
        {
            if (phase == CrashPhase)
            {
                throw new Exception("Intended Crash: " + phase.ToString());
            }
        }
    }

    [DisplayName("Test\\Crash")]
    public class resultListenerCrash : ResultListener
    {
        public enum ResultPhase
        {
            PlanRunStart,
            PlanRunCompleted,
            StepRunStart,
            StepRunCompleted,
            Result,
            Close,
            Open,
            None
        }

        public bool AbortPlan { get; set; }

        public ResultPhase CrashResultPhase { get; set; }
        public Verdict FinalVerdict { get; set; }
        public resultListenerCrash()
        {
            Name = "CRSH";
            CrashResultPhase = ResultPhase.PlanRunStart;
        }

        public override void Close()
        {
            base.Close();
            if (CrashResultPhase == ResultPhase.Close)
            {
                throw new Exception("Intended Close Crash");
            }
        }

        public override void Open()
        {
            base.Open();
            if (CrashResultPhase == ResultPhase.Open)
            {
                throw new Exception("Intended Open Crash");
            }
        }

        TestPlanRun planrun;
        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            this.planrun = planRun;
            if (CrashResultPhase == ResultPhase.PlanRunStart)
            {
                if (AbortPlan)
                    TapThread.Current.Abort();
                else
                    throw new Exception("Intended");
            }
        }

        public override void OnTestPlanRunCompleted(TestPlanRun planRun, System.IO.Stream logStream)
        {
            FinalVerdict = planrun.Verdict;
            if (CrashResultPhase == ResultPhase.PlanRunCompleted)
            {
                if (AbortPlan)
                    TapThread.Current.Abort();
                else
                    throw new Exception("Intended");
            }
            planrun = null;
        }

        /// <summary>
        /// Called just before a test step is started.
        /// </summary>
        /// <param name="stepRun"></param>
        public override void OnTestStepRunStart(TestStepRun stepRun)
        {
            if (CrashResultPhase == ResultPhase.StepRunStart)
            {
                if (AbortPlan)
                    TapThread.Current.Abort();
                else
                    throw new Exception("Intended");
            }
        }

        public override void OnTestStepRunCompleted(TestStepRun stepRun)
        {
            if (CrashResultPhase == ResultPhase.StepRunCompleted)
            {
                if (AbortPlan)
                    TapThread.Current.Abort();
                else
                    throw new Exception("Intended");
            }
        }

        public override void OnResultPublished(Guid stepRunId, ResultTable result)
        {
            if (CrashResultPhase == ResultPhase.Result)
            {
                if (AbortPlan)
                    TapThread.Current.Abort();
                else
                    throw new Exception("Intended");
            }
        }
    }

    public class InterfaceTestDut : ValidatingObject, IDut
    {
        private bool isConnected = true;
        public bool IsConnected
        {
            get { return isConnected; }
            set
            {
                if (value == IsConnected) return;
                isConnected = value;
                OnPropertyChanged("IsConnected");
            }
        }

        string _shortName = "";
        public string Name
        {
            get
            {
                return _shortName;
            }
            set
            {
                if (_shortName != value)
                {
                    _shortName = value;
                    OnPropertyChanged("Name");
                }
            }
        }

        public event EventHandler<EventArgs> ActivityStateChanged;

        public void Close()
        {

        }

        public void Dispose()
        {

        }

        public void OnActivity()
        {
            if (ActivityStateChanged != null)
            {
                ActivityStateChanged.Invoke(this, new EventArgs());
            }
        }

        public void Open()
        {

        }
    }

    public class InterfaceTestResultListenser : ValidatingObject, IResultListener
    {
        private bool isConnected = true;
        public bool IsConnected
        {
            get { return isConnected; }
            set
            {
                if (value == isConnected) return;
                isConnected = value;
                OnPropertyChanged("IsConnected");
            }
        }

        string _shortName = "InterfaceTestResultListener";
        public string Name
        {
            get
            {
                return _shortName;
            }
            set
            {
                if (_shortName != value)
                {
                    _shortName = value;
                    OnPropertyChanged("Name");
                }
            }
        }
        
        public event EventHandler<EventArgs> ActivityStateChanged;

        public void OnResultPublished(Guid stepRun, ResultTable result)
        {

        }

        public void Close()
        {

        }

        public void Dispose()
        {

        }

        public void OnActivity()
        {
            if (ActivityStateChanged != null)
            {
                ActivityStateChanged.Invoke(this, new EventArgs());
            }
        }

        public void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
        {

        }

        public void OnTestPlanRunStart(TestPlanRun planRun)
        {

        }

        public void OnTestStepRunCompleted(TestStepRun stepRun)
        {

        }

        public void OnTestStepRunStart(TestStepRun stepRun)
        {

        }

        public void Open()
        {

        }
    }

    [DisplayName("InterfaceTestInstrument")]
    public class InterfaceTestInstrument : IInstrument
    {
        private bool isConnected = false;
        public bool IsConnected
        {
            get { return isConnected; }
            set
            {
                if (value == isConnected) return;
                isConnected = value;
                OnPropertyChanged("IsConnected");
            }
        }

        private void OnPropertyChanged(string v)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(v));
            }
        }

        string _shortName = "InterfaceTestInstrument";
        public string Name
        {
            get
            {
                return _shortName;
            }
            set
            {
                if (_shortName != value)
                {
                    _shortName = value;
                    OnPropertyChanged("Name");
                }
            }
        }

        public event EventHandler<EventArgs> ActivityStateChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public void Close()
        {
            IsConnected = false;
        }

        public void Dispose()
        {

        }

        public void OnActivity()
        {
            if (ActivityStateChanged != null)
            {
                ActivityStateChanged.Invoke(this, new EventArgs());
            }
        }

        public void Open()
        {
            IsConnected = true;
        }
    }

    public class QuickTestStep : TestStep
    {
        public InterfaceTestInstrument Instrument { get; set; }

        public override void Run()
        {

        }
    }
}
