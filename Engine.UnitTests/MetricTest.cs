using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OpenTap.Metrics;
namespace OpenTap.UnitTests
{
    [Display("Test Metric Producer")]
    public class TestMetricProducer : IMetricProducer
    {
        [Metric] [Unit("I")] public double X { get; private set; }

        [Metric]
        [Unit("V")]
        public double Y { get; private set; }

        private int _offset = 0;
        public void PushMetric()
        {
            var xMetric = Metric.GetMetricInfo(this, nameof(X));
            var yMetric = Metric.GetMetricInfo(this, nameof(Y));
            if (!Metric.HasInterest(xMetric)) return;
            var x = new List<double>();
            var y = new List<double>();
            for (int i = 0; i < 100; i++)
            {
                _offset += 1;
                X = _offset;
                
                x.Add(_offset);
                y.Add(Y = Math.Sin(_offset * 0.1));
            }

            var table = new ResultTable("TestMetricProducer", new ResultColumn[]
            {
                new ResultColumn("X", x.ToArray()),
                new ResultColumn("Y", y.ToArray())
            });
            Metric.PushMetric(table, xMetric, yMetric);

        }   
    }
    
    [TestFixture]
    public class MetricTest
    {   
        public class IdleResultTestInstrument : Instrument, IMetricUpdateCallback, IStatusName
        {
            public IdleResultTestInstrument()
            {
               
            }

            public string StatusName => $"{Name}: {Voltage,2} V";
            
            readonly Stopwatch sw = Stopwatch.StartNew();
            
            [Browsable(true)]
            [Unit("V")]
            [Display("v", Group: "Metrics")]
            [Metric]
            public double Voltage { get; private set; }
            
            [Browsable(true)]
            [Unit("A")]
            [Display("I", Group: "Metrics")]
            [Metric]
            public double Current { get; private set; }
            
            public void UpdateMetrics()
            {
                Voltage = Math.Sin(sw.Elapsed.TotalSeconds * 100.0) + 2.5;
                Current = Math.Cos(sw.Elapsed.TotalSeconds * 100.0) * 0.1 + 1.5;
            }

            
            [Metric]
            [Unit("cm")]
            public int Test { get; private set; }

            public void PushRangeValues()
            {
                var iMetric = Metric.GetMetricInfo(this, nameof(Test));
                if (Metric.HasInterest(iMetric) == false)
                    return;
                var lst = new List<int>();
                for (int i = 0; i < 10; i++)
                {
                    lst.Add(i);
                    Test++;
                }
                Metric.PushMetric(new ResultTable(Name, new []{iMetric.CreateColumn(lst.ToArray())}), iMetric);
            }
            
        }

        public class TestMetricsConsumer : IMetricConsumer
        {
      
            public void Clear()
            {
                ResultTables.Clear();
            }

            public List<ResultTable> ResultTables = new List<ResultTable>();
            public void OnPushMetric(ResultTable table)
            {
                ResultTables.Add(table);
            }

            public HashSet<MetricInfo> MetricFilter { get; set; } = new HashSet<MetricInfo>();

            public IEnumerable<MetricInfo> GetInterest(IEnumerable<MetricInfo> allMetrics) => allMetrics.Where(MetricFilter.Contains);
        }

        [Test]
        public void TestMetricNames()
        {
            using (Session.Create())
            {
                InstrumentSettings.Current.Clear();
                var instrTest = new IdleResultTestInstrument();

                InstrumentSettings.Current.Add(instrTest);
                var metrics = Metric.GetMetricInfos();
                
                Assert.IsTrue(metrics.Any(m => m.MetricFullName == "INST / Metrics \\ v"));

                Assert.Contains("Test Metric Producer / Y", metrics.Select(m => m.MetricFullName).ToArray());
                InstrumentSettings.Current.Remove(instrTest);
                metrics = Metric.GetMetricInfos();
                
                Assert.IsFalse(metrics.Any(m => m.MetricFullName == "INST / Metrics \\ v"));
            }
        }

        [Test]
        public void TestGetMetrics()
        {
            using (Session.Create())
            {
                InstrumentSettings.Current.Clear();
                var consumer = new TestMetricsConsumer(); 
                var instrTest = new IdleResultTestInstrument();
                
                InstrumentSettings.Current.Add(instrTest);
                var metricInfos = Metric.GetMetricInfos();
                foreach (var metric in Metric.GetMetricInfos())
                {
                    consumer.MetricFilter.Add(metric);
                }
                
                Metric.RegisterConsumer(consumer);
                Metric.PollMetrics();
                

                instrTest.PushRangeValues();
                
                var results0 = consumer.ResultTables.ToArray();
                Assert.AreEqual(3, results0.Length);
                
                consumer.Clear();
                consumer.MetricFilter.Remove(consumer.MetricFilter.FirstOrDefault(x => x.Member.Name == "Test"));
                Metric.PollMetrics();
                instrTest.PushRangeValues();
                var results2 = consumer.ResultTables.ToArray();
                Assert.AreEqual(2, results2.Length);
            }
        }
    }
}