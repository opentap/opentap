using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OpenTap
{
    /// <summary>
    /// Options used to define the behavior of a <see cref="OpenTap.Session"/>
    /// </summary>
    [Flags]
    public enum SessionOptions
    {
        /// <summary>
        /// No special behavior is applied. Starting a session like this, is the same as just starting a TapThread.
        /// </summary>
        None = 0,
        /// <summary>
        /// Component settings are cloned for the sake of this session. Instrument, DUT etc instances are cloned.
        /// When this is used, test plans should be reloaded in the new context. This causes resources to be serialized.
        /// </summary>
        OverlayComponentSettings = 1,
        /// <summary> Log messages written in Sessions that redirect logging only go to LogListeners that are added in that session. </summary>
        RedirectLogging = 2,
        ///// <summary>
        ///// When this option is specified, the thread context will not be a child of the calling context. 
        ///// Instead the session will be the root of a new separate context.
        ///// This will affect the behavior of e.g. <see cref="TapThread.Abort()"/> and <see cref="ThreadHierarchyLocal{T}"/>.
        ///// </summary>
        //ThreadHierarchyRoot = 4,
    }

    internal interface ISessionLocal
    {
        bool AutoDispose { get; }
    }

    /// <summary>
    /// Used to hold a value that is specific to a session.
    /// </summary>
    public class SessionLocal<T> : ISessionLocal
    {
        /// <summary>
        /// Automatically dispose the value when all threads in the session has completed.
        /// Only has any effect if T is IDisposable
        /// </summary>
        public bool AutoDispose { get; }

        /// <summary> Session specific value. </summary>
        public T Value
        {
            get
            {
                for (var session = Session.Current; session != null; session = session.Parent)
                {
                    if (session.sessionLocals.TryGetValue(this, out object val))
                        return (T) val;
                    if (session == session.Parent) throw new InvalidOperationException("This should not be possible");
                }
                return default;
            }
            set => Session.Current.sessionLocals[this] = value;
        }


        /// <summary>
        /// Used to hold a value that is specific to a session.
        /// Initializes a session local with a root/default value.
        /// </summary>
        /// <param name="rootValue">Default value set at the root session.</param>
        /// <param name="autoDispose">True to automatically dispose the value when all threads in the session has completed. Only has any effect if T is IDisposable.</param>
        public SessionLocal(T rootValue, bool autoDispose = true) : this(autoDispose)
        {
            if(Equals(rootValue, default(T)) == false)
                Session.RootSession.sessionLocals[this] = rootValue;
        }

        /// <summary>
        /// Used to hold a value that is specific to a session.
        /// Initializes a session local without a root/default value.
        /// </summary>
        /// <param name="autoDispose">True to automatically dispose the value when all threads in the session has completed. Only has any effect if T is IDisposable.</param>
        public SessionLocal(bool autoDispose = true)
        {
            AutoDispose = autoDispose;
        }
    }

    /// <summary> A session represents a collection of data associated with running and configuring test plans:
    /// - Logging
    /// - Settings
    /// - Resources
    /// - ...
    /// When a new session is created, it overrides the existing values for these items.
    /// </summary>
    public class Session : IDisposable
    {
        static readonly ThreadField<Session> sessionTField = new ThreadField<Session>(ThreadFieldMode.Cached);
        
        /// <summary> The default/root session. This session is active when no other session is. </summary>
        public static readonly Session RootSession;

        TapThread threadContext;
        internal readonly ConcurrentDictionary<ISessionLocal, object> sessionLocals = new ConcurrentDictionary<ISessionLocal, object>();

        /// <summary> The parent session of the current session. This marks the session that started it.</summary>
        public readonly Session Parent;

        static Session()
        {
            // TapThread needs a RootSession to start, so the first time, the TapThread cannot be set.
            RootSession = new Session(Guid.NewGuid(), SessionOptions.None, true);
            RootSession.threadContext = TapThread.Current;
        }
        
        internal void DisposeSessionLocals()
        {
            foreach (var item in sessionLocals)
            {
                if(item.Key.AutoDispose && item.Value is IDisposable disp)
                {
                    disp.Dispose();
                }
            }
            sessionLocals.Clear();
        }

        /// <summary>
        /// Gets the currently active session.
        /// </summary>
        public static Session Current => sessionTField.Value ?? RootSession;

        /// <summary>
        /// Gets the session ID for this session.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Gets the flags used to create/start this session.
        /// </summary>
        public SessionOptions Options { get; }

        Session(Guid id, SessionOptions options, bool rootSession = false)
        {
            Id = id;
            Options = options;
            if (!rootSession)
            {
                threadContext = TapThread.Current;
                Parent = Current;
            }
        }

        static TraceSource _log;
        // lazily loaded to prevent a circular dependency between Session and LogContext.
        static TraceSource log => _log ?? (_log = Log.CreateSource(nameof(Session)));
        
        readonly Stack<IDisposable> disposables = new Stack<IDisposable>();
        
        /// <summary> Disposes the session. </summary>
        public void Dispose()
        {
            var exceptions = new List<Exception>();
            while (disposables.Count > 0)
            {
                try
                {
                    var item = disposables.Pop();
                    item.Dispose();
                }
                catch(Exception e)
                {
                    exceptions.Add(e);
                }
            }
            
            foreach (var ex in exceptions)
            {
                log.Error("Caught error while disposing session: {0}", ex.Message);
                log.Debug(ex);
            }
        }

        /// <summary> Creates a new session in the current <see cref="TapThread"/> context. The session lasts until the TapTread ends, or Dispose is called on the returned Session object.</summary>
        /// <param name="options">Flags selected from the SessionOptions enum to customize the behavior of the session.</param>
        /// <param name="id">Option to specify the ID of the Session</param>
        /// <returns> A disposable Session object. </returns>
        public static Session Create(SessionOptions options = SessionOptions.OverlayComponentSettings | SessionOptions.RedirectLogging, Guid? id = null)
        {
            var session = new Session(id.HasValue ? id.Value : Guid.NewGuid(), options);
            session.disposables.Push(TapThread.UsingThreadContext(session.DisposeSessionLocals));
            session.Activate();
            return session;
        }

        /// <summary> Creates a new session in the current <see cref="TapThread"/> context. The session lasts until the TapTread ends, or Dispose is called on the returned Session object.</summary>
        /// <param name="options">Flags selected from the SessionOptions enum to customize the behavior of the session.</param>
        /// <returns> A disposable Session object. </returns>
        public static Session Create(SessionOptions options) => Create(options, null);


        /// <summary>
        /// Creates a new session, and runs the specified action in the context of that session. When the acion completes, the session is Disposed automatically.
        /// </summary>
        public static void Start(Action action, SessionOptions options = SessionOptions.OverlayComponentSettings | SessionOptions.RedirectLogging)
        {
            var session = new Session(Guid.NewGuid(), options);
            TapThread.Start(() =>
            {
                try
                {
                    session.Activate();
                    sessionTField.Value = session;
                    action();
                }
                finally
                {
                    session.Dispose();
                }
            }, session.DisposeSessionLocals, $"SessionRootThread-{session.Id}");
        }

        /// <summary>
        /// Synchronously runs the specified action in the context of the given session
        /// </summary>
        /// <param name="action">The action to run.</param>
        /// <returns>The session in which the action is run</returns>
        public void RunInSession(Action action)
        {
            TapThread.WithNewContext(action, this.threadContext);
        }

        void Activate()
        {
            try
            {
                sessionTField.Value = this;
                if (Options.HasFlag(SessionOptions.OverlayComponentSettings))
                    ComponentSettings.BeginSession();
                if (Options.HasFlag(SessionOptions.RedirectLogging))
                    Log.WithNewContext();
            }
            catch
            {
                Dispose();
                throw;
            }
        }
    }
}