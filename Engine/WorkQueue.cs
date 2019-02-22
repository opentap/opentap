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

    class TimeSpanAverager
    {
        const int averageWeight = 10;
        int averageCnt = 0;
        long[] weights = new long[10];
        int averageIndex = 0;

        public void PushTimeSpan(TimeSpan ts)
        {
            var indexOfValue = averageIndex = (averageIndex + 1) % weights.Length;
            weights[indexOfValue] = ts.Ticks;
            averageCnt = Math.Min(weights.Length, averageCnt + 1);
        }

        static TimeSpan defaultSpan = TimeSpan.FromSeconds(0.1);

        public TimeSpan GetAverage()
        {
            if (averageCnt == 0) return defaultSpan;
            long sum = 0;
            for(int i = 0; i < averageCnt; i++)
            {
                sum += weights[i];
            }

            var avg = TimeSpan.FromTicks(sum / averageCnt);
            return avg;
        }

    }

    /// <summary>
    /// 'inverse semaphore'. Wait blocks until the countdown is 0.
    /// </summary>
    class CountDownLatch
    {
        
        CountdownEvent evt = new CountdownEvent(0);
        int count = 0;
        object counterLock = new object();
        public void Increment()
        {
            lock (counterLock)
            {
                if (count == 0)
                {
                    evt.Reset(1);
                }
                evt.TryAddCount();
                count++;
            }
        }

        public void Decrement()
        {
            lock (counterLock)
            {
                if (count == 0) throw new InvalidOperationException();
                count--;
                
                if(count == 0)
                {
                    evt.Signal(2);
                }
                else
                {
                    evt.Signal();
                }
            }
        }

        public void Wait(CancellationToken token)
        {
            evt.Wait(token);
        }

        public void Wait()
        {
            evt.Wait();
        }
    }

    /// <summary> 
    /// Work Queue used for result processing in sequence but asynchronously. It uses the ThreadManager to automatically clean up threads that have been idle for a while.
    /// When the WorkQueue is disposed, the used thread is immediately returned to the ThreadManager.
    /// </summary>
    class WorkQueue : IDisposable
    {
        [Flags]
        public enum Options
        {
            None = 0,
            /// <summary>
            /// The thread is not returned to the ThreadManager when it has been idle for some time. In this situation the WorkQueue must be disposed manually.
            /// </summary>
            LongRunning = 1,
            /// <summary>
            /// Time averaging is enabled.
            /// </summary>
            TimeAveraging = 2
        }
        /// <summary>
        /// The amount of idle time to wait before giving the thread back to the threading manager. This has no effect if the LongRunning option is selected. 
        /// </summary>
        public int Timeout = 5;
        
        // list of things to do sequenctially.
        ConcurrentQueue<Action> workItems = new ConcurrentQueue<Action>();

        TimeSpanAverager average;
        public TimeSpan AverageTimeSpent
        {
            get
            {
                if (average == null)
                    throw new InvalidOperationException("The TimeAveraging option has not been selected.");
                return average.GetAverage();
            }
        }
        public int QueueSize { get { return workItems.Count; } }
        
        object threadCreationLock = new object();
        CancellationTokenSource cancel = new CancellationTokenSource();

        //this should always be either 1(thread was started) or 0(thread is not started yet)
        int threadCount = 0;

        const int semaphoreMaxCount = 1024 * 1024;
        // the addSemaphore counts the current number of things in the tasklist.
        SemaphoreSlim addSemaphore = new SemaphoreSlim(0,semaphoreMaxCount); //todo: consider using SemaphoreSlim for better performance


        int countdown = 0;
        public object Context;

        bool longRunning = false;

        public WorkQueue(Options options, object context)
        {
            longRunning = options.HasFlag(Options.LongRunning);
            if (options.HasFlag(Options.TimeAveraging))
                average = new TimeSpanAverager();
            
            Context = context;
        }

        public void EnqueueWork(Action f)
        {
            void threadGo()
            {
                var awaitArray = new WaitHandle[] { addSemaphore.AvailableWaitHandle, cancel.Token.WaitHandle };
                while (true)
                {
                    retry:
                    awaitArray[1] = cancel.Token.WaitHandle;
                    int thing = 0;
                    if(longRunning)
                        thing = WaitHandle.WaitAny(awaitArray);
                    else
                        thing = WaitHandle.WaitAny(awaitArray, Timeout);
                    if (thing == 0 && !addSemaphore.Wait(0))
                        goto retry;
                    bool ok = thing == 0;
                    
                    if (!ok)
                    {
                        if (cancel.IsCancellationRequested == false && longRunning) continue;
                        lock (threadCreationLock)
                        {

                            if (workItems.Count > 0)
                                goto retry;
                            threadCount--;
                            break;
                        }
                    }

                    
                    Action run = null;
                    while (!workItems.TryDequeue(out run))
                        Thread.Yield();
                    if (average != null)
                    {
                        var sw = Stopwatch.StartNew();
                        run();
                        average.PushTimeSpan(sw.Elapsed);
                    }
                    else
                    {
                        run();
                    }
                    Interlocked.Decrement(ref countdown);
                }
            }

            while (addSemaphore.CurrentCount >= semaphoreMaxCount - 10)
            {
                // #4246: this is incredibly rare, but can happen if millions of results are pushed at once.
                //        the solution is to just slow a bit down when it happens.
                //        100 ms sleep is OK, because it needs to do around 1M things before it's idle.
                TapThread.Sleep(100);
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
                        TapThread.Start(threadGo);
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
    }
}
