//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace OpenTap.Diagnostic
{
    /// <summary>
    /// Log source interface. Instances of this are always created by a corresponding ILogContext.
    /// </summary>
    [ComVisible(false)]
    public interface ILog
    {
        /// <summary>
        /// Logs an event.
        /// </summary>
        /// <param name="EventType">Event type constant. Typically matches System.Diagnostics.TraceEventType.</param>
        /// <param name="Message">Message for the event.</param>
        void LogEvent(int EventType, string Message);

        /// <summary>
        /// Logs an event.
        /// </summary>
        /// <param name="EventType">Event type constant. Typically matches System.Diagnostics.TraceEventType.</param>
        /// <param name="Message">Message for the event. Formatted with arguments in Args.</param>
        /// <param name="Args">Arguments for String.Format() call.</param>
        void LogEvent(int EventType, string Message, params object[] Args);

        /// <summary>
        /// Logs an event.
        /// </summary>
        /// <param name="EventType">Event type constant. Typically matches System.Diagnostics.TraceEventType.</param>
        /// <param name="DurationNS">Duration in nanoseconds of this event.</param>
        /// <param name="Message">Message for the event.</param>
        void LogEvent(int EventType, long DurationNS, string Message);

        /// <summary>
        /// Logs an event.
        /// </summary>
        /// <param name="EventType">Event type constant. Typically matches System.Diagnostics.TraceEventType.</param>
        /// <param name="DurationNS">Duration in nanoseconds of this event.</param>
        /// <param name="Message">Message for the event. Formatted with arguments in Args.</param>
        /// <param name="Args">Arguments for String.Format() call.</param>
        void LogEvent(int EventType, long DurationNS, string Message, params object[] Args);

        /// <summary>
        /// Identifier name of this source.
        /// </summary>
        string Source { get; }
    }

    /// <summary>
    /// A structure containing all information about an event.
    /// </summary>
    public struct Event
    {
        /// <summary>
        /// Construct new <c ref="Event"/> structure.
        /// </summary>
        /// <param name="duration">The duration of the event in nanoseconds.  </param>
        /// <param name="eventType">The event type this event was logged with. </param>
        /// <param name="message">The message for the event.  </param>
        /// <param name="source">The log source identifier this event was logged from.  </param>
        /// <param name="timestamp">The timestamp for the event in system ticks.  </param>
        public Event(long duration, int eventType, string message, string source, long timestamp)
        {
            DurationNS = duration;
            EventType = eventType;
            Message = message;
            Source = source;
            Timestamp = timestamp;
        }

        /// <summary>
        /// The event type this event was logged with. Typically matches System.Diagnostics.TraceEventType.
        /// </summary>
        public int EventType;
        /// <summary>
        /// The log source identifier this event was logged from.
        /// </summary>
        public string Source;

        /// <summary>
        /// The Timestamp for the event in Ticks.
        /// </summary>
        public long Timestamp;
        /// <summary>
        /// The duration of the event in nanoseconds.
        /// </summary>
        public long DurationNS;

        /// <summary>
        /// The message for the event.
        /// </summary>
        public string Message;
    }
    
    /// <summary>
    /// A collection class that provide posibility to iterate over an array of <see cref="Event">events</see>
    /// </summary>
    internal class EventCollection : IEnumerable<Event>, IDisposable
    {
        #region private fields
        private Event[] events = null;
        #endregion

        #region nested types
        private class EventCollectionEnumerator : IEnumerator<Event>
        {
            #region private fields
            private int index = -1;
            private EventCollection eventCollection = null;
            private bool disposed = false;
            #endregion

            #region properties
            public Event Current
            {
                get
                {
                    VerifyNotDisposed();
                    try
                    {
                        Event element = eventCollection.events[index];
                        return element;
                    }
                    catch (IndexOutOfRangeException e)
                    {
                        throw new InvalidOperationException(e.Message);
                    }
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    VerifyNotDisposed();
                    return Current;
                }
            }
            #endregion

            #region ctor
            public EventCollectionEnumerator(EventCollection eventCollection)
            {
                disposed = false;
                this.eventCollection = eventCollection;
            }
            #endregion
            
            public void Dispose()
            {
                VerifyNotDisposed();
                disposed = true;
            }

            public bool MoveNext()
            {
                VerifyNotDisposed();
                index++;
                return (index < eventCollection.events.Length);
            }

            public void Reset()
            {
                VerifyNotDisposed();
                index = -1;
            }
            private void VerifyNotDisposed()
            {
                if (disposed)
                {
                    throw new ObjectDisposedException("EventCollectionEnumerator");
                }
                else if (eventCollection.Disposed)
                {
                    throw new ObjectDisposedException("EventCollection");
                }
            }
        }
        #endregion

        #region properties

        /// <summary>
        /// Returns a boolean indicating whether this instance has been disposed or not.
        /// </summary>
        public bool Disposed
        {
            get
            {
                VerifyNotDisposed();
                return events == null;
            }
        }
        
        /// <summary>
        /// Gets the number of the elements in the collection
        /// </summary>
        public int Length
        {
            get
            {
                VerifyNotDisposed();

                if (events == null)
                {
                    throw new ObjectDisposedException("EventCollection");
                }

                if (events != null)
                    return events.Length;
                else
                    return 0;
            }
        }
        #endregion

        #region ctor 
        /// <summary>
        /// Creates a new instance of <see cref="EventCollection"/>.
        /// </summary>
        /// <param name="events">The event array that will be wrapped around by this class.</param>
        public EventCollection(Event[] events)
        {
            this.events = events;
        }
        #endregion
        
        /// <summary>
        /// Dispose this instance.
        /// </summary>
        public void Dispose()
        {
            VerifyNotDisposed();
            events = null;
        }
        
        /// <summary>
        /// An enumerator that can be used to enumerate this collection.
        /// </summary>
        /// <returns>An enumerator that can be used to enumerate this collection.</returns>
        public IEnumerator<Event> GetEnumerator()
        {
            VerifyNotDisposed();
            return new EventCollectionEnumerator(this);
        }

        /// <summary>
        /// An enumerator that can be used to enumerate this collection.
        /// </summary>
        /// <returns>An enumerator that can be used to enumerate this collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            VerifyNotDisposed();
            return new EventCollectionEnumerator(this);
        }
        private void VerifyNotDisposed()
        {
            if (events == null)
            {
                throw new ObjectDisposedException("EventCollection");
            }
        }
    }

    /// <summary>
    /// Interface a log listener must implement.
    /// </summary>
    public interface ILogListener
    {
        /// <summary>
        /// Message called when multiple events have been logged.  
        /// </summary>
        /// <param name="Events">Array containing a number of events.</param>
        void EventsLogged(IEnumerable<Event> Events);
        /// <summary>
        /// Called when the log context requests that this listener must flush all of its output resources.  
        /// </summary>
        void Flush();
    }
    
    /// <summary>
    /// The timestamping mechanism used by ILogContext.
    /// </summary>
    public interface ILogTimestampProvider : ITapPlugin
    {
        /// <summary>
        /// Generates a timestamp for the current instant.
        /// </summary>
        long Timestamp();
        /// <summary>
        /// Converts a timestamp generated by the Timestamp method into Ticks.
        /// </summary>
        /// <param name="timestamp"></param>
        long ConvertToTicks(long timestamp);
    }


    /// <summary>
    /// A log context that can have multiple log sources and <see cref="LogResultListener"/>. 
    /// </summary>
    [ComVisible(false)]
    public interface ILogContext
    {
        /// <summary>
        /// Creates a log source with a given source identifier.
        /// </summary>
        /// <param name="Source">The source identifier of this log source.</param>
        ILog CreateLog(string Source);

        /// <summary>
        /// Removes a log source from the context.
        /// </summary>
        /// <param name="LogSource">The given log source.</param>
        void RemoveLog(ILog LogSource);

        /// <summary>
        /// Attaches a log listener.
        /// </summary>
        void AttachListener(ILogListener Listener);

        /// <summary>
        /// Detaches a log listener. Automatically flushes the context.
        /// </summary>
        void DetachListener(ILogListener Listener);

        /// <summary>
        /// Flush all events received at the time instant this method is called, but only waits a number of milliseconds.
        /// </summary>
        /// <param name="TimeoutMS">Max time to wait for messages. If 0 it will wait infinitely.</param>
        /// <returns>True if it waited successfully, or false if a timeout occurred.</returns>
        bool Flush(int TimeoutMS = 0);

        /// <summary>
        /// Flush all events received at the time instant this method is called, but only waits a given duration.
        /// </summary>
        /// <param name="Timeout">Max time to wait for messages, or zero to wait infinitely.</param>
        /// <returns>True if it waited successfully, or false if a timeout occurred.</returns>
        bool Flush(TimeSpan Timeout);

        /// <summary>
        /// Timestamp method to use for all subsequent logged events.
        /// </summary>
        ILogTimestampProvider Timestamper { get; set; }

        /// <summary>
        /// When true, sets the log context to an asynchronous mode (avoiding the potential synchronous mode problem of log sources returning from <see cref="ILog.LogEvent(int, string)"/> calls before the events have been processed). 
        /// When false, log sources always wait until all log listeners have processed the events.  
        /// </summary>
        bool Async { get; set; }

        /// <summary>
        /// Maximum number of outstanding events. Only relevant for <see cref="Async"/> mode.  
        /// </summary>
        int MessageBufferSize { get; set; }
    }
}
