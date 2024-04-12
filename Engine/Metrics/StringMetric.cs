using System;
namespace OpenTap.Metrics
{
    public class StringMetric : IMetric
    {

        public MetricInfo Info
        {
            get;
        }
        public object Value
        {
            get;
        }
        public DateTime Time
        {
            get;
        }
        
        /// <summary> Creates a new instance of the double metric. </summary>
        public StringMetric(MetricInfo info, string value, DateTime? time = null)
        {
            Value = value;
            Info = info;
            Time = time ?? DateTime.Now;
        }
    }
}
