using System;
using System.Collections.Concurrent;
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

    /// <summary>
    /// Information about a given metric,
    /// </summary>
    public class MetricInfo 
    {
        /// <summary>
        /// The source object of the metric.
        /// </summary>
        public readonly object Source;
        /// <summary>
        /// The metric member objectr.
        /// </summary>
        public IMemberData Member;
        /// <summary>
        /// The name of the metric group.
        /// </summary>
        public string MetricName { get; }

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
            MetricName = memberKey;
        }

        /// <summary>
        /// Provides name for the metric.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{MetricName}: {Member.Name}";

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
                return otherMetric.MetricName == MetricName && object.Equals(otherMetric.Member, Member) &&
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
                hashCode = (hashCode * 397) ^ (MetricName != null ? MetricName.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
    
    /// <summary> Defines that a class can consume metrics. </summary>
    public interface IMetricConsumer
    {
        /// <summary>
        /// Notifies the metric consumer that the subject has been updated.
        /// </summary>
        /// <param name="subject"></param>
        void OnMetricsUpdate(object subject);

        /// <summary>  Event occuring when a metric producer generates out-of-band metrics. </summary>
        void OnPushMetric(ResultTable table);

        /// <summary>
        /// Defines which in a list of metrics are the ones that have interest.
        /// </summary>
        /// <param name="allMetrics"></param>
        /// <returns></returns>
        IEnumerable<MetricInfo> GetInterest(IEnumerable<MetricInfo> allMetrics);
    }

    /// <summary> Marker interface for classes that produce metrics. </summary>
    public interface IMetricProducer
    {
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

        /// <summary>
        /// Returns true if a metric has interest.
        /// </summary>
        /// <param name="metric"></param>
        /// <returns></returns>
        public static bool MetricHasInterest(MetricInfo metric)
        {
            return interest.Contains(metric);
        }
        
        /// <summary> Get information about the metrics available to query. </summary>
        /// <returns></returns>
        public static IEnumerable<MetricInfo> GetMetricInfos()
        {
            var types = TypeData.GetDerivedTypes<IMetricProducer>().Where(x => x.CanCreateInstance);
            List<IMetricProducer> producers = new List<IMetricProducer>();
            foreach (var type in types.Select(t => t))
            {
                if (type.DescendsTo(typeof(ComponentSettings)))
                {
                    var item2 = ComponentSettings.GetCurrent(type) as IMetricProducer;
                    if(item2 != null)
                        producers.Add(item2);
                }
                else
                {
                    if (metricProducers.GetOrAdd(type, t => (IMetricProducer)t.CreateInstance()) is IMetricProducer m)
                        producers.Add(m);
                }
            }

            foreach (object instr in InstrumentSettings.Current.Cast<object>().Concat(DutSettings.Current)
                         .Concat(producers))
            {
            
                var td = TypeData.GetTypeData(instr);
                string name = (instr as IResource)?.Name ?? td.GetDisplayAttribute().Name;
                var memberGrp = td.GetMembers()
                    .Where(member => member.HasAttribute<MetricAttribute>())
                    .ToLookup(td => td.GetAttribute<MetricAttribute>().Name ?? name);
                foreach (var member in memberGrp)
                {
                    foreach(var mem in member)
                        yield return new MetricInfo(instr, mem, member.Key);
                }
            }
        }

        private static readonly ConcurrentDictionary<ITypeData, IMetricProducer> metricProducers =
            new ConcurrentDictionary<ITypeData, IMetricProducer>();

        /// <summary>  Gets all metrics in the system.  </summary>
        public static ResultColumn[] GetMetrics(Func<MetricInfo, bool> filter = null)
        {
            if (filter == null) filter = (info) => true;
            var providers = GetMetricInfos().Where(filter).ToArray();
            foreach (var cb in providers.Select(p => p.Source).OfType<IMetricUpdateCallback>())
            {
                cb.UpdateMetrics();
            }

            var results = new List<ResultColumn>();

            foreach (var metricBySource in providers.ToLookup(metric => metric.Source))
            {
                var obj = metricBySource.Key;

                var td = TypeData.GetTypeData(metricBySource.Key);
                string name = (obj as IResource)?.Name ?? td.GetDisplayAttribute()?.Name;
                var metricsBySource = metricBySource.ToLookup(x => x.MetricName);
                foreach (var sourceGroup in metricsBySource)
                {
                    var columns = sourceGroup.Select(mem =>
                    {
                        var parameters = new List<ResultParameter>();
                        if (mem.Member.GetAttribute<UnitAttribute>() is UnitAttribute unit)
                        {
                            parameters.Add(new ResultParameter("Unit", unit.Unit));
                        }

                        var memValue = mem.Member.GetValue(obj) ?? "";
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

                        return new ResultColumn(mem.Member.GetDisplayAttribute().Name, array, parameters.ToArray());
                    });

                    results.AddRange(columns);
                }
            }
            return results.ToArray();
        }

        private static MetricInfo[] allMetrics;
        private static HashSet<MetricInfo> interest = new HashSet<MetricInfo>();
        
        /// <summary>
        /// Push a metric,
        /// </summary>
        /// <param name="table"></param>
        /// <param name="columnMetrics"></param>
        /// <exception cref="ArgumentException"></exception>
        public static void PushMetric(ResultTable table, params MetricInfo[] columnMetrics)
        {
            if (table.Columns.Length != columnMetrics.Length)
                throw new ArgumentException("There has to be as many metrics as columns");
            foreach (var consumer in consumers.GetElements())
            {
                var thisInterest = consumer.GetInterest(columnMetrics);
                if(thisInterest.Any())
                    consumer.OnPushMetric(table);
            }
        }

        /// <summary>
        /// Poll metrics.
        /// </summary>
        public static void PollMetrics()
        {
            if (allMetrics == null)
                allMetrics = GetMetricInfos().ToArray();
            Dictionary<IMetricConsumer, MetricInfo[]> interestLookup = new Dictionary<IMetricConsumer, MetricInfo[]>();
            foreach (var consumer in consumers.GetElements())
            {
                interestLookup[consumer] = consumer.GetInterest(allMetrics).ToArray();
            }

            var interest2 = interestLookup.Values.SelectMany(x => x).Distinct().ToHashSet();
            interest = interest2;
            foreach (var producer in interest2.Select(x => x.Source).Distinct().OfType<IMetricUpdateCallback>())
            {
                producer.UpdateMetrics();
            }
            Dictionary<MetricInfo, object> metricValues = new Dictionary<MetricInfo, object>();
            foreach (var metric in interest2)
            {
                metricValues[metric] = metric.Member.GetValue(metric.Source);
            }



            foreach (var consumer in interestLookup)
            {
                foreach (var grp in consumer.Value.GroupBy(x => x.MetricName))
                {
                    var columns = grp.Select(mem =>
                    {
                        var parameters = new List<ResultParameter>();
                        if (mem.Member.GetAttribute<UnitAttribute>() is UnitAttribute unit)
                        {
                            parameters.Add(new ResultParameter("Unit", unit.Unit));
                        }

                        var memValue = metricValues[mem] ?? "";
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

                        return new ResultColumn(mem.Member.GetDisplayAttribute().Name, array, parameters.ToArray());
                    });
                    var table = new ResultTable(grp.Key, columns.ToArray());
                    consumer.Key.OnPushMetric(table);
                }
            }
        }
         
        /// <summary>
        /// Get metric information from the system.
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="yName"></param>
        /// <returns></returns>
        public static MetricInfo GetMetricInfo(object p0, string yName)
        {
            if(allMetrics == null)
                PollMetrics();
            return allMetrics?.FirstOrDefault(metric => metric.Source == p0 && metric.Member.Name == yName);
        }
    }
}