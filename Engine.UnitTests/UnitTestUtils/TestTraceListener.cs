//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using OpenTap;

namespace OpenTap.EngineUnitTestUtils
{
    /// <summary>
    /// Trace listener used when running unittests, it checks for error messages in the trace
    /// </summary>
    public class TestTraceListener : OpenTap.TraceListener
    {
        public void ClearErrors()
        {
            ErrorMessage = new List<string>();
            WarningMessage = new List<string>();
        }

        public void AssertErrors(IList<string> allowedMessages)
        {
            foreach (string error in ErrorMessage)
            {
                if (allowedMessages == null || !allowedMessages.Contains(error))
                {
                    Assert.Fail("{0}{1}{1}{2}", ErrorMessage, Environment.NewLine, GetLog());
                }
            }
            foreach (string warning in WarningMessage)
            {
                if (allowedMessages == null || !allowedMessages.Contains(warning))
                {
                    Assert.Inconclusive("{0}{1}{1}{2}", WarningMessage, Environment.NewLine, GetLog());
                }
            }
        }

        public void AssertErrors(List<Regex> allowedMessages)
        {
            foreach (string error in ErrorMessage)
            {
                if (allowedMessages == null || allowedMessages.All(r => !r.IsMatch(error)))
                {
                    Assert.Fail("{0}{1}{1}{2}", ErrorMessage, Environment.NewLine, GetLog());
                }
            }
            foreach (string warning in WarningMessage)
            {
                if (allowedMessages == null || allowedMessages.All(r => !r.IsMatch(warning)))
                {
                    Assert.Inconclusive("{0}{1}{1}{2}", WarningMessage, Environment.NewLine, GetLog());
                }
            }
        }

        public void AssertErrors()
        {
            Flush();
            Assert.IsTrue(ErrorMessage.Count == 0, "{0}{1}{1}{2}", string.Join(Environment.NewLine, ErrorMessage), Environment.NewLine, GetLog());
            if (WarningMessage.Count != 0)
                Assert.Inconclusive("{0}{1}{1}{2}", string.Join(Environment.NewLine, WarningMessage), Environment.NewLine, GetLog());
        }

        public string GetLog()
        {
            return allLog.ToString();
        }

        public StringBuilder allLog = new StringBuilder(1000);
        private Stopwatch globalTimer = Stopwatch.StartNew();
        public List<string> ErrorMessage = new List<string>();
        public List<string> WarningMessage = new List<string>();
        public override void TraceEvent(string source, LogEventType eventType, int id, string message)
        {
            base.TraceEvent(source, eventType, id, message);
            allLog.AppendLine(String.Format("{3:ss\\.fff} : {0,-11} : {1,-13} : {2}", source, eventType, message, globalTimer.Elapsed));
            if (eventType == LogEventType.Error)
                ErrorMessage.Add(message);
            if (eventType == LogEventType.Warning)
                WarningMessage.Add(message);
        }

        public override void TraceEvent(string source, LogEventType eventType, int id, string format, params object[] args)
        {
            if (args == null)
            {
                TraceEvent(source, eventType, id, format);
                return;
            }
            base.TraceEvent(source, eventType, id, format, args);
            allLog.AppendLine(String.Format("{3:ss\\.fff} : {0,-11} : {1,-13} : {2}", source, eventType, String.Format(format, args), globalTimer.Elapsed));
            if (eventType == LogEventType.Error)
                ErrorMessage.Add(String.Format(format, args));
            if (eventType == LogEventType.Warning)
                WarningMessage.Add(String.Format(format, args));
        }

        public override void Write(string message)
        {
        }

        public override void WriteLine(string message)
        {
        }
    }
}
