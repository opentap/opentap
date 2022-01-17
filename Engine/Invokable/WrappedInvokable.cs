namespace OpenTap
{
    /// <summary>  Wraps an IInvokable(T) in an IInvokable. </summary>
    class WrappedInvokable<T> : IWrappedInvokable, IWorkQueueIntrospectiveIInvokable 
    {
        public readonly T Arg;
        public IInvokable<T> Wrap;
        
        public WrappedInvokable(IInvokable<T> invokable, T argument)
        {
            Arg = argument;
            Wrap = invokable;
        }

        public void Invoke() => Wrap.Invoke(Arg);
        public object InnerInvokable => Wrap;
        public object Argument => Arg;
        public bool NeedsIntrospection => InnerInvokable is IWorkQueueIntrospectiveIInvokable i && i.NeedsIntrospection;
    }
}