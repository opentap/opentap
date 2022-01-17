using System;

namespace OpenTap
{
    /// <summary> Action(T) IInvokable. </summary>
    /// <typeparam name="T"></typeparam>
    class Invokable<T> : IInvokable<T>
    {
        readonly Action<T> action;
        public Invokable(Action<T> action) => this.action = action;

        public void Invoke(T v) => action(v);

        public static implicit operator Invokable<T>(Action<T> a) => new Invokable<T>(a);        
    }

    /// <summary>
    /// Action IInvokable.
    /// </summary>
    class Invokable : IInvokable
    {
        readonly Action action;
        public Invokable(Action action) => this.action = action;

        public void Invoke() => action();

        public static implicit operator Invokable(Action a) => new Invokable(a);
    }
}