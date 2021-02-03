using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

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
        /// When this is used, test plans should be reloaded in the new context.  
        /// </summary>
        OverlayComponentSettings = 1,
        /// <summary> Redirect logging. When this flag is enabled, existing log listeners can be overwritten with new ones. </summary>
        RedirectLogging = 2,
        /// <summary>
        /// When this option is specified, the thread context will not be a child of the calling context. 
        /// Instead the session will be the root of a new separate context.
        /// This will affect the behavior of e.g. <see cref="TapThread.Abort()"/> and <see cref="ThreadHierarchyLocal{T}"/>.
        /// </summary>
        ThreadHeirarchyRoot = 4,
    }

    internal interface ISessionStatic
    {
        bool AutoDispose { get; set; }
    }

    /// <summary>
    /// Used to hold a value that is specific to a session.
    /// </summary>
    public class SessionStatic<T> : ISessionStatic
    {
        /// <summary>
        /// Automatically dispose the value when all threads in the session has completed.
        /// Only has any effect if T is IDisposable
        /// </summary>
        public bool AutoDispose { get; set; }

        /// <summary>
        /// Session specifc value.
        /// </summary>
        public T Value
        {
            get
            {
                if (Session.Current.StaticVars.TryGetValue(this, out object val))
                    return (T)val;
                return defaultValue;
            }
            set => Session.Current.StaticVars[this] = value;
        }

        readonly T defaultValue;

        /// <summary>
        /// Used to hold a value that is specific to a session.
        /// </summary>
        /// <param name="defaultValue">Default value used if there is no session, or if the value has not been set for the session.</param>
        public SessionStatic(T defaultValue)
        {
            this.defaultValue = defaultValue;
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
        static ThreadField<Session> Sessions = new ThreadField<Session>(ThreadFieldMode.Cached);
        static Dictionary<Guid, Session> RunningSessions = new Dictionary<Guid, Session>();
        internal static readonly Session RootSession = new Session(SessionOptions.None);

        readonly TapThread ThreadContext = TapThread.Current;
        internal Dictionary<ISessionStatic, object> StaticVars = new Dictionary<ISessionStatic, object>();

        internal void DisposeStaticVars(TapThread lastThread)
        {
            foreach (var item in StaticVars)
            {
                if(item.Key.AutoDispose && item.Value is IDisposable disp)
                {
                    disp.Dispose();
                    StaticVars[item.Key] = null;
                }
            }
        }

        /// <summary>
        /// Gets the currently active session.
        /// </summary>
        public static Session Current => Sessions.Value ?? RootSession;

        /// <summary>
        /// Gets the session ID for this session.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// Gets the flags used to create/start this session.
        /// </summary>
        public SessionOptions Options => options;

        readonly SessionOptions options;

        Session(SessionOptions options)
        {
            this.options = options;
            RunningSessions.Add(Id, this);
        } 

        static readonly TraceSource log = Log.CreateSource(nameof(Session));
        
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
            RunningSessions.Remove(Id);

            foreach (var ex in exceptions)
            {
                log.Error("Caught error while disposing session: {0}", ex.Message);
                log.Debug(ex);
            }
        }

        /// <summary> Creates a new session in the current <see cref="TapThread"/> context. The session lasts until the TapTread ends, or Dispose is called on the returned Session object.</summary>
        /// <param name="options">Flags selected from the SessionOptions enum to customize the behavior of the session.</param>
        /// <returns> A disposable Session object. </returns>
        public static Session Create(SessionOptions options = SessionOptions.OverlayComponentSettings | SessionOptions.RedirectLogging)
        {
            var session = new Session(options);
            session.disposables.Push(TapThread.UsingThreadContext($"SessionRootContext-{session.Id}", session.DisposeStaticVars));
            session.Activate();
            return session;
        }

        /// <summary>
        /// Creates a new session, and runs the specified action in the context of that session. When the acion completes, the session is Disposed automatically.
        /// </summary>
        public static void Start(Action action, SessionOptions options = SessionOptions.OverlayComponentSettings | SessionOptions.RedirectLogging)
        {
            var session = new Session(options);
            TapThread.Start(() =>
            {
                try
                {
                    session.Activate();
                    Sessions.Value = session;
                    action();
                }
                finally
                {
                    session.Dispose();
                }
            }, session.DisposeStaticVars, $"SessionRootThread-{session.Id}");
        }

        /// <summary>
        /// Synchroniously runs the specified ation in the context of the given session
        /// </summary>
        /// <param name="sessionId">ID of the session.</param>
        /// <param name="action">The action to run.</param>
        /// <returns>The session in which the action is run</returns>
        public static Session RunInSession(Guid sessionId, Action action)
        {
            if (RunningSessions.ContainsKey(sessionId))
            {
                var session = RunningSessions[sessionId];
                session.RunInSession(action);
                return session;
            }
            else
                throw new ArgumentOutOfRangeException($"Session with ID {sessionId} does not exist.");
        }

        /// <summary>
        /// Synchroniously runs the specified ation in the context of the given session
        /// </summary>
        /// <param name="action">The action to run.</param>
        /// <returns>The session in which the action is run</returns>
        public void RunInSession(Action action)
        {
            TapThread.WithNewContext(action, this.ThreadContext);
        }

        void Activate()
        {
            try
            {
                foreach (var item in Current.StaticVars)
                {
                    StaticVars.Add(item.Key, item.Value);
                }
                Sessions.Value = this;
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