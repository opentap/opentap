namespace OpenTap
{
    /// <summary> Similar to Action, but this can be implemented by other classes. </summary>
    interface IInvokable
    {
        void Invoke();
    }

    /// <summary>  Similar to Action(T), but this can be implemented by some class. </summary>
    interface IInvokable<T>
    {
        void Invoke(T a);
    }
    
    /// <summary> Similar to Action(T,T2), but this can be implemented by some class. </summary>
    interface IInvokable<T, T2>
    {
        void Invoke(T a, T2 b);
    }
}