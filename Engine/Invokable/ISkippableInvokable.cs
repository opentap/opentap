namespace OpenTap
{
    /// <summary> Implements a check if an invocation can be skipped. This should only be implemented if it can be done very quickly. </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="T2"></typeparam>
    interface ISkippableInvokable<T, T2>
    {
        public bool Skip(T a, T2 b);
    }
}
