//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace OpenTap
{
    /// <summary> 
    /// Work Queue used for result processing in sequence but asynchronously. It uses the ThreadManager to automatically clean up threads that have been idle for a while.
    /// When the WorkQueue is disposed, the used thread is immediately returned to the ThreadManager.
    /// </summary>
    public class WorkQueue : IDisposable
    {
        /// <summary> Options for WorkQueues. </summary>
        [Flags]
        public enum Options
        {
            /// <summary> No options. </summary>
            None = 0,
            /// <summary> The thread is not returned to the ThreadManager when it has been idle for some time. In this situation the WorkQueue must be disposed manually. </summary>
            LongRunning = 1,
            /// <summary> Time averaging is enabled. Each piece of work will have measured time spent. </summary>
            TimeAveraging = 2
        }
        /// <summary>
        /// The amount of idle time to wait before giving the thread back to the threading manager. This has no effect if the LongRunning option is selected. 
        /// </summary>
        public int Timeout = 5;
        
        // list of things to do sequentially.
        readonly ConcurrentQueue<IInvokable> workItems = new ConcurrentQueue<IInvokable>();
        readonly TimeSpanAverager average;
        
        internal object Peek()
        {
            if (workItems.TryPeek(out var inv))
            {
                if (inv is IWrappedInvokable wrap)
                    return wrap.InnerInvokable;
                return inv;
            }
            return null;
        }

        /// <summary> The average time spent for each task. Only available if Options.TImeAveraging is enabled. </summary>
        public TimeSpan AverageTimeSpent
        {
            get
            {
                if (average == null)
                    throw new InvalidOperationException("The TimeAveraging option has not been selected.");
                return average.GetAverage();
            }
        }

        /// <summary> The current number of items in the work queue. If called from the worker thread, this number will be 0 for that last worker. </summary>
        public int QueueSize => workItems.Count;

        readonly object threadCreationLock = new object();
        readonly CancellationTokenSource cancel = new CancellationTokenSource();
        readonly TapThread threadContext = null;

        //this should always be either 1(thread was started) or 0(thread is not started yet)
        int threadCount = 0;

        internal static int semaphoreMaxCount = 1024 * 1024;
        // the addSemaphore counts the current number of things in the tasklist.
        readonly SemaphoreSlim addSemaphore = new SemaphoreSlim(0, semaphoreMaxCount); 

        int countdown = 0;
        
        /// <summary> A name of identifying the work queue. </summary>
        public readonly string Name;

        readonly bool longRunning;

        /// <summary> Creates a new instance of WorkQueue.</summary>
        /// <param name="options">Options.</param>
        /// <param name="name">A name to identify a work queue.</param>
        public WorkQueue(Options options, string name = "")
        {
            longRunning = options.HasFlag(Options.LongRunning);
            if (options.HasFlag(Options.TimeAveraging))
                average = new TimeSpanAverager();
            Name = name;
        }
        
        /// <summary> Creates a new instance of WorkQueue.</summary>
        /// <param name="options">Options.</param>
        /// <param name="name">A name to identify a work queue.</param>
        /// <param name="threadContext"> The thread context in which to run work jobs. The default value causes the context to be the parent of an enqueuing thread.</param>
        public WorkQueue(Options options, string name = "", TapThread threadContext = null) :this(options, name)
        {
            this.threadContext = threadContext;
        }


        /// <summary> Enqueue a new piece of work to be handled in the future. </summary>
        public void EnqueueWork(Action a) => EnqueueWork(new ActionInvokable(a));
        internal void EnqueueWork<T1, T2>(IInvokable<T1, T2> v, T1 a1, T2 a2) =>  EnqueueWork(new WrappedInvokable<T1,T2>(v, a1, a2));

        /// <summary>
        /// This method in in charge of processing the work queue.
        /// </summary>
        void WorkerFunction()
        {
            try
            {
                var awaitArray = new WaitHandle[] {addSemaphore.AvailableWaitHandle, cancel.Token.WaitHandle};
                while (true)
                {
                    retry:
                    awaitArray[1] = cancel.Token.WaitHandle;
                    int cancelIndex = 0;
                    if (longRunning)
                        cancelIndex = WaitHandle.WaitAny(awaitArray);
                    else
                        cancelIndex = WaitHandle.WaitAny(awaitArray, Timeout);
                    if (cancelIndex == 0 && !addSemaphore.Wait(0))
                        goto retry;
                    bool ok = cancelIndex == 0;

                    if (!ok)
                    {
                        if (cancel.IsCancellationRequested == false && longRunning) continue;
                        lock (threadCreationLock)
                        {
                            if (workItems.Count > 0)
                                goto retry;
                            break;
                        }
                    }

                    IInvokable run;
                    while (!workItems.TryDequeue(out run))
                        Thread.Yield();
                    try
                    {
                        if (average != null)
                        {
                            var sw = Stopwatch.StartNew();
                            run.Invoke();
                            average.PushTimeSpan(sw.Elapsed);
                        }
                        else
                        {
                            run.Invoke();
                        }
                    }
                    finally
                    {
                        Interlocked.Decrement(ref countdown);
                    }
                }
            }
            finally
            {
                lock (threadCreationLock)
                {
                    threadCount--;
                }
            }
        }

        /// <summary> Enqueue a new piece of work to be handled in the future. </summary>
        internal void EnqueueWork(IInvokable f)
        {
            while (addSemaphore.CurrentCount >= semaphoreMaxCount - 10)
            {
                // #4246: this is incredibly rare, but can happen if millions of results are pushed at once.
                //        the solution is to just slow a bit down when it happens.
                //        100 ms sleep is OK, because it needs to do around 1M things before it's idle.
                Thread.Sleep(100);
            }
            Interlocked.Increment(ref countdown);
            workItems.Enqueue(f);
            addSemaphore.Release();
            
            if (threadCount == 0)
            {
                lock (threadCreationLock)
                {
                    if (threadCount == 0)
                    {
                        TapThread.Start(WorkerFunction, null, Name, threadContext);
                        threadCount++;
                    }
                }
            }
        }
        
        /// <summary> Give the thread back to the thread manager.</summary>
        public void Dispose()
        {
            cancel.Cancel(false);
        }

        /// <summary> Waits for the workqueue to become empty. </summary>
        public void Wait()
        {
            // This is not often called spin-wait is fine in this case.
            while(countdown > 0)
            {
                Thread.Sleep(5);
            }
        }

        internal object Dequeue()
        {
            // Take the semaphore then take an object, just like WorkerFunction does.
            if (!addSemaphore.Wait(0))
                return null;
            if (workItems.TryDequeue(out var inv))
            {
                // when taking an item from the workqueue the countdown and the semaphore must be decremented
                Interlocked.Decrement(ref countdown);
                
                if (inv is IWrappedInvokable wrap)
                    return wrap.InnerInvokable;
                return inv;
            }
            return inv;
        }

        interface IWrappedInvokable: IInvokable
        {
            object InnerInvokable { get; }
        }
        
        /// <summary>  Wraps an Action in an IInvokable. </summary>
        class ActionInvokable : IWrappedInvokable
        {
            readonly Action action;
            public ActionInvokable(Action inv)
            {
                action = inv;
            }

            public void Invoke() => action();
            public object InnerInvokable => action;
        }
        /// <summary>  Wraps an IInvokable(T,T2) in an IInvokable. </summary>
        class WrappedInvokable<T, T2> : IWrappedInvokable 
        {
            readonly T arg1;
            readonly T2 arg2;
            readonly IInvokable<T, T2> wrapped;
        
            public WrappedInvokable(IInvokable<T, T2> invokable, T argument1, T2 argument2)
            {
                arg1 = argument1;
                arg2 = argument2;
                wrapped = invokable;
            }
            public void Invoke() => wrapped.Invoke(arg1, arg2);
            public object InnerInvokable => wrapped;
        }
    }
}
