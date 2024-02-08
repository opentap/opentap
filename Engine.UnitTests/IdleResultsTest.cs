using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace OpenTap.UnitTests
{
    public class TestMetricProducer : IMetricProducer
    {
        [Metric] [Unit("I")] public double X { get; private set; }

        [Metric]
        [Unit("V")]
        public double Y { get; private set; }

        private int _offset = 0;
        public void PushMetric()
        {
            var xMetric = Metrics.GetMetricInfo(this, nameof(X));
            var yMetric = Metrics.GetMetricInfo(this, nameof(Y));
            if (!Metrics.MetricHasInterest(xMetric)) return;
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
            Metrics.PushMetric(table, xMetric, yMetric);

        }
        
    }
    
    [TestFixture]
    public class IdleResultsTest
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

            public void InvokeMetricsDate()
            {
                UpdateMetrics();
                Metrics.NotifyMetricsChanged(this);
            }
            
            [Metric]
            [Unit("cm")]
            public int I { get; private set; }

            public void PushRangeValues()
            {
                var iMetric = Metrics.GetMetricInfo(this, nameof(I));
                if (Metrics.MetricHasInterest(iMetric) == false)
                    return;
                var lst = new List<int>();
                for (int i = 0; i < 10; i++)
                {
                    lst.Add(i);
                    I++;
                }
                Metrics.PushMetric(new ResultTable(Name, new []{iMetric.CreateColumn(lst.ToArray())}), iMetric);
            }
            
        }

        public class TestMetricsConsumer : IMetricConsumer
        {
            public void OnMetricsUpdate(object subject)
            {
                
            }
            StringBuilder sb = new StringBuilder();

            public string GetString() => sb.ToString();

            public void Clear()
            {
                sb.Clear();
            }
            
            
            public void OnPushMetric(ResultTable table)
            {
                
                sb.Append("=========== ");
                sb.Append(table.Name);
                sb.AppendLine(" =========== ");
                foreach (var column in table.Columns)
                {
                    string unit = "";
                    unit = column.Parameters["Unit"]?.ToString() ?? "";
                    sb.AppendFormat($"{column.Name} : {string.Join( ", ", column.Data.Cast<object>())} {unit}");
                    sb.AppendLine();
                }


                var text = sb.ToString();

            }

            public HashSet<MetricInfo> MetricFilter { get; set; } = new HashSet<MetricInfo>();

            public IEnumerable<MetricInfo> GetInterest(IEnumerable<MetricInfo> allMetrics) => allMetrics.Where(x => x.Member.Name != "I");
        }

        [Test]
        public void TestGetIdleResults()
        {
            using (Session.Create())
            {
                InstrumentSettings.Current.Clear();
                var consumer = new TestMetricsConsumer(); 
                var instrTest = new IdleResultTestInstrument();
                
                InstrumentSettings.Current.Add(instrTest);

                foreach (var metric in Metrics.GetMetricInfos())
                {
                    consumer.MetricFilter.Add(metric);
                }
                
                Metrics.RegisterMetricConsumer(consumer);
                Metrics.PollMetrics();
                

                instrTest.InvokeMetricsDate();
                instrTest.PushRangeValues();

                var str = consumer.GetString();
                consumer.Clear();
                consumer.MetricFilter.Remove(consumer.MetricFilter.FirstOrDefault(x => x.Member.Name == "I"));
                Metrics.PollMetrics();
                instrTest.PushRangeValues();
                var str2 = consumer.GetString();
                Assert.AreNotEqual(str, str2);
                /*var metrics = Metrics.GetMetrics();
                instrTest.InvokeMetricsDate();
                StringBuilder sb = new StringBuilder();
                foreach (var table in metrics)
                {
                    sb.Append("=========== ");
                    sb.Append(table.Name);
                    sb.AppendLine(" =========== ");
                    foreach (var column in table.Columns)
                    {
                        string unit = "";
                        unit = column.Parameters["Unit"]?.ToString() ?? "" ;
                        sb.AppendFormat($"{column.Name} : {column.Data.GetValue(0)} {unit}");
                        sb.AppendLine();
                    }
                }

                var text = sb.ToString();

                var producerMetrics = Metrics.GetMetrics((obj) => obj.Source is TestMetricProducer);
                Assert.AreEqual(1, producerMetrics.Length);*/

            }
        }
    }


    
}