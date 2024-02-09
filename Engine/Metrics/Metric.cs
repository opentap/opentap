using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap.Metrics
{
    /// <summary>
    /// Utility methods for metrics,.
    /// </summary>
    public static class Metric
    {
        private static readonly WeakHashSet<IMetricConsumer> _consumers =
            new WeakHashSet<IMetricConsumer>();

        /// <summary> Register a metric consumer. </summary>
        /// <param name="consumer"></param>
        public static void RegisterConsumer(IMetricConsumer consumer)
        {
            _consumers.Add(consumer);
        }

        /// <summary> Returns true if a metric has interest. </summary>
        public static bool HasInterest(MetricInfo metric) => interest.Contains(metric);
        
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
                    if(ComponentSettings.GetCurrent(type) is IMetricProducer producer)
                        producers.Add(producer);
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
                var metricsBySource = metricBySource.ToLookup(x => x.MetricGroupName);
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
            foreach (var consumer in _consumers.GetElements())
            {
                var thisInterest = consumer.GetInterest(columnMetrics);
                if(thisInterest.Any())
                    consumer.OnPushMetric(table);
            }
        }

        /// <summary> Poll metrics. </summary>
        public static void PollMetrics()
        {
            allMetrics = GetMetricInfos().ToArray();
            Dictionary<IMetricConsumer, MetricInfo[]> interestLookup = new Dictionary<IMetricConsumer, MetricInfo[]>();
            foreach (var consumer in _consumers.GetElements())
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
                foreach (var grp in consumer.Value.GroupBy(x => x.MetricGroupName))
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
         
        /// <summary> Get metric information from the system. </summary>
        public static MetricInfo GetMetricInfo(object source, string member)
        {
            var type = TypeData.GetTypeData(source);
            var mem = type.GetMember(member);
            if (mem != null && mem.GetAttribute<MetricAttribute>() is MetricAttribute metric)
            {
                return new MetricInfo(source, mem,
                    metric.Name ?? (source as IResource)?.Name ?? type.GetDisplayAttribute()?.Name);
            }

            return null;
        }
    }
}