//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Reflection;
using System.Xml.Serialization;
using System.Data;
using OpenTap;

namespace OpenTap.Plugins.BasicSteps
{
    [Display("Sweep Loop", Group:"Flow Control", Description: "Loops its child steps while sweeping specified parameters/settings on the child steps.")]
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
        [Display("Iteration", "Shows the iteration of the loop that is currently running or about to run.", Order: 4)]
        public string IterationInfo
        {
            get
            {
                var param = sweepParameters.FirstOrDefault();
                if (param == null || param.IsEnabled == null) return "0 of 0";
                return string.Format("{0} of {1}", Iteration + 1, param.IsEnabled.Length);
            }
        }

        internal int Iteration
        {
            get { return currentIteration; }
            set
            {
                if (currentIteration != value)
                {
                    currentIteration = value;
                    OnPropertyChanged("IterationInfo");
                }
            }
        }

        #region Settings

        List<SweepParam> sweepParameters = new List<SweepParam>();

        [Display("Sweep Parameters", Order: 1, Description: "Select the child steps parameters that you want to sweep and configure their values.")]
        public List<SweepParam> SweepParameters
        {
            get { return sweepParameters; }
            set
            {
                crossPlanSweepIndex = 0;
                Iteration = 0;

                sweepParameters = value;
                sanitizeSweepParams();
                OnPropertyChanged("SweepParameters");
            }
        }

