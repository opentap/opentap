using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    public interface IMetricUpdateCallback
    {
        void UpdateMetrics();
    }

    public interface IMetricConsumer
    {
        void OnMetricsUpdate(object subject);
    }

    public interface IStatusName
    {
        string StatusName { get; }
    }

    public class MetricAttribute : Attribute
    {
        public string Name { get; }

        public MetricAttribute(string name)
        {
            Name = name;
        }

        public MetricAttribute()
        {
            // inherit the member name from the object.
            Name = null;
        }
        
    }
    
    public static class Metrics
    {
        private static WeakHashSet<IMetricConsumer> consumers =
            new WeakHashSet<IMetricConsumer>(); 
        public static void RegisterMetricConsumer(IMetricConsumer consumer)
        {
            consumers.Add(consumer);
        }
        
        public static void NotifyMetricsChanged(object subject)
        {
            foreach (var consumer in consumers.GetElements())
            {
                consumer.OnMetricsUpdate(subject);
            }
        }

        public static ResultTable[] GetMetrics(object subject)
        {
            return GetMetrics((obj) => obj == subject);
        }
        
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