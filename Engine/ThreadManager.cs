//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

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
        Completed
    }


    /// <summary> baseclass for ThreadField types. </summary>
    internal abstract class ThreadField
    {
        static int threadFieldIndexer = 0;
        /// <summary>  Index of this thread field. </summary>
        protected readonly int Index = GetThreadFieldIndex();   
        
        static int GetThreadFieldIndex() => Interlocked.Increment(ref threadFieldIndexer);
    }
    
    /// <summary>
    /// Thread fields are static objects that manage the value of a thread field.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class ThreadField<T> : ThreadField
    {
        /// <summary>
        /// Gets or sets the value of the thread field. Note that the getter may get a value from a parent thread, while the setter cannot override values from parent fields. 
        /// </summary>
        public T Value
        {
            get => Get();
            set => Set(value);
        }
        
        T Get()
        {
            var thread = TapThread.Current;
            while (thread != null)
            {
                if (thread.Fields != null && thread.Fields.Length > Index && thread.Fields[Index] != null)
                    return (T)thread.Fields[Index];
                thread = thread.Parent;
            }

            return default;
        }

        void Set(T value)
        {
            var currentThread = TapThread.Current;
            if (currentThread.Fields == null)
                currentThread.Fields = new object[Index + 1];
            else if(currentThread.Fields.Length <= Index)
            {
                var newarray = new object[Index + 1];
                currentThread.Fields.CopyTo(newarray, 0);
                currentThread.Fields = newarray;
            }
            currentThread.Fields[Index] = value;
        }
    }
    
    /// <summary>
    /// Represents a item of work in the <see cref="ThreadManager"/>. Also allows access to the Parent <see cref="TapThread"/> (the thread that initially called<see cref="TapThread.Start"/>)
    /// </summary>
    public class TapThread
    {
        #region fields
        static ThreadManager manager = new ThreadManager();
        Action action;
        readonly CancellationTokenSource abortTokenSource;
        #endregion

        internal object[] Fields = null;
        #region properties
        /// <summary>
        /// The currently running TapThread
        /// </summary>
        public static TapThread Current
        {
            get
            {
                if (ThreadManager.ThreadKey == null)
                {
                    ThreadManager.ThreadKey = new TapThread(null, null);
                }
                return ThreadManager.ThreadKey;
            }
        }

        /// <summary> Pretends that the current thread is a different thread while evaluating 'action'. 
        /// This affects the functionality of ThreadHeirachyLocals and TapThread.Current. 
        /// This overload also specifies which parent thread should be used.</summary>
        public static void WithNewContext(Action action, TapThread parent)
        {
            var currentThread = Current;
            ThreadManager.ThreadKey = new TapThread(parent, action, currentThread.Name)
            {
                Status = TapThreadStatus.Running
            };
            try
            {
                action();
            }
            finally
            {
                ThreadManager.ThreadKey = currentThread;
            }
        }

        /// <summary> Pretends that the current thread is a different thread while evaluating 'action'. 
        /// This affects the functionality of ThreadHeirachyLocals and TapThread.Current. </summary>
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

        /// <summary>
        /// The parent <see cref="TapThread">TapThread</see> that started this thread. In case it is null, then it is 
        /// not a managed <see cref="TapThread">TapThread</see>.
        /// </summary>
        public TapThread Parent { get; private set; }
        #endregion

        #region ctor
        internal TapThread(TapThread parent, Action action, string name = "")
        {
            Name = name;
            this.action = action;
            Parent = parent;
            Status = TapThreadStatus.Queued;
            if (parent is TapThread parentThread)
            {
                // Create a new cancellation token source and link it to the thread's parent abort token.
                abortTokenSource = CancellationTokenSource.CreateLinkedTokenSource(parentThread.abortTokenSource.Token);
            }
            else
            {
                // Create a new cancellation token source in case this thread has not TapThread parent.
                abortTokenSource = new CancellationTokenSource();
            }
        }

        /// <summary> </summary>
        ~TapThread()
        {
            abortTokenSource.Dispose();
        }
        #endregion

        /// <summary>
        /// Aborts the execution of this current instance of the <see cref="TapThread">TapThread</see>.
        /// </summary>
        public void Abort()
        {
            Abort(null);
        }

        /// <summary>
        /// Aborts the execution of this current instance of the <see cref="TapThread">TapThread</see> with a
        /// specified reason.
        /// <param name="reason">Thea reason to abort.</param>
        /// </summary>
        internal void Abort(string reason)
        {
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
                        else
                            Current.AbortToken.ThrowIfCancellationRequested();
                    }
                    trd = trd.Parent;
                }
            }
        }

        /// <summary> Enqueue an action to be executed asynchroniously. </summary>
        /// <param name="action">The action to be executed.</param>
        /// <param name="name">The (optional) name of the OpenTAP thread. </param>
        public static TapThread Start(Action action, string name = "")
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action), "Action to be executed cannot be null.");
            return manager.Enqueue(action, name ?? "");
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
        ConcurrentQueue<TapThread> workQueue = new ConcurrentQueue<TapThread>();

        [ThreadStatic]
        internal static TapThread ThreadKey;
        
        // used for cancelling currently running tasks.
        CancellationTokenSource cancelSrc = new CancellationTokenSource();

        // this semaphore counts up whenever there is work to be done.
        Semaphore freeWorkSemaphore = new Semaphore(0, Int32.MaxValue);

        Thread threadManagerThread;
        /// <summary> The number of currently available workers.</summary>
        int freeWorkers = 0;
        /// <summary>
        /// Max number of worker threads.
        /// </summary>
        int MaxWorkerThreads = 1024; // normally around 1024.

        /// <summary> Current number of threads. </summary>
        public uint ThreadCount => (uint)threads;
        /// <summary> Enqueue an action to be executed in the future. </summary>
        /// <param name="action">The work to be processed.</param>
        /// <param name="name">The (Optional) name of the new OpenTAP thread. </param>
        public TapThread Enqueue(Action action, string name = "")
        {
            if(cancelSrc.IsCancellationRequested) throw new Exception("ThreadManager has been disposed.");
            var newThread = new TapThread(TapThread.Current, action, name);
            workQueue.Enqueue(newThread);
            freeWorkSemaphore.Release();
            return newThread;
        }
        
        /// <summary> Creates a new ThreadManager. </summary>
        internal ThreadManager()
        {
            threadManagerThread = new Thread(threadManagerWork) { Name = "Thread Manager", IsBackground = true, Priority = ThreadPriority.Normal };
            threadManagerThread.Start();
            ThreadPool.GetMaxThreads(out MaxWorkerThreads, out int _);
        }
        static int idleThreadCount = Environment.ProcessorCount * 2;
        void threadManagerWork()
        {
            var handles = new WaitHandle[2];
            while (cancelSrc.IsCancellationRequested == false)
            {
                for(uint i = (uint)threads; i < idleThreadCount; i++)
                {
                    newWorkerThread();
                }

                handles[0] = freeWorkSemaphore;
                handles[1] = cancelSrc.Token.WaitHandle;
                int state = WaitHandle.WaitAny(handles);
                if (state == 1) break;

                freeWorkSemaphore.Release();
                if (freeWorkers < workQueue.Count)
                {
                    if (threads > MaxWorkerThreads || freeWorkers > 20)
                    {
                        // this is very bad (and unlikely). We should just wait for the threads to do their things and not start more threads.
                        Thread.Sleep(20);
                    }
                    else
                    {
                        newWorkerThread();
                    }
                }
                else
                {
                    Thread.Yield();
                }
            }
        }

        void newWorkerThread()
        {
            var trd = new Thread(processQueue) { IsBackground = true, Name = "Unnamed Work Thread", Priority = ThreadPriority.BelowNormal };
            trd.Start();

            Interlocked.Increment(ref freeWorkers);
        }
        
        // if a thread waits for some time and no new work is fetched, it can stop.
        // 5000ms is a good number, because resultlisteners sometimes wait 3s if they have too much work.
        // this avoids the threads shutting down just because of this.
        static readonly int timeout = 5 * 60 * 1000; // 5 minutes
        int threads = 0;
        // This method can be processed by many threads at once.
        void processQueue()
        {
            int trd = Interlocked.Increment(ref threads);
            Debug.WriteLine("ThreadManager: Stating thread {0}", trd);
            var handles = new WaitHandle[2];
            try
            {
                handles[0] = freeWorkSemaphore;
                handles[1] = cancelSrc.Token.WaitHandle;

                while (cancelSrc.IsCancellationRequested == false)
                {
                    int state = WaitHandle.WaitAny(handles, timeout);
                    if (state == WaitHandle.WaitTimeout)
                    {
                        if (ThreadCount < idleThreadCount)
                            continue;
                        break;
                    }
                    if (workQueue.Count == 0)
                        continue; // Someone already handled the work. go back to sleep.

                    // once resumed, crunch as much as possible.
                    // this will reduce latency and cause some threads to wake up just to go to sleep again.
                    Interlocked.Decrement(ref freeWorkers);
                    try
                    {
                        while (cancelSrc.IsCancellationRequested == false && workQueue.TryDequeue(out TapThread work))
                        {
                            try
                            {
                                ThreadKey = work;
                                work.Process();
                            }
                            finally
                            {
                                ThreadKey = null;
                            }
                        }
                    }
                    finally
                    {
                        Interlocked.Increment(ref freeWorkers);
                    }
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch (ThreadAbortException)
            {
                // Can be throws when the application exits.
            }
            catch (Exception e)
            {
                // exceptions should be handled at the 'work' level.
                Debug.WriteLine("Exception unhandled in worker thread.");
                Debug.WriteLine(e.StackTrace);
            }
            finally
            {
                Interlocked.Decrement(ref freeWorkers);
                trd = Interlocked.Decrement(ref threads);
                Debug.WriteLine("ThreadManager: Ending thread {0}", trd);
            }
        }

        /// <summary> Disposes the ThreadManager. This can optionally be done at program exit.</summary>
        public void Dispose()
        {
            cancelSrc.Cancel();
            while (threads > 0)
                Thread.Sleep(10);
        }
    }

    /// <summary>
    /// Has a separate value for each hierarchy of Threads that it is set on.
    /// If a thread sets this to a value, that value will be visible only to that thread and its child threads (as started using <see cref="TapThread.Start"/>)
    /// </summary>
    class ThreadHierarchyLocal<T> where T : class
    {
        ConditionalWeakTable<TapThread, T> threadObjects = new ConditionalWeakTable<TapThread, T>();
        /// <summary>
        /// Has a separate value for each hierarchy of Threads that it is set on.
        /// If a thread sets this to a value, that value will be visible only to that thread and its child threads (as started using <see cref="TapThread.Start"/>)
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
