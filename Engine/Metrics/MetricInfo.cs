using System;
using System.Collections.Generic;

namespace OpenTap.Metrics
{
    /// <summary> Information about a given metric, </summary>
    public class MetricInfo 
    {
        /// <summary> The source object of the metric. </summary>
        public object Source { get; }
        /// <summary> The metric member object. </summary>
        public IMemberData Member { get; }
        
        /// <summary> The name of the metric group. </summary>
        public string MetricGroupName { get; }

        /// <summary> Gets the full name of the metric. </summary>
        public string MetricFullName => $"{MetricGroupName} / {Member.GetDisplayAttribute().Name}";
        
        /// <summary>
        /// creates an instance.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="mem"></param>
        /// <param name="memberKey"></param>
        public MetricInfo(object source, IMemberData mem, string memberKey)
        {
            Source = source;
            Member = mem;
            MetricGroupName = memberKey;
        }

        /// <summary>
        /// Provides name for the metric.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"Metric: {MetricFullName}";

        /// <summary>
        /// Creates a column.
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public ResultColumn CreateColumn(Array array)
        {
            var list = new List<IParameter>();
            if(Member.GetAttribute<UnitAttribute>() is UnitAttribute unit)
                list.Add(new ResultParameter("Unit", unit.Unit));
            
            return new ResultColumn(this.Member.GetDisplayAttribute().Name, array, list.ToArray());
        }

        /// <summary>
        /// Implements equality for metric info.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is MetricInfo otherMetric)
            {
                return otherMetric.MetricGroupName == MetricGroupName && object.Equals(otherMetric.Member, Member) &&
                       Source == otherMetric.Source;
            }

            return false;
        }
        
        /// <summary>
        /// Hash code for metrics.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Source != null ? Source.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Member != null ? Member.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (MetricGroupName != null ? MetricGroupName.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}