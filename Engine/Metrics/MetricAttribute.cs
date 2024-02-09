using System;

namespace OpenTap.Metrics
{
    /// <summary> Defines a property as a metric. </summary>
    public class MetricAttribute : Attribute
    {
        /// <summary> Optionally, the name of the metric. </summary>
        public string Name { get; }

        /// <summary> Creates a new instance of the metric attribute </summary>
        /// <param name="name"></param>
        public MetricAttribute(string name)
        {
            Name = name;
        }

        /// <summary> Creates a new instance of the metric attribute.</summary>
        public MetricAttribute() : this(null)
        {
        }
    }
}