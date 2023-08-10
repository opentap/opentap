namespace OpenTap
{
    /// <summary>
    /// An IStartupInfo implementation logs some specific information about the current installation.
    /// </summary>
    public interface IStartupInfo : ITapPlugin
    {
        /// <summary>
        /// LogStartupInfo is called exactly once immediately after session logging has been initialized.
        /// </summary>
        void LogStartupInfo();
    }
}