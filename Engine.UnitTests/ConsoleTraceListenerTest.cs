//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.IO;
using NUnit.Framework;
using OpenTap.Diagnostic;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    [NonParallelizable]
    public class ConsoleTraceListenerTest
    {
        static Event MakeEvent(LogEventType eventType, string message) =>
            new Event(0, (int)eventType, message, "TestSource", DateTime.Now.Ticks);

        static (string stdout, string stderr) TraceEvents(bool quiet, params Event[] events)
        {
            var prevOut = Console.Out;
            var prevErr = Console.Error;
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            try
            {
                Console.SetOut(stdout);
                Console.SetError(stderr);
                var listener = new ConsoleTraceListener(isVerbose: false, isQuiet: quiet, isColor: false);
                listener.TraceEvents(events);
            }
            finally
            {
                Console.SetOut(prevOut);
                Console.SetError(prevErr);
            }
            return (stdout.ToString(), stderr.ToString());
        }

        [Test]
        public void WarningsAndErrorsGoToStderr()
        {
            var (stdout, stderr) = TraceEvents(quiet: false,
                MakeEvent(LogEventType.Information, "info message"),
                MakeEvent(LogEventType.Warning, "warning message"),
                MakeEvent(LogEventType.Error, "error message"));

            // Information goes to stdout.
            Assert.That(stdout, Does.Contain("info message"));

            // Warnings and errors go to stderr, and only to stderr.
            Assert.That(stderr, Does.Contain("warning message"));
            Assert.That(stderr, Does.Contain("error message"));
            Assert.That(stdout, Does.Not.Contain("warning message"));
            Assert.That(stdout, Does.Not.Contain("error message"));
            Assert.That(stderr, Does.Not.Contain("info message"));
        }

        [Test]
        public void QuietWritesNothingToStdout()
        {
            var (stdout, stderr) = TraceEvents(quiet: true,
                MakeEvent(LogEventType.Debug, "debug message"),
                MakeEvent(LogEventType.Information, "info message"),
                MakeEvent(LogEventType.Warning, "warning message"),
                MakeEvent(LogEventType.Error, "error message"));

            // With --quiet there should be NO messages other than those on stderr.
            Assert.That(stdout, Is.Empty);

            // Warnings and errors are still shown, on stderr.
            Assert.That(stderr, Does.Contain("warning message"));
            Assert.That(stderr, Does.Contain("error message"));
            Assert.That(stderr, Does.Not.Contain("debug message"));
            Assert.That(stderr, Does.Not.Contain("info message"));
        }
    }
}
