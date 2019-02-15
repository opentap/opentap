//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.ComponentModel;
using OpenTap;

namespace OpenTap.Plugins.BasicSteps
{
    public enum LogSeverity
    {
        Debug,
        [Display("Information")]
        Info,
        Warning,
        Error
    }

    [Display("Log Output", Group:"Basic Steps", Description: "Outputs a specified message to the log with a specified severity.")]
    public class LogStep : TestStep
    {
        [Display("Log Message", Description: "The log message to be output.", Order: -1)]
        public string LogMessage { get; set; }

        [Display("Log Severity", Description: "What log level the message will be written at.")]
        public LogSeverity Severity { get; set; }

        public LogStep()
        {
            Severity = LogSeverity.Info;
            LogMessage = "";
        }

        public override void Run()
        {
            switch (Severity)
            {
                case LogSeverity.Debug:
                    Log.Debug(LogMessage);
                    break;
                case LogSeverity.Info:
                    Log.Info(LogMessage);
                    break;
                case LogSeverity.Warning:
                    Log.Warning(LogMessage);
                    break;
                case LogSeverity.Error:
                    Log.Error(LogMessage);
                    break;
            }
        }
    }
}
