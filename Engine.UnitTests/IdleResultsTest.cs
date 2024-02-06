using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;

namespace OpenTap.UnitTests
{
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
        }

        public class TestMetricsConsumer : IMetricConsumer
        {
            public void OnMetricsUpdate(object subject)
            {
                
            }
        }

        [Test]
        public void TestGetIdleResults()
        {
            using (Session.Create())
            {
                var consumer = new TestMetricsConsumer(); 
                Metrics.RegisterMetricConsumer(consumer);
                InstrumentSettings.Current.Clear();
                var instrTest = new IdleResultTestInstrument();
                
                InstrumentSettings.Current.Add(instrTest);
                var metrics = Metrics.GetMetrics();
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

            }
        }
    }
}