using System;
namespace OpenTap.Metrics
{
    /// <summary>  A boolean metric. </summary>
    public readonly struct BooleanMetric : IMetric
    {
        /// <summary> The metric information. </summary>
        public MetricInfo Info { get; }
        /// <summary> The value of the metric. </summary>
        public bool Value { get; }
        /// <summary> The time the metric was recorded. </summary>
        public DateTime Time { get; }

        /// <summary> Creates a new instance of the boolean metric. </summary>
        public BooleanMetric(MetricInfo info, bool value, DateTime? time = null)
        {
            Info = info;
            Value = value;
            Time = time ?? DateTime.Now;   
        }
        
        /// <summary> Returns a string representation of the boolean metric. </summary>
        public override string ToString()
        {
            return $"{Info.MetricFullName}: {Value} at {Time}";
        }
        
        object IMetric.Value => Value;
    }
}
