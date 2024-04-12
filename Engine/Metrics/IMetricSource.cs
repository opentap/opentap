using System.Collections.Generic;
namespace OpenTap.Metrics
{
    /// <summary> Marker interface for classes that produce metrics. </summary>
    public interface IMetricSource
    {
    }

    public interface IAdditionalMetricSources : IMetricSource
    {
        IEnumerable<MetricInfo> AdditionalMetrics { get; } 
    }
}