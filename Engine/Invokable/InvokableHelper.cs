namespace OpenTap
{
    static class InvokableHelper
    {
        /// <summary> Allows wraping an IInvokable(T) in an IInvokable by providing the value for the T argument </summary>
        public static IWrappedInvokable Wrap<T>(this IInvokable<T> i, T argument) =>  new WrappedInvokable<T>(i, argument);
        /// <summary> Allows wraping an IInvokable(T1, T2) in an IInvokable by providing values for the T1 and T2 arguments </summary>
        public static IWrappedInvokable Wrap<T, T2>(this IInvokable<T, T2> i, T argument, T2 arg2) =>  new WrappedInvokable<T, T2>(i, argument, arg2);
    }
}