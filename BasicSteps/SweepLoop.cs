//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Xml.Serialization;
using System.Threading;

namespace OpenTap.Plugins.BasicSteps
{
    [Display("Sweep Loop", Groups: new [] { "Flow Control", "Legacy" }, Description: "Loops its child steps while sweeping specified parameters/settings on the child steps.", Collapsed:true)]
    [AllowAnyChild]
    public class SweepLoop : LoopTestStep, IDeserializedCallback
    {

        public enum SweepBehaviour
        {
            [Display("Within Run")]
            Within_Run,
            [Display("Across Runs")]
            Across_Runs
        }

        private SweepBehaviour _CrossPlan;
        int crossPlanSweepIndex = 0;

        int currentIteration = 0;
        
        [Output]
        [Display("Iteration", "Shows the iteration of the loop that is currently running or about to run.", Order: 3)]
        public string IterationInfo => string.Format("{0} of {1}", Iteration + 1, EnabledRows.Length);

        [Browsable(false)]
        public bool[] EnabledRows { get; set; } = Array.Empty<bool>();

        internal int Iteration
        {
            get => currentIteration;
            set
            {
                if (currentIteration != value)
                {
                    currentIteration = value;
                    OnPropertyChanged(nameof(IterationInfo));
                }
            }
        }

        #region Settings

        public bool SweepParametersEnabled => sweepParameters.Count > 0;

        List<SweepParam> sweepParameters = new List<SweepParam>();
        [Unsweepable]
        [Display("Sweep Values", Order: 2, Description: "Select the ranges of values to sweep.")]
        [EnabledIf(nameof(SweepParametersEnabled), true)]
        [DeserializeOrder(1)]
        public List<SweepParam> SweepParameters
        {
            get => sweepParameters;
            set
            {
                crossPlanSweepIndex = 0;
                Iteration = 0;
                sweepParameters = value;
            }
        }

        int changedid = 0;
        internal void parametersChanged()
        {
            changedid += 1;
        }

        /// <summary> Quickly check if things has changed. </summary>
        /// <returns></returns>
        bool popChanged()
        {
            bool ischanged = false;
            foreach(var param in SweepParameters)
            {
                if(param.Step != this)
                {
                    param.Step = this;
                    ischanged = true;
                }
            }
            if(changedid > 0)
            {
                ischanged = true;
            }
            if(ischanged)
                changedid = 0;
            return ischanged;
        }

        [XmlIgnore]
        [Browsable(true)]
        [Unsweepable]
        [Display("Sweep Parameters", Order: 1, Description: "Select which child step settings to sweep.")]
        public IEnumerable<IMemberData> SweepMembers
        {
            get => SweepParameters.Select(x => x.Member);
            set { }
        }

        /// <summary>
        /// To ensure that OpenTAP opens the referenced resources.
        /// </summary>
        [Browsable(false)]
        public IEnumerable<IResource> ReferencedResources
        {
            get
            {
                if (CrossPlan == SweepBehaviour.Across_Runs)
                    return SweepParameters
                        .Select(param => param.Values.GetValue(crossPlanSweepIndex))
                        .OfType<IResource>();
                return SweepParameters
                    .SelectMany(param => param.Values)
                    .OfType<IResource>();
            }
        }

        [Display("Sweep Mode", Description: "Loop through the sweep values in a single TestPlan run or change values between runs.", Order:0)]
        [Unsweepable]
        public SweepBehaviour CrossPlan
        {
            get => _CrossPlan;
            
            set
            {

                if (_CrossPlan == value)
                    return;
                _CrossPlan = value;
                crossPlanSweepIndex = 0;
                Iteration = 0;
            }
        }
        #endregion

        public SweepLoop()
        {
            SweepParameters = new List<SweepParam>();
            Rules.Add(() => SweepMembers.Count() != 0, "No parameters selected to sweep", nameof(SweepMembers));
            Rules.Add(() => string.IsNullOrWhiteSpace(validateSweep()), validateSweep, nameof(SweepParameters));

            ChildTestSteps.ChildStepsChanged += childStepsChanged;
            PropertyChanged += SweepLoop_PropertyChanged;
        }

