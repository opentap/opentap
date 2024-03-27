namespace OpenTap.Metrics
{
    /// <summary> Gives a short name based on metrics. </summary>
    public interface IStatusName
    {
        /// <summary> the 'status name'.</summary>
        string StatusName { get; }
    }
}