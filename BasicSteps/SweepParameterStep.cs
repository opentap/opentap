using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

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
        [Unsweepable, Unmergable]
        public SweepRowCollection SweepValues 
        { 
            get => sweepValues;
            set
            {
                sweepValues = value;
                sweepValues.Loop = this;
            }
        }


        /// <summary>
        /// This property declares to the Resource Manager which resources are declared by this test step. 
        /// </summary>
        [AnnotationIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public IEnumerable<IResource> Resources
        {
            get
            {
                foreach (var row in SweepValues)
                {
                    if (row.Enabled == false) continue;
                    foreach (var value in row.Values.Values)
                    {
                        if (value is IResource res)
                            yield return res;
                    }
                }
            }
        }
            
        public SweepParameterStep()
        {
            SweepValues.Loop = this;
            Name = "Sweep {Parameters}";
            Rules.Add(() => string.IsNullOrWhiteSpace(Validate()), Validate, nameof(SweepValues));
        }

        private string lastError = "";

        private string Validate()
        {
            if (!validateSweepMutex.WaitOne(0))
                return lastError;

            try
            {
                if (isRunning)
                    return lastError;

                if (SelectedParameters.Count <= 0)
                {
                    SweepValues = new SweepRowCollection();
                }
                {
                    // Remove sweep values not in the selected parameters. This can be needed when the user has removed some.

                    var rows = SweepValues.Select(x => x.Values);
                    var selectedNames = SelectedParameters.Select(x => x.Name);
                    var namesToRemove = rows.SelectMany(x => x.Keys).ToHashSet();
                    namesToRemove.ExceptWith(selectedNames);

                    foreach (var removeName in namesToRemove)
                    foreach (var row in rows)
                        row.Remove(removeName);
                }
                var errors = ValidateSweepValues();
                lastError = errors;
                return errors;
            }
            finally
            {
                validateSweepMutex.ReleaseMutex();
            }
        }

        int iteration;
        
        [Output]
        [Display("Iteration", "Shows the iteration of the sweep that is currently running or about to run.", "Sweep", Order: 3)]
        public string IterationInfo => $"{iteration} of {SweepValues.Count(x => x.Enabled)}";

        bool isRunning => GetParent<TestPlan>()?.IsRunning ?? false;
        
        /// <summary>
        /// Validation requires setting the values of parameterized members.
        /// This mutex will be locked while the test plan is running to prevent validation requests from interfering with test plan runs. 
        /// </summary>
        Mutex validateSweepMutex = new Mutex();

        private string ValidateSweepValues()
        {
            const int maxErrors = 10;
            var sets = SelectedMembers.ToArray();
            if (SweepValues.Count == 0)
                return "No rows selected to sweep.";
            var rowType = SweepValues.Select(TypeData.GetTypeData).FirstOrDefault();
            var sb = new StringBuilder();
            var numErrors = 0;

            string FormatError(string rowDescriptor, string error)
            {
                return $"{rowDescriptor}: {error}";
            }

            foreach (var set in sets)
            {
                var mem = rowType.GetMember(set.Name);

                // If an error is reported on all rows, only show one validation error
                var allRowsHaveErrors = true;
                var errorTuple = new List<(int row, string error)>();

                foreach (var (index, sweepValue) in SweepValues.WithIndex())
                {
                    if (sweepValue.Enabled == false) continue;

                    var stepsToValidate = new HashSet<ITestStep>();

                    var rowNumber = index + 1;
                    var value = mem.GetValue(sweepValue);

                    try
                    {
                        set.SetValue(this, value);
                        // Don't try to validate a step if SetValue failed
                        set.ParameterizedMembers.ForEach(m =>
                        {
                            if (m.Source is ITestStep step)
                                stepsToValidate.Add(step);
                        });
                    }
                    catch (TargetInvocationException ex)
                    {
                        var reason = ex?.InnerException?.Message;

                        string valueString;
                        if (value == null)
                            valueString = "";
                        else if (false == StringConvertProvider.TryGetString(value, out valueString,
                            CultureInfo.InvariantCulture))
                            valueString = value.ToString();

                        var error = $"Unable to set '{set.GetDisplayAttribute().Name}' to value '{valueString}'";

                        if (reason == null)
                            error += ".";
                        else
                            error += $": {reason}";

                        errorTuple.Add((rowNumber, error));
                        continue;
                    }

                    var hasErrors = false;

                    foreach (ITestStep step in stepsToValidate)
                    {
                        var errors = step.Error;
                        if (string.IsNullOrWhiteSpace(errors) == false)
                        {
                            errorTuple.Add((rowNumber, $"{step.GetFormattedName()} - {errors}"));
                            hasErrors = true;
                        }
                    }

                    if (hasErrors == false)
                        allRowsHaveErrors = false;
                }

                if (allRowsHaveErrors && errorTuple.Count > 1 &&
                    errorTuple.Select(t => t.error).Distinct().Count() == 1)
                {
                    var error = errorTuple.First();
                    sb.AppendLine(FormatError("All rows", error.error));
                    numErrors += 1;
                }
                else
                {
                    foreach (var error in errorTuple)
                    {
                        sb.AppendLine(FormatError($"Row {error.row}", error.error));
                        numErrors += 1;
                        if (numErrors >= maxErrors)
                            break;
                    }
                }

                if (numErrors >= maxErrors)
                    break;
            }

            return sb.ToString();
        }

        public override void PrePlanRun()
        {
            validateSweepMutex.WaitOne();
            base.PrePlanRun();
            iteration = 0;

            if (SelectedParameters.Count <= 0)
                throw new InvalidOperationException("No parameters selected to sweep");

            if (SweepValues.Count <= 0 || SweepValues.All(x => x.Enabled == false))
                throw new InvalidOperationException("No values selected to sweep");
        }

        public override void PostPlanRun()
        {
            validateSweepMutex.ReleaseMutex();
            base.PostPlanRun();
        }

        public override void Run()
        {
            base.Run();
            iteration = 0;
            var sets = SelectedMembers.ToArray();
            var originalValues = sets.Select(set => set.GetValue(this)).ToArray();

            var rowType = SweepValues.Select(TypeData.GetTypeData).FirstOrDefault();
            for (int i = 0; i < SweepValues.Count; i++)
            {
                SweepRow Value = SweepValues[i];
                if (Value.Enabled == false) continue;
                var AdditionalParams = new ResultParameters();


                foreach (var set in sets)
                {
                    var mem = rowType.GetMember(set.Name);
                    var value = mem.GetValue(Value);

                    string valueString;
                    if (value == null)
                        valueString = "";
                    else if (false == StringConvertProvider.TryGetString(value, out valueString,
                        CultureInfo.InvariantCulture))
                        valueString = value.ToString();

                    var disp = mem.GetDisplayAttribute();
                    AdditionalParams.Add(new ResultParameter(disp.Group.FirstOrDefault() ?? "", disp.Name,
                        valueString));

                    try
                    {
                        set.SetValue(this, value);
                    }
                    catch (TargetInvocationException ex)
                    {
                        throw new ArgumentException($"Unable to set '{set.GetDisplayAttribute()}' to '{valueString}' on row {i}: {ex.InnerException.Message}", ex.InnerException);
                    }
                }

                iteration += 1;
                // Notify that values might have changes
                OnPropertyChanged("");

                Log.Info("Running child steps with {0}", Value.GetIterationString());

                var runs = RunChildSteps(AdditionalParams, BreakLoopRequested, throwOnBreak: false).ToArray();
                if (BreakLoopRequested.IsCancellationRequested) break;
                runs.ForEach(r => r.WaitForCompletion());
                if (runs.LastOrDefault()?.BreakConditionsSatisfied() == true)
                    break;
            }

            for (int i = 0; i < sets.Length; i++)
                sets[i].SetValue(this, originalValues[i]);
        }
    }
}
