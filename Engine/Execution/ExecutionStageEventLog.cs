//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Threading;

namespace OpenTap
{
    interface IStageExecutionEvent
    {
        IExecutionStage Stage { get; }
    }

    class CompletedEvent : IStageExecutionEvent
    {
        public IExecutionStage Stage { get; }

        public CompletedEvent(IExecutionStage stage)
        {
            Stage = stage;
        }
    }

    class FailedEvent : IStageExecutionEvent
    {
        public IExecutionStage Stage { get; }
        public Exception Exception { get; }

        public FailedEvent(IExecutionStage stage, Exception ex)
        {
            Stage = stage;
            Exception = ex;
        }
    }

    class ExecutionStageEventLog
    {
        List<IStageExecutionEvent> EventLog = new List<IStageExecutionEvent>();

        event Action<IStageExecutionEvent> EventLogged;

        object addLock = new object();

        public void Add(IStageExecutionEvent e)
        {
            lock (addLock)
            {
                EventLog.Add(e);
                if(EventLogged != null)
                    EventLogged(e);
            }
        }

        public int Subscribe(Action<IStageExecutionEvent> eventLogged)
        {
            lock (addLock)
            {
                EventLogged += eventLogged;
                return EventLog.Count;
            }
        }

        public IStageExecutionEvent this[int i]
        {
            get { return EventLog[i]; }
        }
    }

    class ExecutionStageEventLogReader
    {
        private ExecutionStageEventLog log;
        public int Position = 0;
        public SemaphoreSlim Queued;

        public ExecutionStageEventLogReader(ExecutionStageEventLog log)
        {
            this.log = log;
            Queued = new SemaphoreSlim(log.Subscribe(eventLogged));
        }

        private void eventLogged(IStageExecutionEvent obj)
        {
            Queued.Release();
        }

        public IStageExecutionEvent Read()
        {
            Queued.Wait();
            return log[Position++];
        }
    }
}
