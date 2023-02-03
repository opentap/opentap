using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml.Serialization;

namespace OpenTap.Plugins.BasicSteps
{
    [Display("Sweep Parameter Range", "Ranged based sweep step that iterates value of its parameters based on a selected range.", "Flow Control")]
    [AllowAnyChild]
    public class SweepParameterRangeStep : SweepParameterStepBase
    {
        [Display("Start", Group:"Sweep", Order: -2, Description: "The parameter value where the sweep will start.")]
        public decimal SweepStart { get; set; }

        [Display("Stop",  Group:"Sweep",Order: -1, Description: "The parameter value where the sweep will stop.")]
        public decimal SweepStop { get; set; }

        [Browsable(false)]
        public decimal SweepEnd { get { return SweepStop; } set { SweepStop = value; } }
        
        [Display("Step Size",  Group:"Sweep", Order: 1, Description: "The value to be increased or decreased between every iteration of the sweep.")]
        [EnabledIf(nameof(SweepBehavior), SweepBehavior.Linear, HideIfDisabled = true)]
        public decimal SweepStep {
            get
            {
                if (SweepPoints == 0) return 0;
                if (SweepPoints == 1) return 0;
                return (SweepStop - SweepStart) / (SweepPoints - 1);
            }
            set
            {
                if (decimal.Zero == value) return;
                var newv = (uint)Math.Round((SweepStop - SweepStart) / value) + 1;
                SweepPoints = newv;
            }
        }

        [Display("Points",  Group:"Sweep",Order: 1, Description: "The number of points to sweep.")]
        public uint SweepPoints { get; set; }
        
        [Display("Behavior",  Group:"Sweep",Order: -3, Description: "Linear or exponential growth.")]
        public SweepBehavior SweepBehavior { get; set; }

        public override void PrePlanRun()
        {
            base.PrePlanRun();
            validateSweepMutex.WaitOne();
        }

        public override void PostPlanRun()
        {
            validateSweepMutex.ReleaseMutex();
            base.PostPlanRun();
        }

        public override IEnumerable<ParameterMemberData> AvailableParameters => base.AvailableParameters.Where(x => x.TypeDescriptor.IsNumeric());
        

        // Check if the test plan is running before validating sweeps.
        // the validateSweep might have been started before the plan started.
        // hence we use the validateSweepMutex to ensure that validation is done before 
        // the plan starts.
        bool isRunning => GetParent<TestPlan>()?.IsRunning ?? false;
        Mutex validateSweepMutex = new Mutex();
        string validateSweep(decimal Value)
        {   // Mostly copied from Run
            var props = AvailableParameters.ToArray();
            
            if (props.Length == 0) return "";
            if (isRunning) return ""; // Avoid changing the value during run when the gui asks for validation errors.
            if (!validateSweepMutex.WaitOne(0)) return "";
            var originalValues = props.Select(set => set.GetValue(this)).ToArray();
            try
            {
                var str = StringConvertProvider.GetString(Value, CultureInfo.InvariantCulture);

                foreach (var set in props)
                {
                    var val = StringConvertProvider.FromString(str, set.TypeDescriptor, this, CultureInfo.InvariantCulture);
                    set.SetValue(this, val);
                }

                return "";
            }
            catch (TargetInvocationException e)
            {
                return e.InnerException.Message;
            }
            finally
            {
                for (int i = 0; i < props.Length; i++)
                    props[i].SetValue(this, originalValues[i]);
                validateSweepMutex.ReleaseMutex();
            }
        }
        
        public SweepParameterRangeStep()
        {
            Name = "Sweep Range {Parameters}";

            SweepStart = 1;
            SweepStop = 100;
            SweepPoints = 100;

            Rules.Add(() => string.IsNullOrWhiteSpace(validateSweep(SweepStart)), () => validateSweep(SweepStart), "SweepStart");
            Rules.Add(() => string.IsNullOrWhiteSpace(validateSweep(SweepStop)), () => validateSweep(SweepStop), "SweepEnd");

            Rules.Add(() => ((SweepBehavior != SweepBehavior.Exponential)) || (SweepStart != 0), "Sweep start value must be non-zero.", "SweepStart");
            Rules.Add(() => ((SweepBehavior != SweepBehavior.Exponential)) || (SweepStop != 0), "Sweep end value must be non-zero.", "SweepEnd");
            Rules.Add(() => SweepPoints > 1, "Sweep points must be bigger than 1.", "SweepPoints");
            
            Rules.Add(() => ((SweepBehavior != SweepBehavior.Exponential)) || (Math.Sign(SweepStop) == Math.Sign(SweepStart)), "Sweep start and end value must have the same sign.", "SweepEnd", "SweepStart");
        }

        public static IEnumerable<decimal> LinearRange(decimal start,  decimal end, int points)
        {
            if (points == 0) yield break;
            for(int i = 0; i < points - 1; i++)
            {
                yield return (start * (points - 1- i) + end * (i)) / (points - 1);
            }
            yield return end;
        }

        public static IEnumerable<decimal> ExponentialRange(decimal start, decimal end, int points)
        {
            if (start == 0)
                throw new ArgumentException("Start value must be different than zero.");
            if (end == 0)
                throw new ArgumentException("End value must be different than zero.");
            if (Math.Sign(start) != Math.Sign(end))
                throw new ArgumentException("Start and end value must have the same sign.");
            
            var logs = Math.Log10((double) start);
            var loge = Math.Log10((double) end);
            return LinearRange((decimal)logs, (decimal)loge, points).Select(x => (decimal)Math.Pow(10, (double)x));
        }
        
        
        public override void Run()
        {
            base.Run();

            var selected = SelectedMembers.ToArray();
            var originalValues = selected.Select(set => set.GetValue(this)).ToArray();


            IEnumerable<decimal> range = LinearRange(SweepStart, SweepStop, (int)SweepPoints);

            if (SweepBehavior == SweepBehavior.Exponential)
                range = ExponentialRange(SweepStart, SweepStop, (int)SweepPoints);

            var disps = selected.Select(x => x.GetDisplayAttribute()).ToList();
            string names = string.Join(", ", disps.Select(x => x.Name));
            
            if (disps.Count > 1)
                names = string.Format("{{{0}}}", names);
            
            foreach (var Value in range)
            {
                var val = StringConvertProvider.GetString(Value, CultureInfo.InvariantCulture);
                foreach (var set in selected)
                {
                    try
                    {
                        var value = StringConvertProvider.FromString(val, set.TypeDescriptor, this, CultureInfo.InvariantCulture);
                        set.SetValue(this, value);
                    }
                    catch (TargetInvocationException ex)
                    {
                        Log.Error("Unable to set '{0}' to value '{2}': {1}", set.GetDisplayAttribute().Name, ex.InnerException.Message, Value);
                        Log.Debug(ex.InnerException);
                    }
                }
                // Notify that values might have changes
                OnPropertyChanged("");

                var AdditionalParams = new ResultParameters();
                
                foreach (var disp in disps)
                    AdditionalParams.Add(new ResultParameter(disp.Group.FirstOrDefault() ?? "", disp.Name, Value));

                Log.Info("Running child steps with {0} = {1} ", names, Value);

                var runs = RunChildSteps(AdditionalParams, BreakLoopRequested, throwOnBreak: false).ToArray();
                if (BreakLoopRequested.IsCancellationRequested) break;
                runs.ForEach(r => r.WaitForCompletion());
                if (runs.LastOrDefault()?.BreakConditionsSatisfied() == true)
                    break;
                
            }
            for (int i = 0; i < selected.Length; i++)
                selected[i].SetValue(this, originalValues[i]);
        }
    }
}
