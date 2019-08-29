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
using System.ComponentModel;

namespace OpenTap.Plugins.PluginDevelopment.GUI
{
    public enum WaitForInputResult
    {
        // The number assigned, determines the order in which the buttons are shown in the dialog.
        Cancel = 2, Ok = 1
    }

    // This describes a dialog that asks the user to reset the DUT.
    class ResetDutDialog
    {
        // Name is handled specially to create the title of the dialog window.
        public string Name { get { return "DUT Reset"; } }

        [Layout(LayoutMode.FullRow)] // Set the layout of the property to fill the entire row.
        [Browsable(true)] // Show it event though it is read-only.
        public string Message { get { return "Please reset the DUT. Click OK to confirm."; } }

        [Layout(LayoutMode.FloatBottom | LayoutMode.FullRow)] // Show the button selection at the bottom of the window.
        [Submit] // When the button is clicked the result is 'submitted', so the dialog is closed.
        public WaitForInputResult Response { get; set; }
    }

    // This describes a dialog that asks the user to enter the serial number.
    class EnterSNDialog
    {
        // Name is handled specially to create the title of the dialog window.
        public string Name { get { return "Please enter serialnumber"; } }
        
        // Serial number to be entered by the user.
        [Display("Serial Number")]
        public string SerialNumber { get; set; }

    }

    // This example shows how to use UserInput to get user input. 
    // In TAP GUI it appears as a dialog box. In TAP CLI the user is prompted.
    [Display("User Input Example", Groups: new[] { "Examples", "Plugin Development", "GUI" },
        Description: "This example shows how to use UserInput.")]
    public class UserInputExample : TestStep
    {
        [Display("Use Timeout", "Enabling this will make the dialog close after an amount of time.", Group: "Timeout", Order: 1)]
        public bool UseTimeout { get; set; }

        [EnabledIf("UseTimeout", true)]
        [Unit("s")]
        [Display("Timeout", "The dialog closes after this time.", Group: "Timeout", Order: 2)]
        public double Timeout { get; set; }

        public UserInputExample()
        {
            UseTimeout = true;
            Timeout = 5;
            Rules.Add(() => Timeout > 0, "Timeout must > 0s.", "Timeout");
        }

        public override void Run()
        {
            try
            {
                var timeout = UseTimeout ? TimeSpan.FromSeconds(Timeout) : TimeSpan.Zero;

                // Dialog/prompt where user has option to select "OK" or "Cancel".
                // The message/query is set by the Message property. Selection options are defined by WaitForInputResult.
                var dialog = new ResetDutDialog();
                UserInput.Request(dialog, timeout);

                // Response from the user.
                if (dialog.Response == WaitForInputResult.Cancel)
                {
                    Log.Info("User clicked Cancel.");
                    return;
                }
                Log.Info("User clicked OK. Now we prompt for DUT S/N.");

                var snDialog = new EnterSNDialog();
                // Dialog/prmpt where user has option to enter a string as DUT S/N
                UserInput.Request(snDialog, timeout);
                
                Log.Info("DUT S/N: {0}", snDialog.SerialNumber);
            }
            catch (TimeoutException)
            {
                Log.Info("User did not click. Is he sleeping?");
            }
        }
    }

}
