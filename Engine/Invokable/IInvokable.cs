namespace OpenTap
{
    /// <summary> Similar to Action, but this can be implemented by other classes. </summary>
    interface IInvokable
    {
        void Invoke();
    }

    /// <summary>
    /// Similar to Action(T), but this can be implemented by some class.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    interface IInvokable<T>
    {
        void Invoke(T a);
    }
    /// <summary> Similar to Action(T,T2), but this can be implemented by some class. </summary>
    interface IInvokable<T, T2>
    {
        void Invoke(T a, T2 b);
    }

    /// <summary>
    /// Similar to IInvokable. This means that an IInvokable(T) has been wrapped in an IInvokable.
    /// </summary>
    interface IWrappedInvokable : IInvokable
    {
        /// <summary>  The inner IInvokable(T). </summary>
        object InnerInvokable { get; }
    }
}