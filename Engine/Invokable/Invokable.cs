using System;

namespace OpenTap
{
    /// <summary> Action(T) IInvokable. </summary>
    readonly struct Invokable<T> : IInvokable<T>
    {
        readonly Action<T> action;
        public Invokable(Action<T> action) => this.action = action;

        public void Invoke(T v) => action(v);

        /// <summary> Add an ignored argument. </summary>
        public Invokable<T, T2> AddArg<T2>()
        {
            var act = action;
            return new Invokable<T, T2>((a1, a2) => act(a1));
        }
    }
    
    /// <summary> Action(T) IInvokable. </summary>
    readonly struct Invokable<T, T2> : IInvokable<T,T2>
    {
        readonly Action<T,T2> action;
        public Invokable(Action<T,T2> action) => this.action = action;

        public void Invoke(T v, T2 v2) => action(v, v2);
    }
}