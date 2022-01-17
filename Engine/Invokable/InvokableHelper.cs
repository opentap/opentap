namespace OpenTap
{
    static class InvokableHelper
    {
        /// <summary>
        /// Wraps an IInvokable(T) in an IInvokable.
        /// </summary>
        /// <param name="i"></param>
        /// <param name="argument"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IWrappedInvokable Wrap<T>(this IInvokable<T> i, T argument) =>  new WrappedInvokable<T>(i, argument);
    }
}