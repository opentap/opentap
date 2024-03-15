//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;

namespace OpenTap.Diagnostic
{
    /// <summary>
    /// Default timestamper.
    /// </summary>
    [Display("Local and Accurate", "Delivers timestamping that will be accurate to the system time over longer durations.")]
    public class AccurateStamper : ILogTimestampProvider
    {
        /// <summary> Prints a friendly name. </summary>
        /// <returns></returns>
        public override string ToString() => "Local and Accurate";
        long ILogTimestampProvider.Timestamp()
        {
            return DateTime.UtcNow.Ticks;
        }

        Stopwatch sw = Stopwatch.StartNew();
        long UtcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).Ticks;
        const long ticksPerSecond = 10000000; // TimeSpan.FromSeconds(1).Ticks;
        long ILogTimestampProvider.ConvertToTicks(long timestamp)
        {
            if (sw.ElapsedTicks > ticksPerSecond)
            {
                UtcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).Ticks;
                sw.Restart();
            }

            return (timestamp + UtcOffset);
        }
    }

    internal class LogContext : ILogContext, ILogContext2, IDisposable
    {
        
        readonly LogQueue LogQueue = new LogQueue();

        public bool HasListeners => listeners.Count > 0;

        readonly List<ILogListener> listeners = new List<ILogListener>();

        long processedMessages;
        long OutstandingMessages =>  LogQueue.PostedMessages - processedMessages;
        
        ILogTimestampProvider timeStamper = new AccurateStamper();
        
        readonly AutoResetEvent flushBarrier = new AutoResetEvent(false);

        readonly Thread processor;
        public LogContext(bool startProcessor = true)
        {
            if (startProcessor)
            {
                processor = new Thread(ProcessLog) { IsBackground = true, Name = "Log processing" };
                processor.Start();
            }
        }

        static LogContext EmptyLogContext()
        {
            return new LogContext(false);
        }

        readonly AutoResetEvent newEventOccured = new AutoResetEvent(false);

        void ProcessLog()
        {
            var copy = new List<ILogListener>();
            Event[] bunch = Array.Empty<Event>();
            while (isDisposed == false)
            {
                newEventOccured.WaitOne();
                flushBarrier.WaitOne(100); // let things queue up unless flush is called.
                int count = LogQueue.DequeueBunch(ref bunch);

                if (count > 0)
                {
                    lock (listeners)
                    {
                        copy.Clear();
                        foreach (var thing in listeners)
                            copy.Add(thing);
                    }

                    if (copy.Count > 0)
                    {
                        if (timeStamper != null)
                            for (int i = 0; i < bunch.Length; i++)
                                bunch[i].Timestamp = timeStamper.ConvertToTicks(bunch[i].Timestamp);


                        foreach (var listener in copy)
                        {
                            try
                            {
                                using (var events = new EventCollection(bunch))
                                {
                                    listener.EventsLogged(events);
                                }
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                    }

                    processedMessages += bunch.LongLength;    
                }
                
            }
        }

        public ILog CreateLog(string source)
        {
            return new Log(this, source);
        }

        public void RemoveLog(ILog logSource)
        {
            if (logSource is Log log)
                log.Context = EmptyLogContext();
        }

        public void AttachListener(ILogListener listener)
        {
            lock (listeners)
                listeners.Add(listener);
        }

        public void DetachListener(ILogListener listener)
        {
            lock (listeners)
                listeners.Remove(listener);
        }

        public ReadOnlyCollection<ILogListener> GetListeners()
        {
            return new ReadOnlyCollection<ILogListener>(listeners);
        }

        public bool Flush(int timeoutMs = 0)
        {
            if (isDisposed) return true;
            long posted = LogQueue.PostedMessages;

            flushBarrier.Set();
            newEventOccured.Set();

            if (timeoutMs == 0)
            {
                while (((processedMessages - posted)) < 0)
                {
                    Thread.Yield();
                    newEventOccured.Set();
                    flushBarrier.Set();
                }
                return true;
            }
            
            {
                var sw = Stopwatch.StartNew();

                while ((processedMessages - posted) < 0 && sw.ElapsedMilliseconds < timeoutMs)
                {
                    Thread.Yield();
                    newEventOccured.Set();
                    flushBarrier.Set();
                }

                return (processedMessages - posted) < 0;
            }
        }

        public bool Flush(TimeSpan timeout)
        {
            return Flush((int)timeout.TotalMilliseconds);
        }

        bool isDisposed;
        public void Dispose()
        {
            Flush();
            isDisposed = true;
            newEventOccured.Set();
            flushBarrier.Set();
        }

        public bool Async { get; set; }

        public int MessageBufferSize { get; set; }

        public ILogTimestampProvider Timestamper { get { return timeStamper; } set { timeStamper = value; } }


        void injectEvent(Event @event)
        {
            lock (listeners)
            {
                using (EventCollection eventCollection = new EventCollection(new [] { @event }))
                {
                    listeners.ForEach(l => l.EventsLogged(eventCollection));
                }
            }
        }
        
        public void AddEvent(Event evt)
        {
            if (Async)
            {
                int msgCnt = MessageBufferSize;
                if (msgCnt > 0)
                {
                    while (OutstandingMessages > msgCnt)
                        Thread.Sleep(1);
                }
                LogQueue.Enqueue(evt);
            }
            else
            {
                injectEvent(evt);
            }
            this.newEventOccured.Set();
        }

        internal class LogInjector
        {
            internal LogContext Context;
            public LogInjector(LogContext context) => Context = context;

            public void logEvent(Event evt) => Context.AddEvent(evt);
            
            public void LogEvent(string source, int eventType, string message)
            {
                if (Context.HasListeners)
                {
                    long timestamp = getTimestamp();
                    var evt = new Event(0, eventType, message, source, timestamp);
                    logEvent(evt);
                }
            }

            public void LogEvent(string source, int eventType, string message, params object[] args)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));
                if (args == null)
                    throw new ArgumentNullException(nameof(args));
                if (Context.HasListeners)
                {
                    var messageFmt = string.Format(message, args);
                    LogEvent(source, eventType, messageFmt);
                }
            }

            long getTimestamp()
            {
                long timestamp = 0;
                var timestamper = Context.Timestamper;
                if (timestamper != null)
                    timestamp = timestamper.Timestamp();
                return timestamp;
            }

            public void LogEvent(string source, int eventType, long durationNs, string message)
            {
                if (Context.HasListeners)
                {
                    long timestamp = getTimestamp();
                    var evt = new Event(durationNs, eventType, message, source, timestamp);
                    logEvent(evt);
                }
            }

            public void LogEvent(string source, int eventType, long durationNs, string message, params object[] args)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));
                if (args == null)
                    throw new ArgumentNullException(nameof(args));
                if (source == null)
                    throw new ArgumentNullException(nameof(source));
                if (Context.HasListeners)
                {
                    var messageFmt = string.Format(message, args);
                    LogEvent(source, eventType, durationNs, messageFmt);
                }
            }
        }
        
        internal class Log : LogInjector, ILog
        {
            private readonly string _source;

            public Log(LogContext context, string source) : base(context)
            {
                _source = source;
            }

            public void LogEvent(int eventType, string message)
            {
                LogEvent(_source, eventType, message);
            }

            public void LogEvent(int eventType, string message, params object[] args)
            {
                LogEvent(_source, eventType, message, args);
            }

            public void LogEvent(int eventType, long durationNs, string message)
            {
                LogEvent(_source, eventType, durationNs, message);
            }

            public void LogEvent(int eventType, long durationNs, string message, params object[] args)
            {
                LogEvent(_source, eventType, durationNs, message, args);
            }

            string ILog.Source =>  _source;
        }

        public long GetProcessedMessages() => processedMessages;
    }
}
