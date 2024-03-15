//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.IO;
using OpenTap.Diagnostic;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace OpenTap
{
    /// <summary>
    /// Identifies the type of event that is logged.
    /// </summary>
    public enum LogEventType
    {
        /// <summary>
        ///     Recoverable error.
        /// </summary>
        Error = 10,
        /// <summary>
        ///     Noncritical problem.
        /// </summary>
        Warning = 20,
        /// <summary>
        ///     Informational message.
        /// </summary>
        Information = 30,
        /// <summary>
        ///     Debugging trace.
        /// </summary>
        Debug = 40
    }

    /// <summary>
    /// Encapsulates the features of the OpenTAP logging infrastructure.
    /// </summary>
    public class TraceSource
    {
        internal ILog log;

        /// <summary> The object that owns this trace source. </summary>
        internal object Owner;
        internal TraceSource(ILog logSource)
        {
            log = logSource;
        }

        LogContext.LogInjector redirectedLog => Log.RedirectedLog;

        /// <summary>
        /// Blocks until all messages posted up to this point have reached all TraceListeners.  
        /// </summary>
        public void Flush()
        {
            Log.Flush();
        }

        /// <summary>
        /// Register a single event.
        /// </summary>
        public void TraceEvent(LogEventType te, int id, string message)
        {
            
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if(redirectedLog != null)
                redirectedLog.LogEvent(log.Source, (int)te, message);
            else
                log.LogEvent((int)te, message);
        }

        /// <summary> Register a single event with formatting and duration. </summary>
        public void TraceEvent(long durationNs, LogEventType te, int id, string message, params object[] args)
        {
            if(redirectedLog != null)
                redirectedLog.LogEvent(log.Source, (int)te, durationNs, message, args);
            else
                log.LogEvent((int)te, durationNs, message, args);
        }
        
        /// <summary> Register a single event without formatting and duration. </summary>
        public void TraceEvent(long durationNs, LogEventType te, int id, string message)
        {
            // this overload is important since otherwise the logging system will use 
            // the overload with [args] even though there are none. And that can give
            // problems if the message itself contains formatting demarcations e.g '{0}'
            if (redirectedLog != null)
                redirectedLog.LogEvent(log.Source, (int)te, durationNs, message);
            else
                log.LogEvent((int)te, durationNs, message);
        }

        /// <summary>
        /// Register a single event with formatting
        /// </summary>
        public void TraceEvent(LogEventType te, int id, string message, params object[] args)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (args == null)
                throw new ArgumentNullException(nameof(args));
            if(redirectedLog != null)
                redirectedLog.LogEvent(log.Source, (int)te, message, args);
            else
                log.LogEvent((int)te, message, args);
        }
    }

    /// <summary>
    /// Base class for various listeners.
    /// </summary>
    public class TraceListener : ILogListener
    {
        void ILogListener.EventsLogged(IEnumerable<Event> events)
        {
            TraceEvents(events);
        }

        /// <summary>
        /// Receives all log messages. The virtual method simply calls <see cref="TraceEvent(string, LogEventType, int, string)"/> directly.  
        /// </summary>
        public virtual void TraceEvents(IEnumerable<Event> events)
        {
            foreach (var evt in events)
                TraceEvent(evt.Source, (LogEventType)evt.EventType, 0, evt.Message);
        }

        /// <summary>
        /// Empty TraceEvent method.
        /// </summary>
        public virtual void TraceEvent(string source, LogEventType eventType, int id, string format)
        {
        }

        /// <summary>
        /// Empty TraceEvent method.
        /// </summary>
        public virtual void TraceEvent(string source, LogEventType eventType, int id, string format, params object[] args)
        {
            TraceEvent(source, eventType, id, string.Format(format, args));
        }

        /// <summary>
        /// Virtual method to match System.Diagnostics.TraceListener. Might be removed.
        /// </summary>
        public virtual void Write(string str)
        {
        }

        /// <summary>
        /// Virtual method to match System.Diagnostics.TraceListener. Might be removed.
        /// </summary>
        public virtual void WriteLine(string str)
        {
        }

        /// <summary>
        /// Waits until all sent log messages have been processed by this and all other TraceListeners.
        /// </summary>
        public virtual void Flush()
        {
            Log.Flush();
        }
    }

    /// <summary>
    /// Simple TraceListener which outputs data to a TextWriter.
    /// </summary>
    class TextWriterTraceListener : TraceListener, IDisposable
    {
        private StreamWriter writer;

        private Mutex LockObject = new Mutex(false);

        protected void LockOutput()
        {
            LockObject.WaitOne();
        }

        protected void UnlockOutput()
        {
            LockObject.ReleaseMutex();
        }

        /// <summary>
        /// The writer that is used as the output.
        /// </summary>
        public StreamWriter Writer
        {
            get => writer;
            set
            {
                if (writer != value)
                {
                    LockOutput();
                    try
                    {
                        writer = value;
                    }
                    finally
                    {
                        UnlockOutput();
                    }
                }
            }
        }

        /// <summary>
        /// Creates a new TextWriterTraceListener writing to the given filename.
        /// </summary>
        public TextWriterTraceListener(string filename)
            : this(new System.IO.FileStream(filename, System.IO.FileMode.Append))
        {
        }

        /// <summary>
        /// Creates a new TextWriterTraceListener writing to the given stream.
        /// </summary>
        public TextWriterTraceListener(Stream stream)
        {
            Writer = new StreamWriter(stream);
        }

        /// <summary>
        /// Writes a string to the current Writer.
        /// </summary>
        public override void Write(string message)
        {
            LockOutput();
            try
            {
                Writer.Write(message);
            }
            finally
            {
                UnlockOutput();
            }
        }

        /// <summary>
        /// Writes a string including a newline to the current Writer.
        /// </summary>
        public override void WriteLine(string message)
        {
            LockOutput();
            try
            {
                Writer.WriteLine(message);
            }
            finally
            {
                UnlockOutput();
            }
        }

        /// <summary>
        /// Flushes the log system and the current Writer.
        /// </summary>
        public override void Flush()
        {
            base.Flush();
            LockOutput();
            try
            {
                if (writer != null)
                    writer.Flush();
            }
            finally
            {
                UnlockOutput();
            }
        }

        /// <summary>
        /// Frees up the writer.
        /// </summary>
        public void Dispose()
        {
            LockOutput();
            try
            {
                if (writer != null)
                {
                    writer.Close();
                    writer = null;
                }
            }
            finally
            {
                UnlockOutput();
            }
        }
    }

    /// <summary>
    /// This class extends System.Diagnostics.Log to provide shorthand methods 
    /// for logging/tracing messages at different levels.
    /// </summary>
    public static class Log
    {
        static readonly LogContext rootLogContext = (LogContext)LogFactory.CreateContext();

        internal static ILogTimestampProvider Timestamper
        {
            get => rootLogContext.Timestamper;
            set => rootLogContext.Timestamper = value;
        }

        static readonly SessionLocal<LogContext.LogInjector> logField = new SessionLocal<LogContext.LogInjector>(null);
        static readonly SessionLocal<LogContext> sessionLogContext = new SessionLocal<LogContext>(rootLogContext);

        internal static void WithNewContext()
        {
            var ctx = new LogContext();
            
            logField.Value = new LogContext.LogInjector(ctx);
            sessionLogContext.Value = ctx;
        }

        internal static LogContext.LogInjector RedirectedLog => logField.Value;

        /// <summary> The current log context. </summary>
        public static ILogContext Context => sessionLogContext.Value;

        /// <summary> Makes a TraceListener start receiving log messages. </summary>
        /// <param name="listener">The TraceListener to add.</param>
        public static void AddListener(ILogListener listener)
        {
            if (listener == null)
                throw new ArgumentNullException(nameof(listener));
            var ctx = Context;
            ctx.Flush();
            ctx.AttachListener(listener);
        }
        
        /// <summary> Stops a specified TraceListener from receiving log messages. </summary>
        /// <param name="listener">The TraceListener to remove.</param>
        public static void RemoveListener(ILogListener listener)
        {
            if (listener == null)
                throw new ArgumentNullException(nameof(listener));
            var ctx = Context;
            listener.Flush();
            ctx.DetachListener(listener);
            listener.Flush();
        }
        /// <summary>
        /// Gets all added TraceListeners.
        /// </summary>
        /// <returns>A readonly collection of TraceListeners.</returns>
        public static ReadOnlyCollection<ILogListener> GetListeners()
        {
            return sessionLogContext.Value?.GetListeners();
        }
        /// <summary> Creates a new log source. </summary>
        /// <param name="name">The name of the Log.</param>
        /// <returns>The created Log.</returns>
        public static TraceSource CreateSource(string name)
        {
            return new TraceSource(rootLogContext.CreateLog(name));
        }

        // ConditionalWeakTable keys does not count as a reference and are automatically removed on GC. This way we avoid leak. CWT's are thread safe.
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, TraceSource> ownedTraceSources = new System.Runtime.CompilerServices.ConditionalWeakTable<object, TraceSource>();
        static readonly object addlock = new object();
        /// <summary> Creates a new owned log source. Note that any given object can only have one owned TraceSource.</summary>
        /// <param name="name">The name of the Log.</param>
        /// <param name="owner">The object owning the log. This is used to enable OpenTAP to emit log messages on behalf of the owner object. </param>
        /// <returns>The created Log.</returns>
        public static TraceSource CreateSource(string name, object owner)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            var source = CreateSource(name);
            source.Owner = owner;
            lock (addlock)
            {
                ownedTraceSources.Remove(owner);
                ownedTraceSources.Add(owner, source); // in this version of .NET there is no Update method...
            }
            return source;
        }
        
        /// <summary> Gets the source of a specified owner. </summary>
        /// <param name="owner"></param>
        /// <returns>returns the TraceSource or null if the owner owns no source.</returns>
        public static TraceSource GetOwnedSource(object owner)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            lock (addlock)
            {
                ownedTraceSources.TryGetValue(owner, out TraceSource source);
                return source;
            }
        }

        /// <summary>
        /// Removes a previously Created Log from the list of sources.
        /// </summary>
        /// <param name="source"></param>
        public static void RemoveSource(TraceSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            rootLogContext.RemoveLog(source.log);
        }

        static Log()
        {
            // This improves performance by disabling a critical region that the .NET framework 
            // would otherwise put around writing to _all_ listeners. With UseGlobalLock set to 
            // false instead, a critical region will only be put around each listener individually 
            // and only if they have IsThreadSafe=false. UseGlobalLock=false is also required to
            // prevent a deadlock when using the Log Breaking feature in the GUI.
            Trace.UseGlobalLock = false;

            rootLogContext.Async = true;
            rootLogContext.MessageBufferSize = 8 * 1024 * 1024;
        }

        // Performance: Reuse the string build each time to avoid generating GC pressure.
        [ThreadStatic]
        static StringBuilder sb;

        /// <summary>
        /// like traceEvent except it uses a stopwatch 'timer' to write formatted time after the message [{1:0}ms].
        /// Usually used to signal in the log how long an operation took.
        /// </summary>
        /// <param name="trace"></param>
        /// <param name="elapsed"></param>
        /// <param name="eventType"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        static void traceEvent(this TraceSource trace, TimeSpan elapsed, LogEventType eventType, string message, params object[] args)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            var timespan = ShortTimeSpan.FromSeconds(elapsed.TotalSeconds);

            if (sb == null)
                sb = new StringBuilder();
            sb.Clear();
            if (args.Length == 0)
            {
                sb.Append(message);
            }
            else
            {
                sb.AppendFormat(message, args);
            }
            sb.Append(" [");
            timespan.ToString(sb);
            sb.Append("]");
            // * 1_000_000 steals about 20 bits of precision
            // so ticks can be about 2^43 before overflowing (signed int)
            // 
            long durationNs = elapsed.Ticks * NanoSecondsPerTick;
            trace.TraceEvent(durationNs, eventType, 0, sb.ToString());
        }

        // this should always be exactly 100.
        const long NanoSecondsPerTick = 1_000_000_000 / TimeSpan.TicksPerSecond;

        /// <summary>
        /// Write a message to the log with a given trace level.
        /// </summary>
        /// <param name="trace"></param>
        /// <param name="eventType"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        static void traceEvent(this TraceSource trace, LogEventType eventType, string message, params object[] args)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (args.Length == 0)
            {
                trace.TraceEvent(eventType, 0, message);    
            }
            else
            {
                trace.TraceEvent(eventType, 0, message, args);
            }
            
        }

        static void exceptionEvent(this TraceSource trace, Exception exception, LogEventType eventType)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));
            WriteException(trace, exception, eventType);
        }

        /// <summary>
        /// Trace a message at level "Information" (<see cref="LogEventType.Information"/>).
        /// </summary>
        /// <param name="trace">this(extension method).</param>
        /// <param name="message">Message to write.</param>
        public static void TraceInformation(this TraceSource trace, string message)
        {
            Info(trace, message);
        }

        /// <summary>
        /// Trace a message at level "Debug" (<see cref="LogEventType.Debug"/>).
        /// </summary>
        /// <param name="trace">this(extension method).</param>
        /// <param name="message">Message to write.</param>
        /// <param name="args">parameters (see <see cref="String.Format(string, object)"/>).</param>
        public static void Debug(this TraceSource trace, string message, params object[] args)
        {
            traceEvent(trace, LogEventType.Debug, message, args);
        }

        /// <summary>
        /// Writes a message with the time measured by timer appended in the format [xx.x (m/u/n)s].
        /// if timer is a TimerToken it will be disposed.
        /// Information level log message.
        /// </summary>
        /// <param name="trace"></param>
        /// <param name="timer"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void Info(this TraceSource trace, Stopwatch timer, string message, params object[] args)
        {
            if (timer == null)
                throw new ArgumentNullException("timer");
            traceEvent(trace, timer.Elapsed, LogEventType.Information, message, args);
        }

        /// <summary>
        /// Writes a message with the time measured by timer appended in the format [xx.x (m/u/n)s].
        /// if timer is a TimerToken it will be disposed.
        /// Debug level end message.
        /// </summary>
        /// <param name="trace"></param>
        /// <param name="timer"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void Debug(this TraceSource trace, Stopwatch timer, string message, params object[] args)
        {
            if (timer == null)
                throw new ArgumentNullException("timer");
            traceEvent(trace, timer.Elapsed, LogEventType.Debug, message, args);
        }
        /// <summary>
        /// Writes a message with the time measured by timer appended in the format [xx.x (m/u/n)s].
        /// if timer is a TimerToken it will be disposed.
        /// Warning level end message.
        /// </summary>
        /// <param name="trace"></param>
        /// <param name="timer"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void Warning(this TraceSource trace, Stopwatch timer, string message, params object[] args)
        {
            if (timer == null)
                throw new ArgumentNullException("timer");
            traceEvent(trace, timer.Elapsed, LogEventType.Warning, message, args);
        }
        /// <summary>
        /// Writes a message with the time measured by timer appended in the format [xx.x (m/u/n)s].
        /// if timer is a TimerToken it will be disposed.
        /// Error level end message.
        /// </summary>
        /// <param name="trace"></param>
        /// <param name="timer"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void Error(this TraceSource trace, Stopwatch timer, string message, params object[] args)
        {
            if (timer == null)
                throw new ArgumentNullException("timer");
            traceEvent(trace, timer.Elapsed, LogEventType.Error, message, args);
        }

        /// <summary>
        /// Writes a message with the time appended in the format [xx.x (m/u/n)s].
        /// if timer is a TimerToken it will be disposed.
        /// Information level log message.
        /// </summary>
        /// <param name="trace"></param>
        /// <param name="elapsed"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void Info(this TraceSource trace, TimeSpan elapsed, string message, params object[] args)
        {
            traceEvent(trace, elapsed, LogEventType.Information, message, args);
        }

        /// <summary>
        /// Writes a message with the time appended in the format [xx.x (m/u/n)s].
        /// if timer is a TimerToken it will be disposed.
        /// Debug level end message.
        /// </summary>
        /// <param name="trace"></param>
        /// <param name="elapsed"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void Debug(this TraceSource trace, TimeSpan elapsed, string message, params object[] args)
        {
            traceEvent(trace, elapsed, LogEventType.Debug, message, args);
        }
        
        /// <summary>
        /// Writes a message with the time appended in the format [xx.x (m/u/n)s].
        /// if timer is a TimerToken it will be disposed.
        /// Warning level end message.
        /// </summary>
        /// <param name="trace"></param>
        /// <param name="elapsed"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void Warning(this TraceSource trace, TimeSpan elapsed, string message, params object[] args)
        {
            traceEvent(trace, elapsed, LogEventType.Warning, message, args);
        }
        /// <summary>
        /// Writes a message with the time appended in the format [xx.x (m/u/n)s].
        /// if timer is a TimerToken it will be disposed.
        /// Error level end message.
        /// </summary>
        /// <param name="trace"></param>
        /// <param name="elapsed"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void Error(this TraceSource trace, TimeSpan elapsed, string message, params object[] args)
        {
            traceEvent(trace, elapsed, LogEventType.Error, message, args);
        }
        /// <summary>
        /// Trace a message at level "Information" (<see cref="LogEventType.Information"/>).
        /// </summary>
        /// <param name="trace">this(extension method).</param>
        /// <param name="message">Message to write.</param>
        /// <param name="args">parameters (see <see cref="String.Format(string, object)"/>).</param>
        public static void Info(this TraceSource trace, string message, params object[] args)
        {
            traceEvent(trace, LogEventType.Information, message, args);
        }

        /// <summary>
        /// Trace a message at level "Warning" (<see cref="LogEventType.Warning"/>).
        /// </summary>
        /// <param name="trace">this(extension method).</param>
        /// <param name="message">Message to write.</param>
        /// <param name="args">parameters (see <see cref="String.Format(string, object)"/>).</param>
        public static void Warning(this TraceSource trace, string message, params object[] args)
        {
            traceEvent(trace, LogEventType.Warning, message, args);
        }

        /// <summary>
        /// Trace a message at level "Error" (<see cref="LogEventType.Error"/>).
        /// </summary>
        /// <param name="trace">this(extension method).</param>
        /// <param name="message">Message to write.</param>
        /// <param name="args">parameters (see <see cref="String.Format(string, object)"/>).</param>
        public static void Error(this TraceSource trace, string message, params object[] args)
        {
            traceEvent(trace, LogEventType.Error, message, args);
        }

        /// <summary>
        /// Write exception details (including stack trace) to the trace at level "Debug" (<see cref="LogEventType.Error"/>). 
        /// </summary>
        /// <param name="trace">this(extension method).</param>
        /// <param name="exception">Inputs error exception.</param>
        public static void Debug(this TraceSource trace, Exception exception)
        {
            exceptionEvent(trace, exception, LogEventType.Debug);
        }

        /// <summary>
        /// Write exception details (including stack trace) to the trace at level "Error" (<see cref="LogEventType.Error"/>). 
        /// </summary>
        /// <param name="trace">this(extension method).</param>
        /// <param name="exception">Inputs error exception.</param>
        public static void Error(this TraceSource trace, Exception exception)
        {
            exceptionEvent(trace, exception, LogEventType.Error);
        }

        const int callerStackTraceLimit = 4;
        static void WriteStackTrace(TraceSource trace, StackTrace stack, LogEventType level)
        {
            var frames = stack.GetFrames() ?? Array.Empty<StackFrame>(); 
            var lines = frames
                .Skip(3) // Skip the frames from the logging itself.
                .Where(line => line.HasMethod()); // only take those with the method available.
            int lineCount = lines.Count();
            
            lines = lines.Take(callerStackTraceLimit); // Just show the top of the call stack.
            
            foreach (StackFrame line in lines)
            {
                var fixedLine =  $"at {line.GetMethod()}";
                if (line.HasSource())
                {
                    fixedLine += $" in {line.GetFileName()}:line {line.GetFileLineNumber()}";
                }
                trace.TraceEvent(level, 2, "    " + fixedLine, false);
            }

            if (lineCount > callerStackTraceLimit)
            {
                trace.TraceEvent(level, 2, "    ...");
            }
        }

        static void WriteException(TraceSource trace, Exception exception, LogEventType level, bool appendStack = true, bool isInner = false)
        {
            var exceptionName = exception.GetType().Name;
            try
            {
                var exceptionMessage = exception.Message
                    .Replace("{", "{{")
                    .Replace("}", "}}");
                if (isInner)
                    trace.TraceEvent(level, 2, "  Inner " + exceptionName + ": " + exceptionMessage, false);
                else
                    trace.TraceEvent(level, 2, exceptionName + ": " + exceptionMessage);
                if (exception.StackTrace != null)
                {
                    string[] lines = exception.StackTrace.Split(new char[] {'\n'});
                    foreach (string line in lines)
                    {
                        var fixedLine = line.Replace("{", "{{").Replace("}", "}}").Trim();
                        trace.TraceEvent(LogEventType.Debug, 2, "    " + fixedLine, false);
                    }
                }

                if (exception is ReflectionTypeLoadException)
                {
                    ReflectionTypeLoadException reflectionTypeLoadException = (ReflectionTypeLoadException) exception;
                    foreach (Exception ex in reflectionTypeLoadException.LoaderExceptions)
                    {
                        WriteException(trace, ex, level, false);
                    }
                }
                else if (exception is AggregateException ag)
                {
                    foreach (var inner in ag.InnerExceptions)
                    {
                        WriteException(trace, inner, level, false, true);
                    }
                }
                else if (exception.InnerException is Exception inner)
                {
                    WriteException(trace, inner, level, false, true);
                }
            }
            catch (Exception)
            {
                trace.TraceEvent(level, 2, "Error caught while logging an exception.");
            }

            if (appendStack)
            {
                try
                {
                    var stackTrace = new StackTrace(true);
                    trace.TraceEvent(level, 2, "Exception caught at:");
                    WriteStackTrace(trace, stackTrace, level);
                }
                catch
                {
                    trace.TraceEvent(level, 2, "Error retrieving current stacktrace.");
                }
            }
        }

        /// <summary>
        /// Flushes all waiting log trace events.
        /// </summary>
        public static void Flush()
        {
            rootLogContext.Flush();
        }

        /// <summary>
        /// Puts the current log context into synchronous mode.
        /// All TraceSources will now wait for their trace events to be handled by all TraceListeners before returning.
        /// </summary>
        public static void StartSync()
        {
            Flush();
            rootLogContext.Async = false;
        }

        /// <summary>
        /// Ends synchronous mode. Must be called after <c ref="StartSync"/>.
        /// </summary>
        public static void StopSync()
        {
            rootLogContext.Async = true;
            Flush();
        }
    }

    /// <summary>
    /// Extension methods for Exception.
    /// </summary>
    static class ExceptionExtensions
    {
        /// <summary>
        /// Finds the message of the inner most exception for this exception. If no inner exceptions are set, the message of this exception is returned.
        /// COMExceptions are ignored as their message is not very useful.
        /// </summary>
        public static string GetInnerMostExceptionMessage(this Exception ex)
        {
            if (ex is AggregateException ag)
            {
                if(ag.InnerExceptions.Count == 1)
                    return ag.InnerExceptions[0].GetInnerMostExceptionMessage();
                return ag.Message;
            }
            if (ex is System.Runtime.InteropServices.COMException)
                return ex.Message;
            return ex.InnerException?.GetInnerMostExceptionMessage() ?? ex.Message;
        }

        public static void Rethrow(this Exception ex)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
        }

        public static void RethrowInner(this Exception ex)
        {
            if(ex is AggregateException a && a.InnerExceptions.Count > 1)
                ex.Rethrow();
            else
                (ex.InnerException ?? ex).Rethrow();
        }
    }
}