        /// <summary>
        /// To ensure that TAP opens the referenced resources.
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
                else
                    return SweepParameters
                        .SelectMany(param => param.Values.Cast<object>())
                        .OfType<IResource>();
            }
        }

        [Display("Sweep Mode", Description: "Loop through the sweep values in a single TestPlan run or change values between runs.", Order:0)]
        public SweepBehaviour CrossPlan
        {
            get
            {
                return _CrossPlan;
            }
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
            Rules.Add(() => string.IsNullOrWhiteSpace(validateSweep()), validateSweep, "SweepParameters");

            ChildTestSteps.ChildStepsChanged += childStepsChanged;
            PropertyChanged += SweepLoop_PropertyChanged;
        }

        void SweepLoop_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "ChildTestSteps")
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
                Type childType = childTestStep.GetType();

                foreach (PropertyInfo property in sweepParameter.PropertyInfos)
                {
                    PropertyInfo paramProp = childType.GetProperty(property.Name);
                    try
                    {
                        if (paramProp == property)
                            paramProp.SetValue(childTestStep, val, null);
                    }catch(TargetInvocationException e)
                    {
                        if (ex == null)
                            ex = new List<Exception>();
                        ex.Add(e.InnerException);
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
        
        static Dictionary<SweepParam, List<ITestStep>> getTargetSteps(List<SweepParam> sweepParameters, TestStepList childTestSteps)
        {
            Dictionary<SweepParam, List<ITestStep>> o = new Dictionary<SweepParam, List<ITestStep>>();
            foreach (var sweepParam in sweepParameters)
                o[sweepParam] = new List<ITestStep>();
            var allsteps = Utils.FlattenHeirarchy(childTestSteps, x => x is TestPlanReference ? Enumerable.Empty<ITestStep>() : x.ChildTestSteps);
            var alltypes = allsteps.Select(x => x.GetType()).Distinct();
            var typeToStep = allsteps.ToLookup(x => x.GetType());
            foreach (var type in alltypes)
            {
                var steps = typeToStep[type];
                var applicableSweeps = sweepParameters.Where(x => x.PropertyInfos.Any(y => type.DescendsTo(y.DeclaringType))).ToArray();
                foreach (var sweep in applicableSweeps)
                    o[sweep].AddRange(steps);
            }
            return o;
        }

        public Dictionary<SweepParam, List<ITestStep>> GetTargetSteps()
        {
            return getTargetSteps(SweepParameters, ChildTestSteps);
        }

        Dictionary<ITestStep, Tuple<PropertyInfo, object>> getSweepParameterValues(SweepParam sweepParameter, TestStepList childTestSteps)
        {
            Dictionary<ITestStep, Tuple<PropertyInfo, object>> values = new Dictionary<ITestStep, Tuple<PropertyInfo, object>>();
            populateChildParameters(values, sweepParameter, childTestSteps);
            return values;
        }

        static void populateChildParameters(Dictionary<ITestStep, Tuple<PropertyInfo, object>> values, SweepParam sweepParameter, TestStepList childTestSteps)
        {
            foreach (ITestStep childTestStep in childTestSteps)
            {
                Type stepType = childTestStep.GetType();
                foreach (PropertyInfo property in sweepParameter.PropertyInfos)
                {
                    PropertyInfo paramProp = stepType.GetProperty(property.Name);
                    if (paramProp == property)
                        values[childTestStep] = new Tuple<PropertyInfo, object>(paramProp, paramProp.GetValue(childTestStep, null));
                }

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
                logMessage.Append(sweepParameter.Name);
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

        bool isRunning => StepRun != null;

        string validateSweep()
        {   // Mostly copied from Run
            if (SweepParameters.Count <= 0) return "";
            if (sweepParameters.All(p => p.Values.Length == 0)) return "No values selected to sweep";

            if (isRunning) return ""; // Avoid changing the value during run when the gui asks for validation errors.

            var oldParams = SweepParameters.Select(param => getSweepParameterValues(param, ChildTestSteps)).ToList();
            try
            {
                var affectedSteps = oldParams.SelectMany(d => d.Keys).Distinct().ToList();

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < SweepParameters[0].Values.Length; i++)
                {
                    if (false == SweepParameters.First().IsEnabled[i])
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
                        kvp.Value.Item1.SetValue(kvp.Key, kvp.Value.Item2, null);
            }
        }

        public string ValidateChildSteps(SweepParam sweepParameter, TestStepList childTestSteps)
        {
            foreach (ITestStep childTestStep in childTestSteps)
            {
                Type stepType = childTestStep.GetType();
                foreach (PropertyInfo property in sweepParameter.PropertyInfos)
                {
                    PropertyInfo paramProp = stepType.GetProperty(property.Name);
                    if (paramProp == property)
                        return paramProp.ToString();
                }

                if (false == stepType.DescendsTo(typeof(TestPlanReference)) && childTestStep.ChildTestSteps.Count > 0)
                {
                    var param = ValidateChildSteps(sweepParameter, childTestStep.ChildTestSteps);
                    if (param != null)
                        return param;
                }
            }

            return null;
        }
        
        public override void PrePlanRun()
        {
            if (sweepParameters == null)
                sweepParameters = new List<SweepParam>();

            if (sweepParameters.Count == 0)
                throw new ArgumentException("No parameters selected to sweep");
            if (sweepParameters[0].IsEnabled == null || sweepParameters[0].IsEnabled.Count(x => x) == 0)
                throw new ArgumentException("No values selected to sweep");
        }
        
        void printException(AggregateException ex)
        {
            foreach (var e in ex.InnerExceptions)
            {
                Log.Error(e.Message);
                Log.Debug(e);
            }       
        }

        public override void Run()
        {
            base.Run();
            if (SweepParameters.Count == 0) return;
            int valuesCount = SweepParameters[0].IsEnabled.Count(v => v);
            if (valuesCount == 0)
                return;

            if (CrossPlan == SweepBehaviour.Across_Runs)
            {
                var AdditionalParams = RegisterAdditionalParams(crossPlanSweepIndex);

                // loop until the parameters are enabled.
                while (false == SweepParameters.First().IsEnabled[crossPlanSweepIndex])
                {
                    crossPlanSweepIndex++;
                    if (crossPlanSweepIndex >= valuesCount)
                        crossPlanSweepIndex = 0;
                }

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
                if (crossPlanSweepIndex >= valuesCount)
                    crossPlanSweepIndex = 0;

                Iteration = crossPlanSweepIndex;
            }
            else
            {
                var oldParams = SweepParameters.Select(param => getSweepParameterValues(param, ChildTestSteps)).ToList();
                var affectedSteps = oldParams.SelectMany(d => d.Keys).Distinct().ToList();
                for (int i = 0; i < SweepParameters[0].Values.Length; i++)
                {
                    if (false == SweepParameters.First().IsEnabled[i])
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
                    runs.ForEach(r => r.WaitForCompletion(PlanRun.AbortToken));
                }

                Iteration = 0;

                // Restore values from before the run
                for (int i = 0; i < SweepParameters.Count; i++)
                    foreach (var kvp in oldParams[i])
                        kvp.Value.Item1.SetValue(kvp.Key, kvp.Value.Item2, null);
                affectedSteps.ForEach(step => step.OnPropertyChanged(""));
            }
        }

        /// <summary>
        /// Ensures that the sweep parameters exists within the child test steps and that SweepParam.Property is set.
        /// </summary>
        void sanitizeSweepParams()
        {
            if(SweepParameters != null)
                SweepParameters.RemoveIf(x => x.Type == null);
            List<SweepParam> itemsToRemove = null;
            List<SweepParam> itemsToAdd = null;
            var steps = Utils.FlattenHeirarchy(ChildTestSteps, step => step is TestPlanReference ? null : step.ChildTestSteps);
            var properties = steps
                .Select(s => s.GetType()).Distinct()
                .SelectMany(t => t.GetProperties()).Distinct()
                .Where(x => x.CanWrite && x.CanRead && x.GetSetMethod() != null && x.GetGetMethod() != null && x.IsBrowsable())
                .ToArray();
            // name -> property lookup
            var namePropLookup = properties.ToLookup(prop => Tuple.Create(prop.GetDisplayAttribute().GetFullName().Trim(), prop.PropertyType), prop => prop);
            var namePropLookupLegacy = properties.ToLookup(prop => Tuple.Create(prop.GetDisplayAttribute().Name.Trim(), prop.PropertyType), prop => prop);
            
            foreach(var sweepParam in SweepParameters)
            {
                var tuple = Tuple.Create(sweepParam.Name.Trim(), sweepParam.Type);
                if (namePropLookup.Contains(tuple))
                {
                    sweepParam.PropertyInfos = namePropLookup[tuple].ToArray();
                    continue;
                };
                if (namePropLookupLegacy.Contains(tuple))
                { // time to upgrade...
                    if (itemsToAdd == null) itemsToAdd = new List<SweepParam>();

                    var props = namePropLookupLegacy[tuple];
                    var modernized = props.GroupBy(prop => Tuple.Create(prop.GetDisplayAttribute().GetFullName(), prop.PropertyType));
                    if (itemsToRemove == null) itemsToRemove = new List<SweepParam>();
                    itemsToRemove.Add(sweepParam);
                    foreach (var grp in modernized)
                    {
                        try {
                            var newsweep = new SweepParam(grp);
                            newsweep.Resize(sweepParam.Values.Length);
                            if (newsweep.Type == sweepParam.Type)
                            {
                                Array.Copy(sweepParam.Values, newsweep.Values, newsweep.Values.Length);
                            }
                            else
                            {

                                for (int i = 0; i < sweepParam.Values.Length; i++)
                                    newsweep.Values.SetValue(Convert.ChangeType(sweepParam.Values.GetValue(i), newsweep.Type), i);
                            }

                            newsweep.PropertyInfos = grp.ToArray();
                            itemsToAdd.Add(newsweep);
                        }
                        catch
                        {

                        }
                    }
                }
            }

            var nullParameters = SweepParameters.Where(p => p.Property == null).ToArray();

            foreach (var param in nullParameters)
            {
                var name = param.PropertyNames.FirstOrDefault(x => namePropLookup.Contains(Tuple.Create(x, param.Type)));
                if (name != null)
                {
                    param.PropertyInfos = namePropLookup[Tuple.Create(name, param.Type)].ToArray();
                }
                else
                {
                    var tp = Tuple.Create(param.Name, param.Type);
                    name = namePropLookupLegacy.Contains(tp) ? namePropLookupLegacy[tp].FirstOrDefault().Name : null;
                    if (name == null)
                    {
                        if (itemsToRemove == null || itemsToRemove.Contains(param) == false)
                        {
                            Log.Warning("Unable to find a property for '{0}'. Removing from list.", param.Name);
                            if (itemsToRemove == null) itemsToRemove = new List<SweepParam>();
                            itemsToRemove.Add(param);
                        }
                    }
                    else
                    {
                        if (param.Property == null)
                        {
                            param.PropertyInfos = namePropLookupLegacy[Tuple.Create(name, param.Type)].ToArray();
                        }
                    }
                }
            }
            if (itemsToRemove != null && itemsToRemove.Count > 0)
            {
                foreach (var item in itemsToRemove)
                    sweepParameters.Remove(item);;
            }
            if (itemsToAdd != null && itemsToAdd.Count > 0)
            {
                foreach (var item in itemsToAdd)
                    sweepParameters.Add(item);
            }

            if (sweepParameters.Count != 0)
            {
                var maxCnt = sweepParameters.Max(x => x.IsEnabled != null ? Math.Max(x.IsEnabled.Length, x.Values.Length) : x.Values.Length);
                for (int i = 0; i < sweepParameters.Count; i++)
                {
                    if (sweepParameters[i].Values.Length < maxCnt)
                    {
                        sweepParameters[i].Resize(maxCnt);
                    }
                }
            }
        }

        public void OnDeserialized()
        {
            sanitizeSweepParams();
            if(sweepParameters.Any(x => x.IsEnabled == null || x.Values.Length != x.IsEnabled.Length))
            {
                Log.Warning("Sweep Loop: Conflicting settings detected. Setting Enabled to true for sweep elements where the data is missing.");
            }
            foreach (var sweepParam in sweepParameters)
            {   // fix IsEnabled if it is null or have different number of elements than sweepParam.Values.
                if (sweepParam.IsEnabled == null)
                {
                    sweepParam.IsEnabled = new bool[0];
                }
                if (sweepParam.IsEnabled.Length != sweepParam.Values.Length)
                {
                    int len = sweepParam.IsEnabled.Length;
                    var isEnabled = sweepParam.IsEnabled;
                    Array.Resize(ref isEnabled, sweepParam.Values.Length);
                    for (int i = len; i < isEnabled.Length; i++)
                        isEnabled[i] = true;
                    sweepParam.IsEnabled = isEnabled;
                }
            }
        }
    }

    public class SweepParam
    {
        public String Name { get; set; }
        public Array Values { get; set; }
        [XmlIgnore]
        public Type Type
        {
            get { return Values == null ? null : Values.GetType().GetElementType(); }
            internal protected set
            {
                if (Values != null && Values.GetType().GetElementType() != value)
                    throw new NotSupportedException("Cannot dynamically change type of Sweep Parameter");
                Values = Array.CreateInstance(value, 0);
            }
        }
        // TypeName useful for serialization.
        public string TypeName
        {
            get { return Type.FullName; }
            private set { Type = PluginManager.LocateType(value); }
        }

        /// <summary>
        /// Gets or sets if the parameter is enabled.
        /// </summary>
        public bool[] IsEnabled { get; set; }
        
        [XmlIgnore]
        public PropertyInfo[] PropertyInfos { get; set; }
        
        /// <summary>
        /// A list of the Properties that this parameters represents. 
        /// </summary>
        public string[] PropertyNames { get; set; }
        /// <summary>
        /// The property from where attributes are pulled.
        /// </summary>
        
        public PropertyInfo Property { get { return PropertyInfos == null ? null : PropertyInfos.FirstOrDefault(); } }

        /// <summary>
        /// Default constructor. Used by serializer.
        /// </summary>
        public SweepParam()
        {
        }

        /// <summary>
        /// Initializes a new instance of the SweepParam class. For use when programatically creating a <see cref="SweepLoop"/>
        /// </summary>
        /// <param name="prop">Property to sweep. This should be one of the properties on one of the childsteps of the <see cref="SweepLoop"/>.</param>
        public SweepParam(IEnumerable<PropertyInfo> props, params object[] values) : this()
        {
            if (props.Count() < 1)
                throw new ArgumentException("Must contain at least one PropertyInfo", "props");

            Type = props.First().PropertyType;
            if (!props.All(p => p.PropertyType == Type))
                throw new ArgumentException("All sweep properties must be of the same type", "props");

            Name = props.First().GetDisplayAttribute().GetFullName();
            PropertyInfos = props.Distinct().ToArray();
            PropertyNames = PropertyInfos.Select(prop => prop.GetDisplayAttribute().GetFullName()).ToArray();
            if (values != null && values.Length > 0)
            {
                Values = Array.CreateInstance(Type, values.Length);
                values.CopyTo(Values, 0);
                IsEnabled = values.Select(item => true).ToArray();
            }
        }

        public override string ToString()
        {
            return string.Format("{0} : {1}", Name, Type.Name);
        }

        public void Resize(int newCount)
        {
            if (Values.Length == newCount)
                return;
            var oldValues = Values;
            Values = Array.CreateInstance(Type, newCount);
            var copyAmount = Math.Min(newCount, oldValues.Length);
            Array.Copy(oldValues, Values, copyAmount);
            bool[] isEnabled = IsEnabled;
            Array.Resize(ref isEnabled, newCount);
            IsEnabled = isEnabled;
        }
    }
}
