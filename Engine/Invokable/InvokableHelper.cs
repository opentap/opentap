namespace OpenTap
{
    static class InvokableHelper
    {
        /// <summary> Wraps an IInvokable(T) in an IInvokable. </summary>
        public static IWrappedInvokable Wrap<T>(this IInvokable<T> i, T argument) =>  new WrappedInvokable<T>(i, argument);
        /// <summary> Wraps an IInvokable(T, T2) in an IInvokable. </summary>
        public static IWrappedInvokable Wrap<T, T2>(this IInvokable<T, T2> i, T argument, T2 arg2) =>  new WrappedInvokable<T, T2>(i, argument, arg2);
    }
}