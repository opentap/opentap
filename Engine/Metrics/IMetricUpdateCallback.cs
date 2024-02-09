namespace OpenTap.Metrics
{
    /// <summary> Defines a class which can update metrics. </summary>
    public interface IMetricUpdateCallback
    {
        /// <summary> Updates metrics. </summary>   
        void UpdateMetrics();
    }
}