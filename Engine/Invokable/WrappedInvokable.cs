namespace OpenTap
{
    /// <summary>  Wraps an IInvokable(T) in an IInvokable. </summary>
    class WrappedInvokable<T> : IWrappedInvokable 
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
    }
    /// <summary>  Wraps an IInvokable(T,T2) in an IInvokable. </summary>
    class WrappedInvokable<T, T2> : IWrappedInvokable 
    {
        public readonly T Arg;
        public readonly T2 Arg2;

        public IInvokable<T, T2> Wrap;
        
        public WrappedInvokable(IInvokable<T, T2> invokable, T argument1, T2 argument2)
        {
            Arg = argument1;
            Arg2 = argument2;
            Wrap = invokable;
        }
        public void Invoke() => Wrap.Invoke(Arg, Arg2);
        public object InnerInvokable => Wrap;
        public object Argument => Arg;
    }
}