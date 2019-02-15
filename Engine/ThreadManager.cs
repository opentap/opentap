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
        /// A thread is currently processing the work.
        /// </summary>
        Running,
        /// <summary>
        /// The work has completed.
        /// </summary>
        Completed
    }

    /// <summary>
    /// Represents a item of work in the <see cref="ThreadManager"/>. Also allows access to the Parent <see cref="TapThread"/> (the one that <see cref="TapThread.Start"/>ed the work represented by this object)
    /// </summary>
    public class TapThread
    {
        static ThreadManager manager = new ThreadManager();

        /// <summary> Enqueue an action to be executed asynchroniously. </summary>
        /// <param name="f"></param>
        public static TapThread Start(Action f)
        {
            return manager.Enqueue(f);
        }

        /// <summary>
        /// The currently running TapThread
        /// </summary>
        public static TapThread Current
        {
            get
            {
                if (ThreadManager.ThreadKey == null) ThreadManager.ThreadKey = new TapThread(null, null);
                return ThreadManager.ThreadKey;
            }
        }

        internal Action action;

        /// <summary>
        /// the <see cref="TapThread"/> that <see cref="Start"/>ed the work represented by this TapThread.
        /// </summary>
        public readonly TapThread Parent;

        /// <summary>
        /// The execution status of the work
        /// </summary>
        public TapThreadStatus Status { get; internal set; }

        internal TapThread(TapThread parent, Action action)
        {
            this.action = action;
            Parent = parent;
            Status = TapThreadStatus.Queued;
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
        SemaphoreSlim freeWorkSemaphore = new SemaphoreSlim(0);

        Thread threadManagerThread;
        /// <summary> The number of currently available workers.</summary>
        int freeWorkers = 0;
        EngineSettings settings => EngineSettings.Current;
        Stopwatch lastTime = Stopwatch.StartNew();
        /// <summary>
        /// Max number of worker threads.
        /// </summary>
        int MaxWorkerThreads = 1024; // normally around 1024.

        /// <summary> Current number of threads. </summary>
        public uint ThreadCount => (uint)threads;
        /// <summary> Enqueue an action to be executed in the future. </summary>
        /// <param name="f"></param>
        public TapThread Enqueue(Action f)
        {
            var newThread = new TapThread(TapThread.Current, f);
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
        static int idleThreadCount = Environment.ProcessorCount * 4;
        void threadManagerWork()
        {
            var handles = new WaitHandle[2];
            while (cancelSrc.IsCancellationRequested == false)
            {
                for(uint i = (uint)threads; i < idleThreadCount; i++)
                {
                    newWorkerThread();
                }

                handles[0] = freeWorkSemaphore.AvailableWaitHandle;
                handles[1] = cancelSrc.Token.WaitHandle;
                int state = WaitHandle.WaitAny(handles);
                if (state == 1) break;
                if (state == 0 && !freeWorkSemaphore.Wait(0)) continue;
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
                handles[0] = freeWorkSemaphore.AvailableWaitHandle;
                handles[1] = cancelSrc.Token.WaitHandle;

                while (cancelSrc.IsCancellationRequested == false)
                {
                    try
                    {
                        int state = WaitHandle.WaitAny(handles, timeout);
                        if (state == WaitHandle.WaitTimeout)
                        {
                            if (ThreadCount < idleThreadCount)
                                continue;
                            break;
                        }
                        freeWorkSemaphore.Wait(0); // decrement the semaphore - It's not done through WaitAny.
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
                                    ThreadKey.Status = TapThreadStatus.Running;
                                    work.action();
                                    
                                }
                                finally
                                {
                                    ThreadKey.Status = TapThreadStatus.Completed;
                                    ThreadKey = null;
                                }
                            }
                        }
                        finally
                        {
                            Interlocked.Increment(ref freeWorkers);
                        }
                    }
                    catch
                    {
                        throw;
                    }
                }
            }
            catch (Exception e)
            {
                // exceptions should be handled at the 'work' level.
                if((e is ThreadAbortException) == false)
                    Debug.Fail("Exception unhandled in worker thread.");
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
                var identifier = TapThread.Current;
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
    }
}
