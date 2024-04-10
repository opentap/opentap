using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using OpenTap.Metrics;
namespace OpenTap.UnitTests
{
    [Display("Test Metric Producer")]
    public class TestMetricSource : IMetricSource
    {
        [Metric] [Unit("I")] public double X { get; private set; }

        [Metric]
        [Unit("V")]
        public double Y { get; private set; }

        private int _offset = 0;
        public void PushMetric()
        {
            var xMetric = MetricManager.GetMetricInfo(this, nameof(X));
            var yMetric = MetricManager.GetMetricInfo(this, nameof(Y));
            if (!MetricManager.HasInterest(xMetric)) return;
            var x = new List<double>();
            var y = new List<double>();
            for (int i = 0; i < 100; i++)
            {
                _offset += 1;
                X = _offset;
                MetricManager.PushMetric(xMetric, X);
                MetricManager.PushMetric(yMetric, Math.Sin(_offset * 0.1));
            }
        }   
    }
    
    [TestFixture]
    public class MetricManagerTest
    {   
        public class IdleResultTestInstrument : Instrument, IMetricUpdateCallback
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
            [Range(minimum:  0.0)]
            public int Test { get; private set; }

            public readonly int Count = 10;

            public void PushRangeValues()
            {
                var iMetric = MetricManager.GetMetricInfo(this, nameof(Test));
                if (MetricManager.HasInterest(iMetric) == false)
                    return;
                var lst = new List<int>();
                for (int i = 0; i < Count; i++)
                {
                    lst.Add(i);
                    Test++;
                    MetricManager.PushMetric(iMetric, Test);
                }
                
            }
            
        }

        public class TestMetricsListener : IMetricListener
        {
      
            public void Clear()
            {
                MetricValues.Clear();
            }

            public List<IMetric> MetricValues = new List<IMetric>();
            public void OnPushMetric(IMetric table)
            {
                MetricValues.Add(table);
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
                var metrics = MetricManager.GetMetricInfos().Select(x => x.Item1).ToArray();

                var testMetric = metrics.FirstOrDefault(m => m.MetricFullName == "INST / Test");
                Assert.IsNotNull(testMetric);
                var range = testMetric.Attributes.OfType<RangeAttribute>().FirstOrDefault();
                Assert.IsNotNull(range);
                Assert.IsTrue(range.Minimum == 0.0);
                
                Assert.IsTrue(metrics.Any(m => m.MetricFullName == "INST / v"));

                Assert.Contains("Test Metric Producer / Y", metrics.Select(m => m.MetricFullName).ToArray());
                InstrumentSettings.Current.Remove(instrTest);
                metrics = MetricManager.GetMetricInfos().Select(x => x.Item1).ToArray();
                
                Assert.IsFalse(metrics.Any(m => m.MetricFullName == "INST / v"));
            }
        }

        [Test]
        public void TestGetMetrics()
        {
            using (Session.Create())
            {
                InstrumentSettings.Current.Clear();
                var consumer = new TestMetricsListener(); 
                var instrTest = new IdleResultTestInstrument();
                
                InstrumentSettings.Current.Add(instrTest);
                var metricInfos = MetricManager.GetMetricInfos();
                foreach (var metric in metricInfos)
                {
                    consumer.MetricFilter.Add(metric.Item1);
                }
                
                MetricManager.RegisterConsumer(consumer);
                MetricManager.PollMetrics();
                

                instrTest.PushRangeValues();
                
                var results0 = consumer.MetricValues.ToArray();
                Assert.AreEqual(17, results0.Length);
                
                consumer.Clear();
                consumer.MetricFilter.Remove(consumer.MetricFilter.FirstOrDefault(x => x.Name == "Test"));
                MetricManager.PollMetrics();
                instrTest.PushRangeValues();
                var results2 = consumer.MetricValues.ToArray();
                Assert.AreEqual(6, results2.Length);
            }
        }
    }
}