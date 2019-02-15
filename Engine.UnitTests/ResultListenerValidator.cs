//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap;
using System;
using System.ComponentModel;
using System.Threading;

namespace OpenTap.Engine.UnitTests
{
    [DisplayName("Test\\Validator")]
    [Description("Validates that result listeners behave expectedly.")]
    public class ResultListenerValidator : ResultListener
    {
        #region Private members
        private bool IsOpen = false;

        private bool IsTestPlanRunning = false;
        #endregion
        public Exception Exception;

        public ResultListenerValidator()
        {
            Name = "Validator";
        }

        private void NeedsOpen(string Message)
        {
            if (!IsOpen) throw (Exception = new Exception("Result listener is not opened: " + Message));
        }

        private void EnterLocked(ref SpinLock Lock, string Procedure)
        {
            bool lockTaken = false;
            Lock.TryEnter(0, ref lockTaken);
            if (!lockTaken)
            {
                throw (Exception = new Exception(string.Format("Tried to enter {0} while it is locked by another thread", Procedure)));
            }
        }

        private SpinLock OpenLock = new SpinLock();
        public override void Open()
        {
            EnterLocked(ref OpenLock, "Open");
            try
            {
                if (IsOpen) throw new Exception("Result listener is already open");
                IsOpen = true;
                base.Open();
            }
            finally
            {
                OpenLock.Exit(false);
            }
        }

        private SpinLock CloseLock = new SpinLock();
        public override void Close()
        {
            EnterLocked(ref CloseLock, "Close");
            try
            {
                NeedsOpen("Close");
                IsOpen = false;
                base.Close();
            }
            finally
            {
                CloseLock.Exit(false);
            }
        }

        private SpinLock AddResultLock = new SpinLock();
        public override void OnResultPublished(Guid stepRunId, ResultTable result)
        {
            EnterLocked(ref AddResultLock, "AddResult");
            try
            {
                NeedsOpen("AddResult");
                base.OnResultPublished(stepRunId, result);
            }
            finally
            {
                AddResultLock.Exit(false);
            }
        }

        private SpinLock OnTestPlanRunStartLock = new SpinLock();
        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            EnterLocked(ref OnTestPlanRunStartLock, "OnTestPlanRunStart");
            try
            {
                NeedsOpen("OnTestPlanRunStart");
                IsTestPlanRunning = true;
                base.OnTestPlanRunStart(planRun);
            }
            finally
            {
                OnTestPlanRunStartLock.Exit(false);
            }
        }

        private SpinLock OnTestPlanRunCompletedLock = new SpinLock();
        public override void OnTestPlanRunCompleted(TestPlanRun planRun, System.IO.Stream logStream)
        {
            EnterLocked(ref OnTestPlanRunCompletedLock, "OnTestPlanRunCompleted");
            try
            {
                NeedsOpen("OnTestPlanRunCompleted");
                if (!IsTestPlanRunning) throw new Exception("TestPlan is not running");
                IsTestPlanRunning = false;
                base.OnTestPlanRunCompleted(planRun, logStream);
            }
            finally
            {
                OnTestPlanRunCompletedLock.Exit(false);
            }
        }

        private SpinLock OnTestStepRunStartLock = new SpinLock();
        public override void OnTestStepRunStart(TestStepRun stepRun)
        {
            EnterLocked(ref OnTestStepRunStartLock, "OnTestStepRunStart");
            try
            {
                NeedsOpen("OnTestStepRunStart");
                base.OnTestStepRunStart(stepRun);
            }
            finally
            {
                OnTestStepRunStartLock.Exit(false);
            }
        }

        private SpinLock OnTestStepRunCompletedLock = new SpinLock();
        public override void OnTestStepRunCompleted(TestStepRun stepRun)
        {
            EnterLocked(ref OnTestStepRunCompletedLock, "OnTestStepRunCompleted");
            try
            {
                NeedsOpen("OnTestStepRunCompleted");
                base.OnTestStepRunCompleted(stepRun);
            }
            finally
            {
                OnTestStepRunCompletedLock.Exit(false);
            }
        }
    }
}