        void SweepLoop_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(ChildTestSteps))
                ChildTestSteps.ChildStepsChanged += childStepsChanged;
        }

        void childStepsChanged(TestStepList sender, TestStepList.ChildStepsChangedAction Action, ITestStep Object, int Index)
        {
            // Make a copy to stop collection modified exception in TapSerializer.
            var localSweepParameters = sweepParameters.ToList();
            localSweepParameters.RemoveIf(s => ValidateChildSteps(s, ChildTestSteps) == null);
            sweepParameters = localSweepParameters;
        }
        
        static List<Exception> setParameterOnChildren(SweepParam sweepParameter, object val, TestStepList childTestSteps)
        {
            List<Exception> ex = null;
            foreach (ITestStep childTestStep in childTestSteps)
            {
                ITypeData childType = TypeData.GetTypeData(childTestStep);

                foreach (var prop in sweepParameter.Members)
                {
                    IMemberData paramProp = childType.GetMember(prop.Name);
                    if (paramProp != null)
                    {
                        try
                        {
                            // Previously only the member name and type was used for this check
                            // this can however give some issue since the GUI groups things with
                            // the same name and group, hence this GetDisplayAttribute has been added
                            // Actually probably direct member comparison should have been done instead,
                            // but lots of test plans code depends on the functionality here, so just
                            // adding a comparison for display attribute is probably the most gentle solution.
                            var d1 = prop.GetDisplayAttribute();
                            var d2 = paramProp.GetDisplayAttribute();
                            if (d1.Name != d2.Name || d1.Group.SequenceEqual(d2.Group) == false)
                                continue;
                            if (Equals(paramProp.TypeDescriptor, sweepParameter.Member.TypeDescriptor) == false)
                                continue;
                            
                            var tp = TypeData.GetTypeData(val);
                            if ((tp.DescendsTo(paramProp.TypeDescriptor) || val == null))
                                paramProp.SetValue(childTestStep, val);
                        }
                        catch (TargetInvocationException e)
                        {
                            if (ex == null)
                                ex = new List<Exception>();
                            ex.Add(e.InnerException);
                        }
                    }

                    if (false == childType.DescendsTo(typeof(TestPlanReference)))
                    {
                        var e = setParameterOnChildren(sweepParameter, val, childTestStep.ChildTestSteps);
                        if(e != null)
                        {
                            if (ex == null) ex = e;
                            else ex.AddRange(e);
                        }
                    }
                }
            }
            return ex;
        }

        Dictionary<ITestStep, List<Tuple<IMemberData, object>>> getSweepParameterValues(SweepParam sweepParameter, TestStepList childTestSteps)
        {
            var values = new Dictionary<ITestStep, List<Tuple<IMemberData, object>>>();
            populateChildParameters(values, sweepParameter, childTestSteps);
            return values;
        }

        static void populateChildParameters(Dictionary<ITestStep, List<Tuple<IMemberData, object>>> values, SweepParam sweepParameter, TestStepList childTestSteps)
        {
            foreach (ITestStep childTestStep in childTestSteps)
            {
                ITypeData stepType = TypeData.GetTypeData(childTestStep);
                foreach (IMemberData property in sweepParameter.Members)
                {
                    IMemberData paramProp = stepType.GetMember(property.Name);
                    if (object.Equals(paramProp, property))
                    {
                        if (!values.TryGetValue(childTestStep, out _))
                            values[childTestStep] = new List<Tuple<IMemberData, object>>();
                        values[childTestStep].Add(new Tuple<IMemberData, object>(paramProp, paramProp.GetValue(childTestStep)));
                    }
                }
                // TODO: do we want this hack??
                if (false == stepType.DescendsTo(typeof(TestPlanReference)))
                    populateChildParameters(values, sweepParameter, childTestStep.ChildTestSteps);
                    
            }
        }

        void RunForSweepParamWithIndex(int index, StringBuilder logMessage)
        {
            List<Exception> ex = null;
            foreach (SweepParam sweepParameter in SweepParameters)
            {
                if (sweepParameter == null)
                    continue;
                object paramValue = sweepParameter.Values.GetValue(index);
                TestStepList childTestSteps = ChildTestSteps;
                
                var e = setParameterOnChildren(sweepParameter, paramValue, childTestSteps);
                if(e != null)
                {
                    if (ex == null) ex = new List<Exception>();
                    ex.AddRange(e);
                }
                if (logMessage == null) continue;
                logMessage.Append(sweepParameter.Member.Name);
                logMessage.Append(" = ");
                logMessage.Append(StringConvertProvider.GetString(sweepParameter.Values.GetValue(index)));
                if (sweepParameter != SweepParameters.Last())
                    logMessage.Append(", ");
            }
            if (ex != null)
                throw new AggregateException(ex);
        }

        private ResultParameters RegisterAdditionalParams(int Index)
        {
            ResultParameters parameters = new ResultParameters();
            foreach (SweepParam sweepParameter in SweepParameters)
            {
                if (sweepParameter == null)
                    continue;

                if (!(sweepParameter.Values.GetValue(Index) is IConvertible))
                    continue;

                string DisplayName = sweepParameter.Name;
                string group;

                DisplayName = ReflectionHelper.ParseDisplayname(DisplayName, out group);

                ResultParameter p = new ResultParameter(group, DisplayName, (IConvertible)sweepParameter.Values.GetValue(Index));
                parameters.Add(p);
            }
            return parameters;
        }

        // Check if the test plan is running before validating sweeps.
        // the validateSweep might have been started before the plan started.
        // hence we use the validateSweeepMutex to ensure that validation is done before 
        // the plan starts.
        bool isRunning => GetParent<TestPlan>()?.IsRunning ?? false;
        Mutex validateSweepMutex = new Mutex();
        string validateSweep()
        {   // Mostly copied from Run
            if (SweepParameters.Count <= 0) return "";
            if (sweepParameters.All(p => p.Values.Length == 0)) return "No values selected to sweep";
            if (isRunning) return ""; // Avoid changing the value during run when the gui asks for validation errors.
            if (!validateSweepMutex.WaitOne(0)) return "";
            sanitizeSweepParams();
            var oldParams = SweepParameters.Select(param => getSweepParameterValues(param, ChildTestSteps)).ToList();
            try
            {
                var affectedSteps = oldParams.SelectMany(d => d.Keys).Distinct().ToList();

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < SweepParameters[0].Values.Length; i++)
                {
                    if (false == EnabledRows[i])
                        continue;
                    RunForSweepParamWithIndex(i, null);
                    if (i > 0)
                        sb.AppendLine();
                    bool first = true;
                    foreach (var step in affectedSteps)
                    {
                        if (string.IsNullOrWhiteSpace(step.Error))
                            continue;
                        if (first)
                            first = false;
                        else
                            sb.AppendLine();
                        sb.AppendFormat("Row {2}, Step '{0}' : {1}", step.GetFormattedName(), step.Error, i + 1);
                    }
                    
                }
                return sb.ToString();
            }
            catch(AggregateException e)
            {
                return string.Join("\n", e.InnerExceptions.Select(x => x.Message));
            }
            finally
            {
                for (int i = 0; i < SweepParameters.Count; i++)
                    foreach (var kvp in oldParams[i])
                    {
                        foreach(var i2 in kvp.Value)
                            i2.Item1.SetValue(kvp.Key, i2.Item2);
                    }
                validateSweepMutex.ReleaseMutex();
            }
        }

        public string ValidateChildSteps(SweepParam sweepParameter, TestStepList childTestSteps)
        {
            if (sweepParameter.Members == null)
                return "";
            
            string iterate_child_steps(TestStepList stepList)
            {
                foreach (ITestStep childTestStep in stepList)
                {
                    if (childTestStep.IsReadOnly == false)
                    {
                        ITypeData stepType = TypeData.GetTypeData(childTestStep);
                        foreach (IMemberData property in sweepParameter.Members)
                        {
                            IMemberData paramProp = stepType.GetMember(property.Name);
                            if (object.Equals(paramProp, property))
                                return paramProp.ToString();
                        }
                    }
                    var param = ValidateChildSteps(sweepParameter, childTestStep.ChildTestSteps);
                    if (param != null)
                        return param;
                }
                return null;
            }

            return iterate_child_steps(childTestSteps);
        }
        
        public override void PrePlanRun()
        {
            validateSweepMutex.WaitOne();
            validateSweepMutex.ReleaseMutex();
            if (sweepParameters == null)
                sweepParameters = new List<SweepParam>();

            sanitizeSweepParams();

            if (sweepParameters.Count == 0)
                throw new InvalidOperationException("No parameters selected to sweep");
            if(EnabledRows.Count(x => x) == 0)
                throw new InvalidOperationException("No values selected to sweep");
        }
        
        void printException(AggregateException ex)
        {
            foreach (var e in ex.InnerExceptions)
            {
                Log.Error(e.Message);
                Log.Debug(e);
            }       
        }

        void acrossRunsGotoEnabledSweepIndex()
        {
            if (crossPlanSweepIndex >= EnabledRows.Length)
                crossPlanSweepIndex = 0;
            for(int i = 0; i < EnabledRows.Length; i++)
            {
                if (EnabledRows[crossPlanSweepIndex])
                    return;
                crossPlanSweepIndex++;
                if (crossPlanSweepIndex >= EnabledRows.Length)
                    crossPlanSweepIndex = 0;
            }
            throw new InvalidOperationException("No rows enabled!");
        }

        public override void Run()
        {
            base.Run();
            if (SweepParameters.Count == 0) return;
            if (EnabledRows.Count(v => v) == 0)
                return;

            if (CrossPlan == SweepBehaviour.Across_Runs)
            {
                var AdditionalParams = RegisterAdditionalParams(crossPlanSweepIndex);

                // loop until the parameters are enabled.
                acrossRunsGotoEnabledSweepIndex();
                
                StringBuilder logMessage = new StringBuilder("Setting sweep parameters for next run (");
                try
                {
                    RunForSweepParamWithIndex(crossPlanSweepIndex, logMessage);
                }
                catch (AggregateException ex)
                {
                    printException(ex);
                }
                logMessage.Append(")");
                Log.Info(logMessage.ToString());
                //Fix issue 687: Ensure the latest Sweep params are applied before running any child steps
                RunChildSteps(AdditionalParams, BreakLoopRequested);

                crossPlanSweepIndex++;
                acrossRunsGotoEnabledSweepIndex();

                Iteration = crossPlanSweepIndex;
            }
            else
            {
                var oldParams = SweepParameters.Select(param => getSweepParameterValues(param, ChildTestSteps)).ToList();
                var affectedSteps = oldParams.SelectMany(d => d.Keys).Distinct().ToList();
                for (int i = 0; i < SweepParameters[0].Values.Length; i++)
                {
                    if (false == EnabledRows[i])
                        continue;
                    Iteration = i;

                    StringBuilder logMessage = new StringBuilder("Running child steps with ");
                    try
                    {
                        RunForSweepParamWithIndex(i, logMessage);
                    }
                    catch (AggregateException ex)
                    {
                        printException(ex);
                    }
                    affectedSteps.ForEach(step => step.OnPropertyChanged(""));
                    var AdditionalParams = RegisterAdditionalParams(i);
                    Log.Info(logMessage.ToString());
                    var runs = RunChildSteps(AdditionalParams, BreakLoopRequested).ToList();
                    if (BreakLoopRequested.IsCancellationRequested) break;
                    runs.ForEach(r => r.WaitForCompletion());
                }

                Iteration = 0;

                // Restore values from before the run
                for (int i = 0; i < SweepParameters.Count; i++)
                    foreach (var kvp in oldParams[i])
                    {
                        foreach (var i2 in kvp.Value)
                            i2.Item1.SetValue(kvp.Key, i2.Item2);
                    }
                affectedSteps.ForEach(step => step.OnPropertyChanged(""));
            }
        }

        /// <summary>
        /// Ensures that the sweep parameters exists within the child test steps and that SweepParam.Property is set.
        /// </summary>
        internal void sanitizeSweepParams(bool log = true)
        {
            if (popChanged() == false)
            {
                return;
            }
            var steps = Utils.FlattenHeirarchy(ChildTestSteps, step => step is TestPlanReference ? null : step.ChildTestSteps);
            var superLookup = steps.Select(x => TypeData.GetTypeData(x)).Distinct()
                .SelectMany(tp => tp.GetMembers()).Distinct()
                .ToLookup(x => SweepParam.GetMemberName(x), x => x);
            
            foreach (var param in SweepParameters)
            {
                List<IMemberData> members = new List<IMemberData>();

                foreach (var name in param.MemberNames) {
                    var super = superLookup[name];
                    members.AddRange(super);   
                }
                param.Members = members.Distinct().ToArray();
                var prop = param.Members.FirstOrDefault();
                if (prop != null)
                {
                    var step = steps.FirstOrDefault(x => TypeData.GetTypeData(x).GetMember(prop.Name) == prop);
                    if (step != null)
                        param.DefaultValue = prop.GetValue(step);
                }
                else
                {
                    Log.Error("Members {0} not found", param.MemberNames);
                }

            }
            bool warn = false;
            int len = sweepParameters.Select(x => x.Values.Length).DefaultIfEmpty(0).Max();

            foreach (var sweepParam in sweepParameters)
            {   // fix IsEnabled if it is null or have different number of elements than sweepParam.Values.
                if(sweepParam.Values.Length != len)
                {
                    sweepParam.Resize(len);
                    warn = true;
                }
            }
            if(EnabledRows.Length != len)
            {
                int start = EnabledRows.Length;
                bool[] enabledrows = EnabledRows;
                Array.Resize(ref enabledrows, len);
                for (int i = start; i < len; i++)
                    enabledrows[i] = true;
                EnabledRows = enabledrows;
                warn = true;
            }
            if (warn && log)
            {
                Log.Error("Sweep Loop: Invalid settings detected. Please ensure that the sweep data is correct.");
            }
        }

        public void OnDeserialized()
        {
            sanitizeSweepParams();
        }
    }
}
