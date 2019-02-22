//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap;
using System;
using System.ComponentModel;

namespace OpenTap.Plugins.BasicSteps
{
    public enum InputButtons
    {
        [Display("Ok / Cancel", "Shows the OK and Cancel buttons")]
        OkCancel,
        [Display("Yes / No", "Shows the Yes and No buttons")]
        YesNo
    }

    public enum WaitForInputResult1
    {
        // The order of the results determines the order in which the buttons is shown in the dialog box.
        // The number assigned, determines the default value.
        No = 2, Yes = 1
    }

    public enum WaitForInputResult2
    {
        // The order of the results determines the order in which the buttons is shown in the dialog box.
        // The number assigned, determines the default value.
        Cancel = 2, Ok = 1
    }

    class DialogRequest
    {
        public DialogRequest(string Title, string Message)
        {
            this.Name = Title;
            this.Message = Message;
        }
        [Browsable(false)]
        public string Name { get; set; }

        [Layout(LayoutMode.FullRow, rowHeight: 5)]
        [Browsable(true)]
        public string Message { get; private set; }

        [Browsable(false)]
        public InputButtons Buttons { get; set; }

        [Layout(LayoutMode.FloatBottom | LayoutMode.FullRow)]
        [EnabledIf("Buttons", InputButtons.YesNo, HideIfDisabled = true)]
        [Submit]
        public WaitForInputResult1 Input1 { get; set; }

        [Layout(LayoutMode.FloatBottom | LayoutMode.FullRow)]
        [EnabledIf("Buttons", InputButtons.OkCancel, HideIfDisabled = true)]
        [Submit]
        public WaitForInputResult2 Input2 { get; set; }
    }

    [Display("Dialog", Group: "Basic Steps", Description: "Used to interact with the user.")]
    public class DialogStep : TestStep
    {
        [Display("Message", Description: "The message shown to the user.", Order: 0.1)]
        [Layout(LayoutMode.Normal, 2)]
        public string Message { get; set; }
        
        [Display("Title", "The title of the dialog box.", Order: 0)]
        public string Title { get; set; }
        
        [Display("Buttons", "Selects what buttons the user gets presented with.", Order: 0.2)]
        public InputButtons Buttons { get; set; }
        
        [Display("If Yes/OK", "This is the verdict presented when the user clicks \"Yes\" or \"OK\".", Group: "Verdict", Order: 0.3, Collapsed: true)]
        public Verdict PositiveAnswer { get; set; }

        [EnabledIf("Buttons", InputButtons.OkCancel, InputButtons.YesNo)]
        [Display("If No/Cancel", "This is the verdict presented when the user clicks \"No\" or \"Cancel\".", Group: "Verdict", Order: 0.4, Collapsed: true)]
        public Verdict NegativeAnswer { get; set; }
        
        [Display("Use Timeout", "Enabling this will make the dialog close as if No/Cancel was clicked after an amount of time.", Group: "Timeout", Order: 1, Collapsed: true)]
        public bool UseTimeout { get; set; }

        double timeout = 5;
        [EnabledIf("UseTimeout", true)]
        [Unit("s")]
        [Display("Timeout", "After this time the dialog will return the default answer.", Group: "Timeout", Order: 2, Collapsed: true)]
        public double Timeout
        {
            get { return timeout; }
            set
            {
                if (value >= 0)
                    timeout = value;
                else throw new Exception("Timeout must be greater than 0 seconds.");
                
            }
        }

        [EnabledIf("UseTimeout", true)]
        [Display("Default Positive", "Is the default answer(on timeout) positive?", Group: "Timeout", Order: 2, Collapsed: true)]
        public Verdict DefaultAnswer { get; set; }

        public DialogStep()
        {
            Message = "Message";
            Title = "Title";

            Rules.Add(() => !string.IsNullOrWhiteSpace(Title), "Title is empty", Title);
            Rules.Add(() => !string.IsNullOrWhiteSpace(Message), "Message is empty", Message);
        }

        public override void Run()
        {
            Verdict answer = DefaultAnswer;
            var req = new DialogRequest(Title, Message) { Buttons = Buttons };
            try
            {
                var timeout = TimeSpan.FromSeconds(Timeout);
                if (timeout == TimeSpan.Zero)
                    timeout = TimeSpan.FromSeconds(0.001);
                if (UseTimeout == false)
                    timeout = TimeSpan.MaxValue;
                UserInput.Request(req, timeout, false);

                if (Buttons == InputButtons.OkCancel)
                {
                    answer = req.Input2 == WaitForInputResult2.Ok ? PositiveAnswer : NegativeAnswer;
                }
                else
                {
                    answer = req.Input1 == WaitForInputResult1.Yes ? PositiveAnswer : NegativeAnswer;
                }
            }
            catch (TimeoutException)
            {

            }

            Verdict = answer;
        }
    }
}
