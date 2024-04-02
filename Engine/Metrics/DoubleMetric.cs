using System;
using System.Linq;
namespace OpenTap.Metrics
{
    /// <summary>  A double metric. </summary>
    public readonly struct DoubleMetric : IMetric
    {
        /// <summary> The metric information. </summary>
        public MetricInfo Info { get; }
        
        /// <summary> The value of the metric. </summary>
        public double Value { get; }
        
        /// <summary> The time the metric was recorded. </summary>
        public DateTime Time { get; }
        
        /// <summary> The unit of the metric. May be left empty.. </summary>
        public string Unit { get; }

        /// <summary> Creates a new instance of the double metric. </summary>
        public DoubleMetric(MetricInfo info, double value, DateTime? time = null)
        {
            Value = value;
            Info = info;
            Time = time ?? DateTime.Now;
            Unit = Info.Attributes.OfType<UnitAttribute>().FirstOrDefault()?.Unit;
        }
        
        /// <summary> Returns a string representation of the double metric. </summary>
        public override string ToString()
        {
            return $"{Info.MetricFullName}: {Value} {Unit} at {Time}";
        }

        object IMetric.Value => Value;
    }
}
