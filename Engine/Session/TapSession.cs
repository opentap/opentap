using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace OpenTap
{

    /// <summary> Specifies how a session flag interacts with the session.</summary>
    public interface ISessionFlag
    {
        /// <summary> When this flag is activated, the Activate method should return an IDisposable, that on dispose clears the session.</summary>
        IDisposable Activate();
    }
    
    /// <summary> Default flags for customizing a session. Others can be added by implementing the ISessionFlag interface. </summary>
    public sealed class SessionFlag : ISessionFlag
    {
        /// <summary> Redirect logging. When this flag is enabled, existing log listeners can be overwritten with new ones. </summary>
        public static SessionFlag RedirectLogging = new SessionFlag(nameof(RedirectLogging));
        ///// <summary> Overlay logging: Extra listeners can be added.</summary>
        //public static SessionFlag OverlayLogging = new SessionFlag(nameof(OverlayLogging));
        
        /// <summary> Inherit Thread Context. A flag specifying that the thread context will be inherited from the previous one. This is the default behavior</summary>
        internal static SessionFlag InheritThreadContext = new SessionFlag(nameof(InheritThreadContext));
        
        ///// <summary> A flag specifying that the new session will have it's own thread context.</summary>
        //public static SessionFlag NewThreadContext = new SessionFlag(nameof(NewThreadContext));
        
        /// <summary>
        /// Component settings are cloned for the sake of this component settings. Instrument, DUT etc instances are cloned.
        /// When this is used, test plans should be reloaded in the new context.  
        /// </summary>
        public static SessionFlag OverlayComponentSettings = new SessionFlag(nameof(OverlayComponentSettings));
        
        readonly string flagName;
        SessionFlag(string flagName) => this.flagName = flagName;
        
        /// <summary> Gets the the flag name string. </summary>
        public override string ToString() => $"{nameof(SessionFlag)}.{flagName}";
        
        // the default implementation of the Activate method.
        IDisposable ISessionFlag.Activate()
        {
            if (this == InheritThreadContext)
                return TapThread.UsingThreadContext();
            //if (this == NewThreadContext)
            //    return TapThread.UsingThreadContext(null);
            if(this == OverlayComponentSettings)
                return ComponentSettings.BeginSession();
            if (this == RedirectLogging)
                return Log.WithNewContext();
            return null;
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
        static Session RootSession = new Session();
        
        /// <summary>
        /// Gets the currently active session.
        /// </summary>
        public static Session Current => Sessions.Value ?? RootSession;

        /// <summary>
        /// Gets the session ID for this session.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();
        
        /// <summary>
        /// Gets the flags used by this session.
        /// </summary>
        public IEnumerable<ISessionFlag> Flags => flags;

        readonly ImmutableHashSet<ISessionFlag> flags;

        Session(params ISessionFlag[] flags)
        {
            this.flags = flags.Append(SessionFlag.InheritThreadContext).ToImmutableHashSet();   
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
            Sessions.Value = null;

            foreach (var ex in exceptions)
            {
                log.Error("Caught error while disposing session: {0}", ex.Message);
                log.Debug(ex);
            }
        }

        /// <summary> Creates a new session. </summary>
        /// <param name="flags">Flags selected from the SessionFlags class.</param>
        /// <returns> A disposable Session object. </returns>
        public static Session WithSession(params ISessionFlag[] flags)
        {
            var session = new Session(flags);
            session.Activate();
            Sessions.Value = session;
            return session;
        }
        
        void activeSessionFlag(ISessionFlag flag)
        {
            if (!flags.Contains(flag)) return;
            var disposable = flag.Activate();
            if (disposable != null)
                disposables.Push(disposable);
        }

        void Activate()
        {
            try
            {
                // the order of the default flags are important.
                //if(!flags.Contains(SessionFlag.NewThreadContext))
                    activeSessionFlag(SessionFlag.InheritThreadContext);
                //activeSessionFlag(SessionFlag.NewThreadContext);
                activeSessionFlag(SessionFlag.OverlayComponentSettings);
                activeSessionFlag(SessionFlag.RedirectLogging);

                foreach (var flag in flags)
                {
                    if (flag is SessionFlag) continue;
                    activeSessionFlag(flag);
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }
    }
}