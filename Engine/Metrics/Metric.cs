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
        public static bool HasInterest(MetricInfo metric) => _interest.Contains(metric);
        
        /// <summary> Get information about the metrics available to query. </summary>
        /// <returns></returns>
        public static IEnumerable<(MetricInfo metric, object source)> GetMetricInfos()
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
                    if (_metricProducers.GetOrAdd(type, t => (IMetricProducer)t.CreateInstance()) is IMetricProducer m)
                        producers.Add(m);
                }
            }

            foreach (var metricSource in InstrumentSettings.Current.Cast<object>().Concat(DutSettings.Current)
                         .Concat(producers))
            {

                var type1 = TypeData.GetTypeData(metricSource);
                
                string name = (metricSource as IResource)?.Name ?? type1.GetDisplayAttribute().Name;
                var memberGrp = type1.GetMembers()
                    .Where(member => member.HasAttribute<MetricAttribute>() && TypeIsSupported(member.TypeDescriptor))
                    .ToLookup(type2 => type2.GetAttribute<MetricAttribute>().Name ?? name);
                foreach (var member in memberGrp)
                {
                    foreach(var mem in member)
                        yield return (new MetricInfo(mem, member.Key), metricSource);
                }
            }
        }
        
        /// <summary> For now only double and bool type are supported. </summary>
        /// <param name="td"></param>
        /// <returns></returns>
        static bool TypeIsSupported(ITypeData td)
        {
            var type = td.AsTypeData().Type;
            return type == typeof(double) || type == typeof(bool) || type == typeof(int);
        }

        private static readonly ConcurrentDictionary<ITypeData, IMetricProducer> _metricProducers =
            new ConcurrentDictionary<ITypeData, IMetricProducer>();

        private static HashSet<MetricInfo> _interest = new HashSet<MetricInfo>();

        /// <summary> Push a double metric. </summary>
        public static void PushMetric(MetricInfo metric, double value)
        {
            PushMetric(new DoubleMetric(metric, value));
        }
        
        /// <summary> Push a boolean metric. </summary>
        public static void PushMetric(MetricInfo metric, bool value)
        {
            PushMetric(new BooleanMetric(metric, value));
        }
        
        /// <summary>
        /// Push a non-specific metric. This method is private to avoid pushing any kind of metric.
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        static void PushMetric(IMetric metric)
        {
            var metrics = new[]
            {
                metric.Info
            };
            foreach (var consumer in _consumers.GetElements())
            {
                var thisInterest = consumer.GetInterest(metrics);
                if (thisInterest.Any())
                    consumer.OnPushMetric(metric);
            }
        }
        
        static readonly TraceSource log = Log.CreateSource("Metric");

        /// <summary> Poll metrics. </summary>
        public static void PollMetrics()
        {
            var allMetrics = GetMetricInfos().ToArray();
            Dictionary<IMetricConsumer, MetricInfo[]> interestLookup = new Dictionary<IMetricConsumer, MetricInfo[]>();
            HashSet<MetricInfo> InterestMetrics = new HashSet<MetricInfo>();
            foreach (var consumer in _consumers.GetElements())
            {
                interestLookup[consumer] = consumer.GetInterest(allMetrics.Select(item => item.Item1)).ToArray();
                InterestMetrics.UnionWith(interestLookup[consumer]);
            }

            var interest2 = interestLookup.Values.SelectMany(x => x).Distinct().ToHashSet();
            _interest = interest2;
            foreach (var producer in allMetrics.Where(x => InterestMetrics.Contains(x.Item1)).Select(x => x.Item2).OfType<IMetricUpdateCallback>().Distinct())
            {
                producer.UpdateMetrics();
            }
            
            Dictionary<MetricInfo, IMetric> metricValues = new Dictionary<MetricInfo, IMetric>();
            foreach (var metric in allMetrics)
            {
                if(interest2.Contains(metric.Item1) == false)
                    continue;
                IMetric metricObject = null;
                var metricValue = metric.Item1.GetValue(metric.Item2);
                switch (metricValue)
                {
                    case bool v:
                        metricObject = new BooleanMetric( metric.Item1, v);
                        break;
                    case double v:
                        metricObject = new DoubleMetric( metric.Item1, v);
                        break;
                    case int v:
                        metricObject = new DoubleMetric( metric.Item1, v);
                        break;
                    default:
                        log.ErrorOnce(metric, "Metric value is not a supported type: {0} of type {1}",  metric.Item1.Name, metricValue?.GetType().Name ?? "null");
                        break;
                }
                if (metricObject != null)
                {
                    metricValues[ metric.Item1] = metricObject;
                }
            }

            foreach (var consumerInterest in interestLookup)
            {
                var consumer = consumerInterest.Key;
                foreach (var metric in consumerInterest.Value)
                {
                    if (metricValues.TryGetValue(metric, out var metricValue))
                    {
                        consumer.OnPushMetric(metricValue);
                    }
                }
            }
        }
         
        /// <summary> Get metric information from the system. </summary>
        public static MetricInfo GetMetricInfo(object source, string member)
        {
            var type = TypeData.GetTypeData(source);
            var mem = type.GetMember(member);
            if (mem?.GetAttribute<MetricAttribute>() is MetricAttribute metric)
            {
                if (TypeIsSupported(mem.TypeDescriptor))
                {
                    return new MetricInfo(mem,
                        metric.Name ?? (source as IResource)?.Name ?? type.GetDisplayAttribute()?.Name);
                }
            }
            return null;
        }
    }
}