//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using OpenTap;

namespace OpenTap.Plugins.BasicSteps
{
    public enum NotificationAction
    {
        [Display("Play system sound at start and end.")]
        Play_System_Sound_At_Start_and_End,
        [Display("Play custom sounds.", Description: "Play custom sounds on pre-defined stages in the test plan.")]
        Play_Custom_Sound,
        [Display("Run command.", Description: "Run system commands on pre-defined stages in the test plan.")]
        Run_Command
        //SendEmail
    }

    public enum SystemSound
    {
        Asterisk,
        Beep,
        Exclamation,
        Hand,
        Question
    }
    [Display("Notifier", Group: "Action", Description: "Notifies you when an action has completed.")]
    public class NotifyingResultListener : ResultListener
    {
        [Display("Action Type", Group: "General", Order: -1, Description: "The action to take when any of the below events occur.")]
        public NotificationAction Action { get; set; }

        #region Command
        [EnabledIf("Action", NotificationAction.Run_Command, HideIfDisabled = true)]
        [Display("When Starting", Group: "On Test Plan Start", Order: 1)]
        [FilePath(FilePathAttribute.BehaviorChoice.Open, "exe")]
        public string CmdBeforeRun { get; set; }

        [EnabledIf("Action", NotificationAction.Run_Command, HideIfDisabled = true)]
        [Display("When Verdict is \"Not Set\"", Group: "On Test Plan End", Order: 0.5)]
        [FilePath(FilePathAttribute.BehaviorChoice.Open, "exe")]
        public string CmdAfterRunDone { get; set; }

        [EnabledIf("Action", NotificationAction.Run_Command, HideIfDisabled = true)]
        [Display("When Verdict is \"Pass\"", Group: "On Test Plan End", Order: 0.4)]
        [FilePath(FilePathAttribute.BehaviorChoice.Open, "exe")]
        public string CmdAfterRunPass { get; set; }

        [EnabledIf("Action", NotificationAction.Run_Command, HideIfDisabled = true)]
        [Display("When Verdict is \"Fail\"", Group: "On Test Plan End", Order: 0.3)]
        [FilePath(FilePathAttribute.BehaviorChoice.Open, "exe")]
        public string CmdAfterRunFail { get; set; }

        [EnabledIf("Action", NotificationAction.Run_Command, HideIfDisabled = true)]
        [Display("When Verdict is \"Aborted\"", Group: "On Test Plan End", Order: 0.2)]
        [FilePath(FilePathAttribute.BehaviorChoice.Open, "exe")]
        public string CmdAfterRunAborted { get; set; }

        [EnabledIf("Action", NotificationAction.Run_Command, HideIfDisabled = true)]
        [Display("When Verdict is \"Error\"", Group: "On Test Plan End", Order: 0.1)]
        [FilePath(FilePathAttribute.BehaviorChoice.Open, "exe")]
        public string CmdAfterRunError { get; set; }
        #endregion

        private void DoAction(string CmdAction)
        {
            try
            {
                switch (Action)
                {
                    case NotificationAction.Run_Command:
                        if (string.IsNullOrWhiteSpace(CmdAction)) return;
                        Process.Start(CmdAction);
                        break;
                }
            }
            catch (Exception ex) { Log.Error(ex.Message); }
        }

        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            DoAction(CmdBeforeRun);
        }

        public override void OnTestPlanRunCompleted(TestPlanRun planRun, System.IO.Stream logStream)
        {
            switch (planRun.Verdict)
            {
                case Verdict.NotSet:
                    DoAction(CmdAfterRunDone);
                    break;
                case Verdict.Pass:
                    DoAction(CmdAfterRunPass);
                    break;
                case Verdict.Fail:
                    DoAction(CmdAfterRunFail);
                    break;
                case Verdict.Aborted:
                    DoAction(CmdAfterRunAborted);
                    break;
                case Verdict.Error:
                    DoAction(CmdAfterRunError);
                    break;
            }
        }
    }
}
