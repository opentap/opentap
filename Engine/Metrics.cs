using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    /// <summary>
    /// Defines a class which can update metrics.
    /// </summary>
    public interface IMetricUpdateCallback
    {
        /// <summary> Updates metrics. </summary>
        void UpdateMetrics();
    }

    /// <summary> Defines that a class can consume metrics. </summary>
    public interface IMetricConsumer
    {
        /// <summary>
        /// Notifies the metric consumer that the subject has been updated.
        /// </summary>
        /// <param name="subject"></param>
        void OnMetricsUpdate(object subject);
    }

    /// <summary> Gives a short name based on metrics. </summary>
    public interface IStatusName
    {
        /// <summary> the 'status name'.</summary>
        string StatusName { get; }
    }

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
    
    /// <summary>
    /// Utility methods for metrics,.
    /// </summary>
    public static class Metrics
    {
        private static WeakHashSet<IMetricConsumer> consumers =
            new WeakHashSet<IMetricConsumer>();
        
        /// <summary> Register a metric consumer. </summary>
        /// <param name="consumer"></param>
        public static void RegisterMetricConsumer(IMetricConsumer consumer)
        {
            consumers.Add(consumer);
        }
        
        /// <summary>
        /// Notifies that a specific object with metrics has been updated.
        /// </summary>
        /// <param name="subject"></param>
        public static void NotifyMetricsChanged(object subject)
        {
            foreach (var consumer in consumers.GetElements())
            {
                consumer.OnMetricsUpdate(subject);
            }
        }

        /// <summary> Gets metrics from an object. </summary>
        public static ResultTable[] GetMetrics(object subject)
        {
            return GetMetrics((obj) => obj == subject);
        }
        
        /// <summary>  Gets all metrics in the system.  </summary>
        public static ResultTable[] GetMetrics(Func<object, bool> filter = null)
        {
            if (filter == null) filter = (obj) => true;
            List<ResultTable> results = new List<ResultTable>();
            foreach (IResource instr in InstrumentSettings.Current.Cast<IResource>().Concat(DutSettings.Current).Where(filter))
            {
                if (instr is IMetricUpdateCallback metricUpdate)
                {
                    metricUpdate.UpdateMetrics();
                }

                var td = TypeData.GetTypeData(instr);
                var memberGrp = td.GetMembers()
                    .Where(member => member.HasAttribute<MetricAttribute>())
                    .ToLookup(td => td.GetAttribute<MetricAttribute>().Name ?? instr.Name);
                foreach (var member in memberGrp)
                {
                    var columns = member.Select(mem =>
                    {
                        var parameters = new List<ResultParameter>();
                        if (mem.GetAttribute<UnitAttribute>() is UnitAttribute unit)
                        {
                            parameters.Add(new ResultParameter("Unit", unit.Unit));
                        }

                        var memValue = mem.GetValue(instr) ?? "";
                        Array array;
                        if (memValue is IConvertible)
                        {
                            array = Array.CreateInstance(memValue.GetType(), 1);
                        }
                        else
                        {
                            array = new string[1];
                            memValue = memValue.ToString();
                        }
                        
                        array.SetValue(memValue, 0);
                        
                        return new ResultColumn(mem.GetDisplayAttribute().Name, array, parameters.ToArray());
                    });
                    
                    var table = new ResultTable(td.GetDisplayAttribute().Name, columns.ToArray());
                    results.Add(table);
                }
            }

            return results.ToArray();
        }
    }
}