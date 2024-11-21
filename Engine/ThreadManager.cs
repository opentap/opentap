//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap
{
    /// <summary>
    /// Status of a <see cref="TapThread"/>.
    /// </summary>
    public enum TapThreadStatus
    {
        /// <summary>
        /// Work has been queued, but not started yet.
        /// </summary>
        Queued,
        /// <summary>
        /// Work is currently being processed.
        /// </summary>
        Running,
        /// <summary>
        /// Work has completed.
        /// </summary>
        Completed,
        /// <summary>
        /// This and all child threads have completed.
        /// </summary>
        HierarchyCompleted,
    }


    /// <summary> baseclass for ThreadField types. </summary>
    internal abstract class ThreadField
    {
        protected static readonly object DefaultCacheMarker = new object();
        static int threadFieldIndexer = 0;
        /// <summary>  Index of this thread field. </summary>
        protected readonly int Index = GetThreadFieldIndex();   
        
        static int GetThreadFieldIndex() => Interlocked.Increment(ref threadFieldIndexer);
        
        protected void SetFieldValue(object value)
        {
            var currentThread = TapThread.Current;
            if (currentThread.Fields == null)
                currentThread.Fields = new object[Index + 1];
            else if(currentThread.Fields.Length <= Index)
            {
                var newArray = new object[Index + 1];
                currentThread.Fields.CopyTo(newArray, 0);
                currentThread.Fields = newArray;
            }
            currentThread.Fields[Index] = value;
        }

        protected bool TryGetFieldValue(TapThread thread, out object value)
        {
            if (thread.Fields != null && thread.Fields.Length > Index)
            {
              var currentValue = thread.Fields[Index];
              if (currentValue != null)
              {
                  value = currentValue;
                  return true;
              }
            }
            value = null;
            return false;
        }
    }
    
    [Flags]
    internal enum ThreadFieldMode
    {
        None = 0,
        /// <summary>  Cached-mode ThreadFields are a bit faster as they dont need to iterate for finding commonly used values.
        /// A value found in the parent thread is upgraded to local cache. Changes in parent thread thread-field values has no effect after it has
        /// been cached the first time.</summary>
        Cached = 1,
        /// <summary>
        /// A flat is a kind of cache that is local to the current thread only. It never inherits to the parent thread value.
        /// </summary>
        Flat = 2
    }
    
    /// <summary>
    /// Thread fields are static objects that manage the value of a thread field.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class ThreadField<T> : ThreadField
    {
        readonly int mode; 
         
        bool isCached => (mode & (int) ThreadFieldMode.Cached) > 0;

        public ThreadField(ThreadFieldMode threadFieldMode = ThreadFieldMode.None) =>  mode = (int)threadFieldMode;
        
        /// <summary>
        /// Gets or sets the value of the thread field. Note that the getter may get a value from a parent thread, while the setter cannot override values from parent fields. 
        /// </summary>
        public T Value
        {
            get => Get();
            set => Set(value);
        }

        /// <summary> Gets the current value for things thread, if any.</summary>
        public T GetCached()
        {
            var thread = TapThread.Current;
            if (TryGetFieldValue(thread, out var value) && value is T x)
                return x;
            return default;
        } 
        
        T Get()
        {
            var thread = TapThread.Current;
            bool isParent = false;
            
            // iterate through parent threads.
            while (thread != null)
            {
                if (TryGetFieldValue(thread, out var found))
                {
                    if (isCached)
                    {
                        if (isParent)
                            SetFieldValue(found); // set the value on the current thread (not on parent).
                        if (ReferenceEquals(found, DefaultCacheMarker))
                            return default;
                    }
                    return (T)found;
                }
                
                
                if ((mode & (int)ThreadFieldMode.Flat) > 0)
                {
                    // flat mode: Dont iterate to parent.
                    return default;
                }
                thread = thread.Parent;
                isParent = true;
            }

            if (isCached)
                SetFieldValue(DefaultCacheMarker);

            return default;
        }

        void Set(T value) => SetFieldValue(value);
    }

    /// <summary>
    /// Represents a item of work in the <see cref="ThreadManager"/>. Also allows access to the Parent <see cref="TapThread"/> (the thread that initially called<see cref="TapThread.Start(Action, string)"/>)
    /// </summary>
    public class TapThread
    {
        #region fields
        internal static readonly ThreadManager manager = new ThreadManager();
        
        static readonly SessionLocal<ThreadManager> sessionThreadManager = new SessionLocal<ThreadManager>(manager);
        Action action;
 
        CancellationTokenSource _abortTokenSource;

        readonly object tokenCreateLock = new object();
        CancellationTokenSource abortTokenSource
        {
            get
            {
                if (_abortTokenSource == null)
                {
                    lock (tokenCreateLock)
                    {
                        if (_abortTokenSource != null) 
                            return _abortTokenSource;
                        
                        if (Parent is TapThread parentThread)
                        {
                            // Create a new cancellation token source and link it to the thread's parent abort token.
                            _abortTokenSource =
                                CancellationTokenSource.CreateLinkedTokenSource(parentThread.AbortToken);
                        }
                        else
                        {
                            if (sessionThreadManager.Value is ThreadManager mng)
                                _abortTokenSource = CancellationTokenSource.CreateLinkedTokenSource(mng.AbortToken);
                            else _abortTokenSource = new CancellationTokenSource();
                        }
                    }
                }

                return _abortTokenSource;
            }
        }

        #endregion

        internal object[] Fields;
        #region properties

        static int rootThreadNameId = 0;
        
        /// <summary>
        /// The currently running TapThread
        /// </summary>
        public static TapThread Current
        {
            get
            {
                if (ThreadManager.ThreadKey == null)
                {
                    var id = Interlocked.Increment(ref rootThreadNameId);
                    ThreadManager.ThreadKey = new TapThread(null, null, null, id == 1 ? "Main Thread" : $"Root Thread {id-1}");
                }
                return ThreadManager.ThreadKey;
            }
        }

        /// <summary> Pretends that the current thread is a different thread while evaluating 'action'. 
        /// This affects the functionality of ThreadHierarchyLocals and TapThread.Current. 
        /// This overload also specifies which parent thread should be used.</summary>
        public static void WithNewContext(Action action, TapThread parent)
        {
            var currentThread = Current;
            ThreadManager.ThreadKey = new TapThread(parent, action, null, currentThread.Name)
            {
                Status = TapThreadStatus.Running
            };
            try
            {
                action();
            }
            finally
            {
                decrementThreadHierarchyCount(ThreadManager.ThreadKey);
                ThreadManager.ThreadKey = currentThread;
            }
        }

        /// <summary> This should be used through Session. </summary>
        internal static IDisposable UsingThreadContext(TapThread parent, Action onHierarchyCompleted = null)
        {
            var currentThread = Current;
            var newThread = new TapThread(parent, () => { }, onHierarchyCompleted, currentThread.Name)
            {
                Status = TapThreadStatus.Running
            };
            ThreadManager.ThreadKey = newThread;
            
            return Utils.WithDisposable(() =>
            {
                decrementThreadHierarchyCount(newThread);
                ThreadManager.ThreadKey = currentThread;
            });
        }

        /// <summary> This should be used through Session. </summary>
        internal static IDisposable UsingThreadContext(Action onHierarchyCompleted = null) => UsingThreadContext(TapThread.Current, onHierarchyCompleted);

        /// <summary> Pretends that the current thread is a different thread while evaluating 'action'. 
        /// This affects the functionality of ThreadHierarchyLocals and TapThread.Current. </summary>
        public static void WithNewContext(Action action)
        {
            WithNewContext(action, parent: Current);
        }
        
        /// <summary> An (optional) name identifying the OpenTAP thread. </summary>
        public readonly string Name;

        /// <summary>
        /// The execution status of the work
        /// </summary>
        public TapThreadStatus Status { get; private set; }

        /// <summary>
        /// The abort token for this thread. Provides an interface to check the cancellation status of the current thread. Note, the status of this token is inherited from parent threads.
        /// </summary>
        public CancellationToken AbortToken => abortTokenSource.Token;

        internal bool CanAbort { get; set; } = true;
        
        /// <summary>
        /// The parent <see cref="TapThread">TapThread</see> that started this thread. In case it is null, then it is 
        /// not a managed <see cref="TapThread">TapThread</see>.
        /// </summary>
        public TapThread Parent { get; }
        #endregion

        #region ctor
        internal TapThread(TapThread parent, Action action, Action onHierarchyCompleted = null, string name = "")
        {
            Name = name;
            this.action = action;
            Parent = parent;
            threadHierarchyCompleted = onHierarchyCompleted ?? (() => {});
            incrementThreadHeirarchy(this);
            Status = TapThreadStatus.Queued;
            

        }

        /// <summary> </summary>
        ~TapThread()
        {
            _abortTokenSource?.Dispose();
        }
        #endregion

        /// <summary>
        /// Aborts the execution of this current instance of the <see cref="TapThread">TapThread</see>.
        /// </summary>
        public void Abort()
        {
            Abort(null);
        }

        internal void AbortNoThrow()
        {
            abortTokenSource.Cancel();
        }

        /// <summary>
        /// Aborts the execution of this current instance of the <see cref="TapThread">TapThread</see> with a
        /// specified reason.
        /// <param name="reason">Thea reason to abort.</param>
        /// </summary>
        internal void Abort(string reason)
        {
            //if (CanAbort == false)
            //    throw new Exception("Cannot abort Thread");
            abortTokenSource.Cancel();
            if (Current.AbortToken.IsCancellationRequested)
            {
                // Check if the aborted thread is the current thread or a parent of it.
                // if so then throw.
                var trd = Current;
                while(trd != null)
                {
                    if (this == trd)
                    {
                        if(reason != null)
                            throw new OperationCanceledException(reason, Current.AbortToken);
                        Current.AbortToken.ThrowIfCancellationRequested();
                    }
                    trd = trd.Parent;
                }
            }
        }

        /// <summary> Enqueue an action to be executed asynchronously. </summary>
        /// <param name="action">The action to be executed.</param>
        /// <param name="name">The (optional) name of the OpenTAP thread. </param>
        public static TapThread Start(Action action, string name = "")
        {
            return Start(action, null, name);
        }

        internal static Task StartAwaitable(Action action, string name = "")
        {
            return StartAwaitable(action, null, name);
        }

        internal static Task StartAwaitable(Action action, CancellationToken? token, string name = "")
        {
            var task = new TaskCompletionSource<bool>();
            Start(() =>
            {
                try
                {
                    if (token.HasValue)
                    {
                        var trd = TapThread.Current;
                        using (token.Value.Register(() => trd.Abort()))
                            action();
                    }
                    else
                    {
                        action();
                    }
                }
                catch (Exception inner)
                {
                    task.SetException(inner);
                }
                finally
                {
                    task.SetResult(true);
                }
            }, null, name);
            return task.Task;
        }

        /// <summary> Starts a new Tap Thread.</summary>
        /// <param name="action">The action to run.</param>
        /// <param name="onHierarchyCompleted">Executed when this hierarchy level is completed (may be before child threads complete)</param>
        /// <param name="name">The name of the thread.</param>
        /// <param name="threadContext">The parent context. null if the current context should be selected.</param>
        /// <returns>A thread instance.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        internal static TapThread Start(Action action, Action onHierarchyCompleted, string name = "", TapThread threadContext = null)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action), "Action to be executed cannot be null.");
            var newThread = new TapThread(threadContext ?? Current, action, onHierarchyCompleted, name);
            manager.Enqueue(newThread);
            return newThread;
        }

        /// <summary>
        ///  Blocks the current thread until the current System.Threading.WaitHandle receives 
        ///  a signal, using a 32-bit signed integer to specify the time interval in milliseconds.
        /// </summary>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or 0 by default.</param>
        public static void Sleep(int millisecondsTimeout)
        {
            Sleep(TimeSpan.FromMilliseconds(millisecondsTimeout));
        }

        /// <summary> Throws an OperationCancelledException if the current TapThread has been aborted. This is the same as calling TapThread.Current.AbortToken.ThrowIfCancellationRequested(). </summary>
        public static void ThrowIfAborted()
        {
            Current.AbortToken.ThrowIfCancellationRequested();
        }

        /// <summary> Blocks the current thread for a specified amount of time. Will throw an OperationCancelledException if the 
        /// thread is aborted during this time.</summary>
        /// <param name="timeSpan">A System.TimeSpan that represents the number of milliseconds to wait.</param>
        public static void Sleep(TimeSpan timeSpan)
        {
            var cancellationToken = Current.AbortToken;
            cancellationToken.ThrowIfCancellationRequested();

            if (timeSpan <= TimeSpan.Zero)
                return;

            TimeSpan min(TimeSpan a, TimeSpan b)
            {
                return a < b ? a : b;
            }

            TimeSpan longTime = TimeSpan.FromHours(1);
            var sw = Stopwatch.StartNew();

            while (true)
            {
                var timeleft = timeSpan - sw.Elapsed;
                if (timeleft <= TimeSpan.Zero)
                    break;

                // if plan.abortAllowed is false, the token might be canceled,
                // but we still cannot abort, so in that case we need to default to Thread.Sleep

                if (cancellationToken.WaitHandle.WaitOne(min(timeleft, longTime)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }

        // Reference counter for all threads in this hierarchy
        int threadHierarchyCount;
        readonly Action threadHierarchyCompleted;

        internal void Process()
        {
            if (action == null) throw new InvalidOperationException("TapThread cannot be executed twice.");
            Status = TapThreadStatus.Running;
            try
            {
                action();
            }
            finally
            {
                Status = TapThreadStatus.Completed;
                // set action to null to signal that it has been processed.
                // also to allow GC to clean up closures.
                action = null;

                decrementThreadHierarchyCount(this);
            }
        }

        static void incrementThreadHeirarchy(TapThread trd)
        {
            for(; trd != null; trd = trd.Parent)
                Interlocked.Increment(ref trd.threadHierarchyCount);
        }
        
        static void decrementThreadHierarchyCount(TapThread t)
        {
            for (; t != null; t = t.Parent)
            {
                var dec = Interlocked.Decrement(ref t.threadHierarchyCount); 
                if (dec == 0)
                {
                    t.Status = TapThreadStatus.HierarchyCompleted;
                    t.threadHierarchyCompleted();
                }

                if (dec < 0)
                    throw new InvalidOperationException("thread hierarchy count mismatch");
            }
        }

        /// <summary> Returns a readable string.</summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"[Thread '{Name}']";
        }
    }

    /// <summary> Custom thread pool for fast thread startup. </summary>
    internal class ThreadManager : IDisposable
    {
        // this queue is generally empty, since a new thread will be started whenever no workers are available for processing.
        readonly ConcurrentQueue<TapThread> workQueue = new ConcurrentQueue<TapThread>();

        [ThreadStatic]
        internal static TapThread ThreadKey;
        
        // used for cancelling currently running tasks.
        readonly CancellationTokenSource cancelSrc = new CancellationTokenSource();

        // this semaphore counts up whenever there is work to be done.
        readonly Semaphore freeWorkSemaphore = new Semaphore(0, Int32.MaxValue);

        /// <summary> The number of currently available workers.</summary>
        int freeWorkers = 0;

        /// <summary> Current number of threads. </summary>
        public uint ThreadCount => (uint)threads;
        int threads = 0;
        /// <summary> Thread manager root abort token. This can cancel all thread and child threads.
        /// Canceled when the thread manager is disposed. </summary>
        public CancellationToken AbortToken => cancelSrc.Token;

        void monitorThread()
        {
            while (AbortToken.IsCancellationRequested == false)
            {
                try
                {
                    TapThread.Sleep(1000);
                }
                catch
                {
                    
                }
                Log.CreateSource("monitor").Debug($"Threads count:  {threadCount}");
            }
        }
        bool monitorStarted = false;
        
        /// <summary> Enqueue an action to be executed in the future. </summary>
        /// <param name="work">The work to be processed.</param>
        public void Enqueue(TapThread work)
        {
            if(cancelSrc.IsCancellationRequested) 
                throw new Exception("ThreadManager has been disposed.");

            if (!monitorStarted)
            {
                monitorStarted = true;
                TapThread.Start(monitorThread);
            }
            retry:
            WorkerThread worker = null;

            do
            {
                worker = idleThreads;
            }while(worker != null && Interlocked.CompareExchange(ref idleThreads, idleThreads.NextFree, worker) != worker);
            
            if (worker == null)
                worker = newWorkerThread();
            if (worker.IsEnded)
            {
                goto retry;
            }
            worker.Start(work);
        }
        
        /// <summary> Creates a new ThreadManager. </summary>
        internal ThreadManager()
        {
            
        }
        internal static int IdleThreadCount { get => idleThreadCount; set => idleThreadCount = value; }
        static int idleThreadCount = 4;
        WorkerThread newWorkerThread()
        {
            var workerThread = new WorkerThread(this);
            Interlocked.Increment(ref freeWorkers);
            return workerThread;
        }
        static int threadCount; 
        class WorkerThread
        {
            public override string ToString()
            {
                return $"WorkerThread; {WorkerThreadIndex}";
            }
            static int workerThreadCounter = 0;
            public readonly int WorkerThreadIndex = Interlocked.Increment(ref workerThreadCounter);
            public WorkerThread NextFree;
            readonly Thread trd;
            readonly ThreadManager manager;
            TapThread work;
            readonly object monitor = new object();
            bool isStarted;
            bool isEnded;
            public bool IsEnded => isEnded;
            public WorkerThread(ThreadManager manager)
            {
                this.manager = manager;
                trd = new Thread(processQueue) { IsBackground = true, Name = "Unnamed Work Thread", Priority = ThreadPriority.BelowNormal };
            }

            public void Start(TapThread task)
            {
                
                this.work = task;
                if (!isStarted)
                {
                    Interlocked.Increment(ref threadCount);
                    isStarted = true;
                    trd.Start();
                }
                else
                {
                    lock (monitor)
                    {
                        Monitor.Pulse(monitor);
                    }
                }
            }

            public void Pulse()
            {
                lock(monitor)
                    Monitor.Pulse(monitor);
            }

            void processQueue()
            {
                try
                {
                    while (manager.cancelSrc.IsCancellationRequested == false)
                    {
                        if (work != null)
                        {
                            ThreadKey = work;
                            try
                            {
                                work.Process();
                            }

                            catch (OperationCanceledException)
                            {
                                break;
                            }
                            catch (ThreadAbortException)
                            {
                                break;
                            }
                            catch (Exception)
                            {
                                break;
                            }
                            finally
                            {
                                ThreadKey = null;
                            }
                            work = null;
                        }

                        lock (monitor)
                        {
                            do
                            {
                                NextFree = manager.idleThreads;
                            } while (Interlocked.CompareExchange(ref manager.idleThreads, this, NextFree) != NextFree);

                            if (!Monitor.Wait(monitor, timeout))
                            {
                                isEnded = true;
                                break;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
                finally
                {
                    isEnded = true;
                   CleanList();
                    Interlocked.Decrement(ref threadCount);
                }
            }
            public void CleanList()
            {
                bool cleanList(ref WorkerThread wk)
                {
                    var val = wk;
                    if (val == null) return true;
                    if (val.IsEnded && val.NextFree == null)
                    {
                        wk = null;
                        return true;
                    }
                    if (val.NextFree != null && cleanList(ref val.NextFree))
                    {
                        wk = null;
                        return true;
                    }
                    return false;
                }
                cleanList(ref NextFree);
            }

        }
        WorkerThread idleThreads = null;
        
        // if a thread waits for some time and no new work is fetched, it can stop.
        // 5000ms is a good number, because resultlisteners sometimes wait 3s if they have too much work.
        // this avoids the threads shutting down just because of this.
        static readonly int timeout = 5 * 60 * 1000; // 5 minutes

        public static void ShutDown()
        {
            TapThread.manager.Dispose();
        }


        /// <summary> Disposes the ThreadManager. This can optionally be done at program exit.</summary>
        public void Dispose()
        {
            cancelSrc.Cancel();
            while (threadCount > 0)
            {
                var trd = idleThreads;
                while (trd != null)
                {
                    if (!trd.IsEnded)
                    {
                        trd.Pulse();    
                    }
                            
                    trd = trd.NextFree;
                }
                Thread.Sleep(10);
                idleThreads?.CleanList();
            }
        }
    }

    /// <summary>
    /// Has a separate value for each hierarchy of Threads that it is set on.
    /// If a thread sets this to a value, that value will be visible only to that thread and its child threads (as started using <see cref="TapThread.Start(Action, string)"/>)
    /// </summary>
    class ThreadHierarchyLocal<T> where T : class
    {
        readonly ConditionalWeakTable<TapThread, T> threadObjects = new ConditionalWeakTable<TapThread, T>();
        /// <summary>
        /// Has a separate value for each hierarchy of Threads that it is set on.
        /// If a thread sets this to a value, that value will be visible only to that thread and its child threads (as started using <see cref="TapThread.Start(Action, string)"/>)
        /// </summary>
        public T LocalValue
        {
            get
            {
                TapThread identifier = TapThread.Current;
                while (identifier != null)
                {
                    if (threadObjects.TryGetValue(identifier, out var value))
                        return value;
                    identifier = identifier.Parent;
                }
                return default(T);
            }
            set
            {
                var identifier = TapThread.Current;
                if (threadObjects.TryGetValue(identifier, out var _))
                    threadObjects.Remove(identifier);
                threadObjects.Add(identifier, value);
            }
        }

        /// <summary>
        /// Removes the thread-locally set value.
        /// </summary>
        public void ClearLocal()
        {
            var identifier = TapThread.Current;
            if (threadObjects.TryGetValue(identifier, out var _))
                threadObjects.Remove(identifier);
        }
    }
}
