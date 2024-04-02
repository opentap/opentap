using System;
using System.Collections.Generic;

namespace OpenTap.Metrics
{
    /// <summary> Information about a given metric, </summary>
    public class MetricInfo 
    {
        
        /// <summary> The metric member object. </summary>
        IMemberData Member { get; }

        /// <summary> The attributes of the metric. </summary>
        public IEnumerable<object> Attributes => Member.Attributes;
        
        /// <summary> The name of the metric group. </summary>
        public string GroupName { get; }

        /// <summary> Gets the full name of the metric. </summary>
        public string MetricFullName => $"{GroupName} / {Member.GetDisplayAttribute().Name}";
         
        /// <summary> The name of the metric. </summary>
        public string Name => Member.Name;

        /// <summary> Creates a new instance of the metric info. </summary>
        /// <param name="mem">The metric member object.</param>
        /// <param name="groupName">The name of the metric group.</param> 
        public MetricInfo(IMemberData mem, string groupName)
        {
            Member = mem;
            GroupName = groupName;
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
                return otherMetric.GroupName == GroupName && object.Equals(otherMetric.Member, Member);
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
                var hashCode = 0;
                hashCode = (hashCode * 397) ^ (Member != null ? Member.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (GroupName != null ? GroupName.GetHashCode() : 0);
                return hashCode;
            }
        }
        
        /// <summary> Gets the value of the metric. </summary>
        public object GetValue(object metricSource)
        {
            if (Member != null)
                return Member.GetValue(metricSource);
            return null;
        }
    }
}