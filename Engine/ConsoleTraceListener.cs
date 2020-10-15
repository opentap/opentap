//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using OpenTap.Diagnostic;

namespace OpenTap
{
    /// <summary>
    /// A class that prints trace messages to the console.
    /// </summary>
    public class ConsoleTraceListener : TraceListener
    {
        /// <summary>
        /// Set logs startup time, this will affect timestamp of all log messages.
        /// </summary>
        /// <param name="time"></param>
        public static void SetStartupTime(DateTime time){
            globalTimer = time.Ticks;
        }
        private static long globalTimer = DateTime.Now.Ticks;
        
        /// <summary>
        /// Show verbose/debug level log messages.
        /// </summary>
        public bool IsVerbose { get; set; }
        
        /// <summary>
        /// Hide debug and information level log messages.
        /// </summary>
        public bool IsQuiet { get; set; }
        
        /// <summary>
        /// Color messages according to their level.
        /// </summary>
        public bool IsColor { get; set; }

        /// <summary>
        /// Waits for the messages to be written to the console.
        /// </summary>
        public override void Flush()
        {
            base.Flush();
            Console.Out.Flush();
        }

        /// <summary>
        /// Creates an instance of the ConsoleTraceListener that can be used to output log messages in consoles.
        /// </summary>
        /// <param name="isVerbose"></param>
        /// <param name="isQuiet"></param>
        /// <param name="isColor"></param>
        public ConsoleTraceListener(bool isVerbose, bool isQuiet, bool isColor)
        {
            this.IsVerbose = isVerbose;
            this.IsQuiet = isQuiet;
            this.IsColor = isColor;
        }

        internal static ConsoleColor GetColorForTraceLevel(LogEventType eventType)
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
            if (eventType == LogEventType.Debug && !IsVerbose)
            {
                return;
            }

            if (eventType == LogEventType.Information && IsQuiet)
                return;

            ConsoleColor color = IsColor ? GetColorForTraceLevel(eventType) : ConsoleColor.Gray;
            string formattedLine = message;
            if (IsVerbose)
            {
                var time = new TimeSpan(timestamp - globalTimer);
                if (IsColor)
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

        /// <summary>
        /// Prints all log messages to the console.
        /// </summary>
        /// <param name="events"></param>
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
