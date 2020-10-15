using System;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace OpenTap.Plugins.BasicSteps
{
    [AllowAnyChild]
    [Display("Sweep Parameter", "Table based loop that sweeps the value of its parameters based on a set of values.", "Flow Control")]
    public class SweepParameterStep : SweepParameterStepBase
    {
        public bool SweepValuesEnabled => SelectedParameters.Count > 0;

        SweepRowCollection sweepValues = new SweepRowCollection();
        [DeserializeOrder(1)] // this should be deserialized as the last thing.
        [Display("Sweep Values", "A table of values to be swept for the selected parameters.", "Sweep")]
        [HideOnMultiSelect] // todo: In the future support multi-selecting this.
        [EnabledIf(nameof(SweepValuesEnabled), true)]
        [Unsweepable]
        public SweepRowCollection SweepValues 
        { 
            get => sweepValues;
            set
            {
                sweepValues = value;
                sweepValues.Loop = this;
            }
        }

        public SweepParameterStep()
        {
            SweepValues.Loop = this;
            Name = "Sweep {Parameters}";
            Rules.Add(() => string.IsNullOrWhiteSpace(validateSweepValues()), validateSweepValues, nameof(SweepValues));
        }

        private string validateSweepValues()
        {
            if (SweepValues.Count <= 0 || SweepValues.All(x => x.Enabled == false)) return "No values selected to sweep";

            if (SelectedParameters.Count <= 0)
            {
                SweepValues = new SweepRowCollection();
            }
            return "";
        }

        int iteration;
        
        [Output]
        [Display("Iteration", "Shows the iteration of the sweep that is currently running or about to run.", "Sweep", Order: 3)]
        public string IterationInfo => $"{iteration} of {SweepValues.Count(x => x.Enabled)}";

        public override void PrePlanRun()
        {
            base.PrePlanRun();
            iteration = 0;

            if (SelectedParameters.Count <= 0)
                throw new InvalidOperationException("No parameters selected to sweep");
            var errorStr = validateSweepValues();
            if (!string.IsNullOrWhiteSpace(errorStr))
                throw new InvalidOperationException(errorStr);
        }

        public override void Run()
        {
            base.Run();
            iteration = 0;
            var sets = SelectedMembers.ToArray();
            var originalValues = sets.Select(set => set.GetValue(this)).ToArray();

            var rowType = SweepValues.Select(TypeData.GetTypeData).FirstOrDefault();
            foreach (var Value in SweepValues)
            {
                if (Value.Enabled == false) continue;
                var AdditionalParams = new ResultParameters();

                
                foreach (var set in sets)
                {
                    var mem = rowType.GetMember(set.Name);
                    var value = mem.GetValue(Value);
                    
                    string valueString;
                    if (value == null)
                        valueString = "";
                    else if(false == StringConvertProvider.TryGetString(value, out valueString, CultureInfo.InvariantCulture))
                        valueString = value.ToString();
                    
                    var disp = mem.GetDisplayAttribute();
                    AdditionalParams.Add(new ResultParameter(disp.Group.FirstOrDefault() ?? "", disp.Name, valueString));
                    
                    try
                    {
                        set.SetValue(this, value);
                    }
                    catch (TargetInvocationException ex)
                    {
                        Log.Error("Unable to set '{0}' to value '{2}': {1}", set.GetDisplayAttribute().Name, ex?.InnerException?.Message, valueString);
                        Log.Debug(ex.InnerException);
                    }
                }

                iteration += 1;
                // Notify that values might have changes
                OnPropertyChanged("");
                
                 Log.Info("Running child steps with {0}", Value.GetIterationString());

                var runs = RunChildSteps(AdditionalParams, BreakLoopRequested).ToList();
                if (BreakLoopRequested.IsCancellationRequested) break;
                runs.ForEach(r => r.WaitForCompletion());
            }
            for (int i = 0; i < sets.Length; i++)
                sets[i].SetValue(this, originalValues[i]);
        } 
    }
}
