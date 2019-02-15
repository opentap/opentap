//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap;
using System;
using System.Diagnostics;

namespace OpenTap.EngineUnitTestUtils
{
    /// <summary>
    /// Prints out data when a trace event occurs. This is usefull for showing messages in VisualStudio.
    /// </summary>
    public class DiagnosticTraceListener : OpenTap.TraceListener
    {
        private readonly Stopwatch globalTimer = Stopwatch.StartNew();
        public override void Write(string message)
        {

        }

        public override void WriteLine(string message)
        {

        }

        public override void TraceEvent(string source, LogEventType eventType, int id, string message)
        {
            Debug.WriteLine(String.Format("{3:ss\\.fff} : {0,-11} : {1,-13} : {2}", source, eventType, message, globalTimer.Elapsed));
            base.TraceEvent(source, eventType, id, message);
        }

        public override void TraceEvent(string source, LogEventType eventType, int id, string format, params object[] args)
        {
            Debug.WriteLine(String.Format("{3:ss\\.fff} : {0,-11} : {1,-13} : {2}", source, eventType, String.Format(format, args), globalTimer.Elapsed));
            base.TraceEvent(source, eventType, id, format, args);
        }
    }
}
