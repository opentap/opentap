using System;
using System.Threading;

namespace OpenTap
{
    class Awaitable : IAsyncResult
    {
        readonly ManualResetEventSlim evt;
        public Awaitable(ManualResetEventSlim evt) => this.evt = evt;

        public object AsyncState { get; } = null;
        public WaitHandle AsyncWaitHandle => evt.WaitHandle;
        public bool CompletedSynchronously => false;
        public bool IsCompleted => evt.IsSet;
    }
}