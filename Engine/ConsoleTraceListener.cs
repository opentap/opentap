//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

        private bool isAnsiColorCodes = false;
        private void EnableAnsiColorCodes()
        {
            if (OperatingSystem.Current == OperatingSystem.Windows)
            {
                try
                {
                    if (Environment.GetEnvironmentVariable("OPENTAP_ANSI_COLORS") != null)
                    {
                        isAnsiColorCodes = true;
                        // This is a "isolated" process that has its stdoout redirected to the parent process. 
                        // The parent process already successfully enabled ansi color codes. No need to do anything else here.
                        return;
                    }
                    isAnsiColorCodes = AnsiColorCodeFix.TryEnableForWin10();
                    if (isAnsiColorCodes)
                        Environment.SetEnvironmentVariable("OPENTAP_ANSI_COLORS", "1");
                }
                catch (Exception ex)
                {
                    InternalTraceEvent("Console", LogEventType.Debug, 0, $"Error while enabling ANSI colors: {ex.Message}", 0);
                }
            }
            else
            {
                isAnsiColorCodes = true;
            }
        }

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
            if (isColor)
                EnableAnsiColorCodes();
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

        internal static string GetAnsiCodeColorForTraceLevel(LogEventType eventType)
        {
            const string AnsiRed = "\x1B[31m";
            const string AnsiYellow = "\x1B[33m";
            const string AnsiBrightBlack = "\x1B[30;1m";
            const string AnsiReset = "\x1B[0m";
            switch (eventType)
            {
                case LogEventType.Error:
                    return AnsiRed;
                case LogEventType.Warning:
                    return AnsiYellow;
                case LogEventType.Debug:
                    return AnsiBrightBlack;
                default:
                    return AnsiReset;
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

            
            string formattedLine = message;
            if (IsVerbose)
            {
                var time = new TimeSpan(timestamp - globalTimer);
                if (IsColor)
                    formattedLine = String.Format("{2:hh\\:mm\\:ss\\.fff} : {0,-11} : {1}", source, message, time);
                else
                    formattedLine = String.Format("{3:hh\\:mm\\:ss\\.fff} : {0,-13} : {1,-11} : {2}", source, eventType, message, time);
            }

            if (IsColor)
            {
                if (isAnsiColorCodes)
                {
                    string colorCode = GetAnsiCodeColorForTraceLevel(eventType);
                    formattedLine = colorCode + formattedLine + GetAnsiCodeColorForTraceLevel(LogEventType.Information);
                }
                else
                {
                    ConsoleColor color = GetColorForTraceLevel(eventType);
                    if (color != currentColor)
                    {
                        currentColor = color;
                        Console.ForegroundColor = color;
                    }
                }
            }

            if (eventType == LogEventType.Error)
                Console.Error.WriteLine(formattedLine);
            else
                Console.WriteLine(formattedLine);
        }

        ConsoleColor currentColor = ConsoleColor.Gray; 

        /// <summary>
        /// Prints all log messages to the console.
        /// </summary>
        /// <param name="events"></param>
        public override void TraceEvents(IEnumerable<Event> events)
        {
            try
            {
                foreach (var evt in events)
                    InternalTraceEvent(evt.Source, (LogEventType)evt.EventType, 0, evt.Message, evt.Timestamp);
            }
            finally
            {
                if (currentColor != ConsoleColor.Gray)
                {
                    currentColor = ConsoleColor.Gray;
                    Console.ForegroundColor = currentColor;
                }
            }
        }
    }
}
