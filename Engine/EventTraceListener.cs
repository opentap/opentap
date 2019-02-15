//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap.Diagnostic;
using System;
using System.Collections.Generic;

namespace OpenTap
{
    /// <summary>
    /// A class that listens to trace messages and raises an event when a message occurs.
    /// </summary>
    public class EventTraceListener : TraceListener
    {
        /// <summary>
        /// Delegate for the log messages.
        /// </summary>
        /// <param name="Events"></param>
        public delegate void LogMessageDelegate(IEnumerable<Event> Events);
        /// <summary>
        /// Event for when messages are logged.
        /// </summary>
        public event LogMessageDelegate MessageLogged;

        /// <summary>
        /// Invokes the MessageLogged event with the new events.
        /// </summary>
        /// <param name="events"></param>
        public override void TraceEvents(IEnumerable<Event> events)
        {
            if (MessageLogged != null)
                MessageLogged(events);
        }

        /// <summary>
        /// Invokes the MessageLogged event with the new event from the legacy TraceEvent system.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="eventType"></param>
        /// <param name="id"></param>
        /// <param name="text"></param>
        public override void TraceEvent(string source, LogEventType eventType, int id, string text)
        {
            Event @event = new Event(0, (int)eventType, text, source, 0);
            using (EventCollection eventCollection = new EventCollection(new Event[] { @event }))
            {
                MessageLogged.Invoke(eventCollection);
            }
        }

        /// <summary>Constructor of the EventTraceListener.</summary>
        public EventTraceListener()
        {

        }
    }
}
