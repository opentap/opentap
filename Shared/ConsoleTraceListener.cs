//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Text;
using OpenTap.Diagnostic;

namespace OpenTap
{
    internal class ConsoleTraceListener : TraceListener
    {
        private static long globalTimer = DateTime.Now.Ticks;
        private readonly bool isVerbose;
        private readonly bool isQuiet;
        private readonly bool isColor;

        /// <summary>
        /// Waits for the messages to be written to the console.
        /// </summary>
        public override void Flush()
        {
            base.Flush();
            Console.Out.Flush();
        }

        public ConsoleTraceListener(bool isVerbose, bool isQuiet, bool isColor)
        {
            this.isVerbose = isVerbose;
            this.isQuiet = isQuiet;
            this.isColor = isColor;
        }

        public static ConsoleColor GetColorForTraceLevel(LogEventType eventType)
        {
            switch (eventType)
            {
                case LogEventType.Error:
                    return ConsoleColor.Red;
                case LogEventType.Warning:
                    return ConsoleColor.Yellow;
                case LogEventType.Debug:
                    return ConsoleColor.DarkGray;
                default:
                    return ConsoleColor.Gray;
            }
        }

        private void InternalTraceEvent(string source, LogEventType eventType, int id, string message, long timestamp)
        {
            if (eventType == LogEventType.Debug && !isVerbose)
            {
                return;
            }

            if (eventType == LogEventType.Information && isQuiet)
                return;

            ConsoleColor color = isColor ? GetColorForTraceLevel(eventType) : ConsoleColor.Gray;
            string formattedLine = message;
            if (isVerbose)
            {
                var time = new TimeSpan(timestamp - globalTimer);
                if (isColor)
                    formattedLine = String.Format("{2:hh\\:mm\\:ss\\.fff} : {0,-11} : {1}", source, message, time);
                else
                    formattedLine = String.Format("{3:hh\\:mm\\:ss\\.fff} : {0,-13} : {1,-11} : {2}", source, eventType, message, time);
            }
            Console.ForegroundColor = color;
            if (eventType == LogEventType.Error)
                Console.Error.WriteLine(formattedLine);
            else
                Console.WriteLine(formattedLine);
        }

        public override void TraceEvents(IEnumerable<Event> events)
        {
            var fg = Console.ForegroundColor;
            try
            {
                foreach (var evt in events)
                    InternalTraceEvent(evt.Source, (LogEventType)evt.EventType, 0, evt.Message, evt.Timestamp);
            }
            finally
            {
                Console.ForegroundColor = fg;
            }
        }
    }
}
