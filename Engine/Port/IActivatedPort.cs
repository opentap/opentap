namespace OpenTap
{
    /// <summary> Represents a port that can be activated.</summary>
    public interface IActivatedPort
    {
        /// <summary> Activates the port.</summary>
        void Activate();
        /// <summary> Returns true if the port is active. </summary>
        bool IsActive { get; }
    }
}
