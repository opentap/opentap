using System;
namespace OpenTap.Metrics
{
    /// <summary>  A metric. This can either be a DoubleMetric  or a BooleanMetric metric. </summary>
    public interface IMetric
    {
        /// <summary> The metric information. </summary>
        MetricInfo Info { get; }
        /// <summary> The value of the metric. </summary>
        object Value { get; }
        /// <summary> The time the metric was recorded. </summary>
        DateTime Time { get; }
    }
}
