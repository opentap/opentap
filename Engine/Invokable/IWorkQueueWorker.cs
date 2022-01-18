using System;

namespace OpenTap
{
    interface IWorkQueueWorker
    {
    }

    interface IWorkQueueWorker<ContextType> : IWorkQueueWorker
    {
        void Invoke(ContextType context, WorkQueue queue);
    }

    class ActionWorkQueueWorker<T> : IWorkQueueWorker<T>
    {
        private readonly Action<T> action;

        public ActionWorkQueueWorker(Action<T> action)
        {
            this.action = action;
        }

        public void Invoke(T context, WorkQueue queue)
        {
            action.Invoke(context);
        }
    }
}