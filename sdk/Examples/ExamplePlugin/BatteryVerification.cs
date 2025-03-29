using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OpenTap.Plugins.ExamplePlugin
{
    [Display("Battery Charge")]
    public class BatteryChargeVerification : TestStep
    {
        
        [Browsable(true)]
        [Result]
        [Unit("A")]
        [Display("Max Charge Current", Group: "Results")]
        public double MaxChargeCurrent { get; private set; }
        
        [Browsable(true)]
        [Result]
        [Unit("J")]
        [Display("Max Capacity", Group: "Results")]
        public double MaxCapacity { get; private set; }
        
        [Browsable(true)]
        [Result]
        [Unit("C")]
        [Display("Average Temperature", Group: "Results")]
        public double AverageTemperature { get; private set; }
        
        public SimulatedPsuInstrument Psu { get; set; }
        public SimulatedBattery Battery { get; set; }
        
        public override void Run()
        {
            Psu.VoltageOn();
            try
            {
                Log.Debug("Charging...");
                List<double> temperature = new List<double>();
                List<double> maxCurrent = new List<double>();
                var charge = 0.0;
                for (int i = 0; i < 10; i++)
                {
                    TapThread.Sleep(100);
                    var voltage = Psu.MeasureVoltage();
                    var current = Psu.MeasureVoltage();
                    maxCurrent.Add(current);
                    temperature.Add(Battery.ReadTemperature());
                    charge = current * voltage * 0.1;
                }

                AverageTemperature = temperature.Average();
                MaxChargeCurrent = maxCurrent.Max();
                MaxCapacity = charge;
                UpgradeVerdict(Verdict.Pass);
            }
            finally
            {
                Psu.VoltageOff();
            }
            
        }
    }
    
    [Display("Battery Discharge")]
    public class BatteryDischargeVerification : TestStep
    {
        [Browsable(true)]
        [Result]
        [Unit("A")]
        [Display("Max Discharge Current", Group: "Results")]
        public double MaxDischargeCurrent { get; private set; }

        [Unit("A")]
        [Display("Max Discharge Current Lower Limit", Group: "Results")]
        public double MaxDischargeCurrentLowerLimit { get; set; } = 1.1;
        
        public SimulatedPsuInstrument Psu { get; set; }
        public SimulatedBattery Battery { get; set; }
        
        public override void Run()
        {
            Psu.VoltageOn();
            try
            {
                Log.Debug("Discharging...");;
                List<double> maxCurrent = new List<double>();
                for (int i = 0; i < 10; i++)
                {
                    TapThread.Sleep(100);
                    var current = Psu.MeasureVoltage();
                    maxCurrent.Add(current);
                }

                MaxDischargeCurrent = maxCurrent.Max();
                if(MaxDischargeCurrent < MaxDischargeCurrentLowerLimit)
                    UpgradeVerdict(Verdict.Fail);
                else
                    UpgradeVerdict(Verdict.Pass);
            }
            finally
            {
                Psu.VoltageOff();
            }
            
        }
    }

    public class SimulatedPsuInstrument : Instrument
    {
        public void VoltageOn()
        {
            
        }

        public void VoltageOff()
        {
            
        }

        public double MeasureCurrent()
        {
            return 1.0;
        }

        public double MeasureVoltage()
        {
            return 12.0;
        }
    }
    public class SimulatedBattery : Dut
    {
        public double ReadTemperature()
        {
            return 43.0;
        }
    }
}