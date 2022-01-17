namespace OpenTap
{
    /// <summary>
    /// IInvokable that can introspect its current WorkQueue.
    /// </summary>
    interface IWorkQueueIntrospectiveIInvokable
    {
        // Whether it needs introspection or not.
        bool NeedsIntrospection { get; }
    }
}