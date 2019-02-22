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
using System.Collections;

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

        public bool SweepParametersEnabled => sweepParameters.Count > 0;

        List<SweepParam> sweepParameters = new List<SweepParam>();

        [Display("Sweep Parameters", Order: 1, Description: "Select the child steps parameters that you want to sweep and configure their values.")]
        [EnabledIf(nameof(SweepParametersEnabled), true)]
        [DelayDeserialize]
        public List<SweepParam> SweepParameters
        {
            get { return sweepParameters; }
            set
            {
                crossPlanSweepIndex = 0;
                Iteration = 0;

                sweepParameters = value;
                //sanitizeSweepParams();
                OnPropertyChanged("SweepParameters");
            }
        }

        [XmlIgnore]
        [Browsable(true)]
        public IEnumerable<IMemberInfo> SweepMembers
        {
            //TODO: remove this placeholder, when we can add properties dynamically.
            get => Enumerable.Empty<IMemberInfo>();
            set {
                
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
            //Rules.Add(() => string.IsNullOrWhiteSpace(validateSweep()), validateSweep, nameof(SweepParameters));

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
                ITypeInfo childType = TypeInfo.GetTypeInfo(childTestStep);

                foreach (var prop in sweepParameter.PropertyInfos)
                {
                    IMemberInfo paramProp = childType.GetMember(prop.Name);
                    try
                    {
                        if (object.Equals(paramProp, sweepParameter.Property))
                            paramProp.SetValue(childTestStep, val);
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

        Dictionary<ITestStep, Tuple<IMemberInfo, object>> getSweepParameterValues(SweepParam sweepParameter, TestStepList childTestSteps)
        {
            Dictionary<ITestStep, Tuple<IMemberInfo, object>> values = new Dictionary<ITestStep, Tuple<IMemberInfo, object>>();
            populateChildParameters(values, sweepParameter, childTestSteps);
            return values;
        }

        static void populateChildParameters(Dictionary<ITestStep, Tuple<IMemberInfo, object>> values, SweepParam sweepParameter, TestStepList childTestSteps)
        {
            foreach (ITestStep childTestStep in childTestSteps)
            {
                ITypeInfo stepType = TypeInfo.GetTypeInfo(childTestStep);
                foreach (IMemberInfo property in sweepParameter.PropertyInfos)
                {
                    IMemberInfo paramProp = stepType.GetMember(property.Name);
                    if (object.Equals(paramProp, property))
                        values[childTestStep] = new Tuple<IMemberInfo, object>(paramProp, paramProp.GetValue(childTestStep));
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
                        kvp.Value.Item1.SetValue(kvp.Key, kvp.Value.Item2);
            }
        }

        public string ValidateChildSteps(SweepParam sweepParameter, TestStepList childTestSteps)
        {
            if (sweepParameter.PropertyInfos == null)
                return "";
            
            string iterate_child_steps(TestStepList stepList)
            {
                foreach (ITestStep childTestStep in stepList)
                {
                    if (childTestStep.IsReadOnly == false)
                    {
                        ITypeInfo stepType = TypeInfo.GetTypeInfo(childTestStep);
                        foreach (IMemberInfo property in sweepParameter.PropertyInfos)
                        {
                            IMemberInfo paramProp = stepType.GetMember(property.Name);
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
            if (sweepParameters == null)
                sweepParameters = new List<SweepParam>();

            if (sweepParameters.Count == 0)
                throw new InvalidOperationException("No parameters selected to sweep");
            if (sweepParameters[0].IsEnabled == null || sweepParameters[0].IsEnabled.Count(x => x) == 0)
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
                    runs.ForEach(r => r.WaitForCompletion());
                }

                Iteration = 0;

                // Restore values from before the run
                for (int i = 0; i < SweepParameters.Count; i++)
                    foreach (var kvp in oldParams[i])
                        kvp.Value.Item1.SetValue(kvp.Key, kvp.Value.Item2);
                affectedSteps.ForEach(step => step.OnPropertyChanged(""));
            }
        }

        /// <summary>
        /// Ensures that the sweep parameters exists within the child test steps and that SweepParam.Property is set.
        /// </summary>
        void sanitizeSweepParams()
        {
            var steps = Utils.FlattenHeirarchy(ChildTestSteps, step => step is TestPlanReference ? null : step.ChildTestSteps);
            var superLookup = steps.Select(x => new { Type = TypeInfo.GetTypeInfo(x), Step = x })
                .SelectMany(tp => tp.Type.GetMembers().Select(mem => new { Step = tp.Step, Member = mem }))
                .ToLookup(x => x.Member.GetDisplayAttribute().GetFullName().Trim(), x => x);
            
            foreach (var param in SweepParameters)
            {
                var super = superLookup[param.Name.Trim()];
                param.PropertyInfos = super.Select(x => x.Member).ToArray();
                param.DefaultValue = super.Select(x => x.Member.GetValue(x.Step)).FirstOrDefault();
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

    public class BasicStepsAnnotator : IAnnotator
    {
        class SweepParamsAnnotation : IMultiSelect, IAvailableValuesAnnotation, IOwnedAnnotation
        {
            [Display("Select All")]
            class AllItems
            {
                public override string ToString()
                {
                    return "Select All";
                }
            }

            object[] selectedValues;


            IEnumerable available = Array.Empty<object>();
            public IEnumerable AvailableValues => available;

            public IEnumerable Selected
            {
                get => selectedValues;
                set => selectedValues = value.Cast<object>().ToArray();
            }

            public void Read(object source)
            {
                List<object> allItems = new List<object>();
                allItems.Add(new AllItems());
                available = allItems;

                Dictionary<IMemberInfo, object> members = new Dictionary<IMemberInfo, object>();
                getPropertiesForItem((ITestStep)source, members);
                foreach(var member in members)
                {
                    var anot = AnnotationCollection.Create(null, member.Key);
                    var access = anot.Get<ReadOnlyMemberAnnotation>();
                    var rmember = anot.Get<IMemberAnnotation>().Member;
                    if (rmember != null && rmember.Writable)
                    {
                        if (access == null || (!access.IsReadOnly && access.IsVisible))
                        {
                            bool? browsable = rmember.GetAttribute<BrowsableAttribute>()?.Browsable;
                            if (browsable == false) continue;
                            if(browsable == null)
                            {
                                if (rmember.GetAttribute<XmlIgnoreAttribute>() != null)
                                    continue;
                            }
                            allItems.Add(member.Key);
                        }
                    }
                }

                foreach(var swe in (source as SweepLoop).SweepParameters)
                {
                    if (members.TryGetValue(swe.Property, out object value))
                        swe.DefaultValue = value;
                }

                Selected = (source as SweepLoop).SweepParameters.Select(x => x.Property).ToArray();
            }

            void getPropertiesForItem(ITestStep step, Dictionary<IMemberInfo, object> members)
            {
                foreach (ITestStep cs in step.ChildTestSteps)
                {
                    foreach(var member in TypeInfo.GetTypeInfo(cs).GetMembers())
                    {
                        if (members.ContainsKey(member) == false)
                        {
                            members.Add(member, member.GetValue(cs));
                        }
                    }
                    getPropertiesForItem(cs, members);
                }
            }

            public void Write(object source)
            {
                var otherMember = fac.ParentAnnotation.Get<IMembersAnnotation>().Members.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member.Name == nameof(SweepLoop.SweepParameters));
                var sweepPrams = otherMember.Get<IObjectValueAnnotation>().Value as List<SweepParam>;
                HashSet<SweepParam> found = new HashSet<SweepParam>();
                int initCount = sweepPrams.FirstOrDefault()?.Values.Length ?? 0;
                Dictionary<IMemberInfo, object> members = null;
                foreach (var _mem in selectedValues)
                {
                    if (_mem is AllItems) continue;
                    var mem = (IMemberInfo)_mem;

                    var existing = sweepPrams.Find(x => x.PropertyInfos.Contains(mem));
                    if (existing != null)
                    {
                        found.Add(existing);
                        continue;
                    }
                    var new2 = new SweepParam(new IMemberInfo[] { mem });
                    found.Add(new2);
                    if (members == null)
                    {
                        members = new Dictionary<IMemberInfo, object>();
                        getPropertiesForItem((TestStep)source, members);
                    }
                    if (members.TryGetValue(mem, out object val))
                        new2.DefaultValue = val;
                    sweepPrams.Add(new2);
                    if(new2.Values.Length != initCount)
                        new2.Resize(initCount);
                }
                sweepPrams.RemoveIf(x => found.Contains(x) == false);
                otherMember.Read(source);
            }

            AnnotationCollection fac;

            public SweepParamsAnnotation(AnnotationCollection fac)
            {
                this.fac = fac;
            }
        }

        class SweepRow
        {
            public int Test { get; set; }
            public List<SweepParam> lst;
            public int index;
            public SweepRow(List<SweepParam> lst, int index){
                this.lst = lst;
                this.index = index;
            }
        }

        class ValueAnnotation : IObjectValueAnnotation, IOwnedAnnotation
        {
            
            public object Value { get; set; }

            public SweepParam Param;
            public int Index;

            public void Read(object source)
            {
                int index = Math.Min(Param.Values.Length - 1, Index);
                Value = index == -1 ? Param.DefaultValue : Param.Values[index];
            }

            public void Write(object source)
            {
                if(Index >= Param.Values.Length)
                {
                    Param.Resize(Index + 1);
                }
                Param.Values[Index] = Value;
            }
        }
        class SweepParamsMembers : IMembersAnnotation, IOwnedAnnotation
        {
            List<AnnotationCollection> _members = null;
            public IEnumerable<AnnotationCollection> Members {
                get{
                    if (_members != null) return _members;
                    var step2 = annotation?.ParentAnnotation.Get<SweepParamsAggregation>();
                    var r = step2.RowAnnotations;
                    var row = annotation.Get<IObjectValueAnnotation>().Value as SweepRow;
                    var p = row.lst;

                    var allsteps = r.SelectMany(x => x.Value).Distinct().ToArray();
                    var superAnnotation = AnnotationCollection.Annotate(allsteps);
                    var allMembers = superAnnotation.Get<IMembersAnnotation>().Members.ToArray();
                    List<AnnotationCollection> lst = new List<AnnotationCollection>(p.Count);
                    var members = r.Keys.ToHashSet();
                    var subSet = allMembers.Where(x => members.Contains(x.Get<IMemberAnnotation>()?.Member)).ToArray();
                    for(int i = 0; i < p.Count; i++)
                    {
                        var p2 = p[i].Property;
                        var sub = subSet.First(x => x.Get<IMemberAnnotation>().Member == p2);
                        var v = new ValueAnnotation { Param = p[i], Index = row.index };
                        v.Read(null);
                        sub.Add(v);
                        
                        lst.Add(sub);
                    }
                    
                    return (_members = lst);
                }

            }

            AnnotationCollection annotation;
            public SweepParamsMembers(AnnotationCollection annotation)
            {
                this.annotation = annotation;
            }

            public void Read(object source)
            {
                if (_members != null)
                    _members.ForEach(x => x.Read(source));
            }

            public void Write(object source)
            {
                if (_members != null)
                    _members.ForEach(x => x.Write(source));
            }
        }

        class SweepParamsAggregation : ICollectionAnnotation, IOwnedAnnotation
        {
            
            AnnotationCollection fac;
            public SweepParamsAggregation(AnnotationCollection fac)
            {
                this.fac = fac;
            }

            Dictionary<IMemberInfo, List<object>> rowAnnotations = new Dictionary<IMemberInfo, List<object>>();

            public Dictionary<IMemberInfo, List<object>> RowAnnotations
            {
                get
                {
                    if (rowAnnotations.Count == 0)
                    {

                        var loop = fac.ParentAnnotation.Get<IObjectValueAnnotation>().Value as SweepLoop;
                        var steps = loop.RecursivelyGetChildSteps(TestStepSearch.All).ToArray();
                        var properties = steps.Select(x => TypeInfo.GetTypeInfo(x).GetMembers()).ToArray();
                        rowAnnotations = new Dictionary<IMemberInfo, List<object>>();
                        var swparams = fac.Get<IObjectValueAnnotation>().Value as List<SweepParam>;
                        foreach (var param in swparams)
                        {
                            rowAnnotations.Add(param.Property, new List<object>());

                        }

                        for (int i = 0; i < properties.Length; i++)
                        {
                            foreach(var property in properties[i])
                            {
                                if (rowAnnotations.ContainsKey(property) == false)
                                    continue;

                                rowAnnotations[property].Add(steps[i]);
                            }
                        }

                    }
                    return rowAnnotations;
                }
            }

            public IEnumerable<AnnotationCollection> AnnotatedElements
            {
                get
                {
                    var lst = new List<AnnotationCollection>(count);
                    for(int i = 0; i < count; i++)
                    {
                        lst.Add(annotation(i));
                    }
                    return lst;
                }
                set {
                    count = value.Count();
                }
            }

            
            public AnnotationCollection NewElement()
            {
                return annotation(count);
            }

            Dictionary<int, AnnotationCollection> annotations = new Dictionary<int, AnnotationCollection>();

            AnnotationCollection annotation(int index)
            {
                var sparams = (List<SweepParam>)fac.Get<IObjectValueAnnotation>().Value;
                var param = new SweepRow(sparams, index) { Test = index };
                var a= fac.AnnotateSub(TypeInfo.GetTypeInfo(param), param);
                annotations[index] = a;
                return a;
            }

            int count = 0;
            public void Read(object source)
            {
                var sparams = (List<SweepParam>)fac.Get<IObjectValueAnnotation>().Value;
                count = sparams.FirstOrDefault()?.Values.Length ?? 0;
                rowAnnotations.Clear();
                annotations.Clear();
            }

            public void Write(object source)
            {
                var sparams = (List<SweepParam>)fac.Get<IObjectValueAnnotation>().Value;
                {
                    foreach(var param in sparams)
                    {
                        param.Resize(count);
                    }
                }
                foreach(var a in annotations)
                {
                    a.Value.Write();
                }
            }
        }

        class SweepRangeMembersAnnotation : IMultiSelect, IAvailableValuesAnnotation, IOwnedAnnotation, IStringValueAnnotation
        {
            class AllItems
            {
                public override string ToString()
                {
                    return "Select All";
                }
            }

            object[] selectedValues = Array.Empty<object>();


            IEnumerable available = Array.Empty<object>();
            public IEnumerable AvailableValues => available;

            public IEnumerable Selected
            {
                get => selectedValues;
                set => selectedValues = value.Cast<object>().ToArray();
            }
            public string Value
            {
                get => String.Join<object>(",", Selected as IEnumerable<object>);
                set => throw new NotSupportedException();
            }

            public void Read(object source)
            {
                List<object> allItems = new List<object>();
                allItems.Add(new AllItems());
                available = allItems;

                Dictionary<IMemberInfo, object> members = new Dictionary<IMemberInfo, object>();
                getPropertiesForItem((ITestStep)source, members);
                foreach (var member in members)
                {
                    var anot = AnnotationCollection.Create(null, member.Key);
                    var access = anot.Get<ReadOnlyMemberAnnotation>();
                    var rmember = anot.Get<IMemberAnnotation>().Member;
                    if (rmember != null)
                    {
                        if (access == null || (!access.IsReadOnly && access.IsVisible))
                        {
                            bool? browsable = rmember.GetAttribute<BrowsableAttribute>()?.Browsable;
                            if (browsable == false) continue;
                            if (browsable == null)
                            {
                                if (rmember.GetAttribute<XmlIgnoreAttribute>() != null)
                                    continue;
                            }
                            allItems.Add(member.Key);
                        }
                    }
                }

                Selected = (source as SweepLoopRange).SweepProperties.ToArray();
            }

            void getPropertiesForItem(ITestStep step, Dictionary<IMemberInfo, object> members)
            {
                foreach (ITestStep cs in step.ChildTestSteps)
                {
                    foreach (var member in TypeInfo.GetTypeInfo(cs).GetMembers())
                    {
                        if (members.ContainsKey(member) == false)
                        {
                            members.Add(member, member.GetValue(cs));
                        }
                    }
                    getPropertiesForItem(cs, members);
                }
            }

            public void Write(object source)
            {
                var sweepPrams = fac.Get<IObjectValueAnnotation>().Value as List<IMemberInfo>;
                HashSet<IMemberInfo> found = new HashSet<IMemberInfo>();
                foreach (var _mem in selectedValues)
                {
                    if (_mem is AllItems) continue;
                    var mem = (IMemberInfo)_mem;
                    found.Add(mem);
                }
                fac.Get<IObjectValueAnnotation>().Value = found.ToList();
            }

            AnnotationCollection fac;

            public SweepRangeMembersAnnotation(AnnotationCollection fac)
            {
                this.fac = fac;
            }
        }

        public double Priority => 5;

        public void Annotate(AnnotationResolver resolver)
        {

            var annotation = resolver.Annotations;
            var mem = annotation.Get<IMemberAnnotation>();
            if(mem != null && mem.Member.TypeDescriptor is CSharpTypeInfo cst)
            {
                if (mem.Member.DeclaringType.DescendsTo(typeof(SweepLoop)))
                {
                    if (cst.Type.DescendsTo(typeof(IEnumerable)))
                    {
                        var elem = cst.Type.GetEnumerableElementType();
                        if (elem == typeof(IMemberInfo))
                        {
                            annotation.Add(new SweepParamsAnnotation(annotation));
                        }
                        if (elem == typeof(SweepParam))
                        {
                            annotation.Add(new SweepParamsAggregation(annotation));
                        }
                    }
                }else if (mem.Member.DeclaringType.DescendsTo(typeof(SweepLoopRange)))
                {
                    if (cst.Type.DescendsTo(typeof(IEnumerable)))
                    {
                        var elem = cst.Type.GetEnumerableElementType();
                        if (elem == typeof(IMemberInfo))
                        {
                            annotation.Add(new SweepRangeMembersAnnotation(annotation));
                        }
                    }
                }
            }
            var reflect = annotation.Get<IReflectionAnnotation>();
            if (reflect?.ReflectionInfo is CSharpTypeInfo type)
            {
                if(type.Type == typeof(SweepRow))
                {
                    annotation.Add(new SweepParamsMembers(annotation));
                }
            }
        }
    }

    class AggregatedMembersAnnotation : IMembersAnnotation, IForwardedAnnotations, IOwnedAnnotation
    {
        public IEnumerable<AnnotationCollection> Members { get; set; }
        
        public IEnumerable<AnnotationCollection> Forwarded { get; set; }

        public void Read(object source)
        {
            foreach (var mem in Members)
                mem.Read(source);
            foreach (var mem in Forwarded)
                mem.Read();
        }

        public void Write(object source)
        {
            foreach (var mem in Members)
                mem.Write(source);
            foreach(var mem in Forwarded)
                mem.Write();
        }
    }
    
    public class TestPlanReferenceAnnotator2 : IAnnotator
    {
        public double Priority => 20;
        class SubAvailable : IAvailableValuesAnnotation
        {
            public IEnumerable AvailableValues => throw new NotImplementedException();
        }

        class SubSuggested : ISuggestedValuesAnnotation
        {
            AnnotationCollection subcol;
            public SubSuggested(AnnotationCollection subcol)
            {
                this.subcol = subcol;
            }
            public IEnumerable SuggestedValues => subcol.Get<ISuggestedValuesAnnotation>().SuggestedValues;
        }

        public void Annotate(AnnotationResolver resolver)
        {
            var annotation = resolver.Annotations;
            ISuggestedValuesAnnotation suggested = annotation.Get<ISuggestedValuesAnnotation>();
            IAvailableValuesAnnotation avail = annotation.Get<IAvailableValuesAnnotation>();
            if (avail != null || suggested != null)
            {
                var obj = annotation.ParentAnnotation?.Get<IObjectValueAnnotation>().Value as TestPlanReference;
                if (obj == null) return;
                var member = annotation.Get<IMemberAnnotation>()?.Member as ExpandedMemberData;
                // an IAvailableValuesAnnotation inside of a TestPlanReference.
                if(member != null)
                {
                    var subannotation = AnnotationCollection.Annotate(member.ExternalParameter.Properties.Select(x => x.Key).ToArray());
                    var subMembers = subannotation.Get<IMembersAnnotation>();
                    var suggesteda = subMembers.Members.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member == member.ExternalParameter.PropertyInfos.First());
                    if(suggesteda != null && suggesteda.Get<ISuggestedValuesAnnotation>() != null)
                    {
                        resolver.Annotations.Add(new SubSuggested(suggesteda));
                    }
                }

            }
        }
    }

    public class SweepParam
    {
        public String Name { get; set; }
        object[] _values = Array.Empty<object>();
        public object[] Values { get => _values; set => _values = value; }
        [XmlIgnore]
        public object DefaultValue;
        [XmlIgnore]
        public ITypeInfo Type
        {
            get { return Property?.TypeDescriptor; }
        }
        
        /// <summary>
        /// Gets or sets if the parameter is enabled.
        /// </summary>
        public bool[] IsEnabled { get; set; }
        
        [XmlIgnore]
        public IMemberInfo[] PropertyInfos { get; set; }
        
        /// <summary>
        /// A list of the Properties that this parameters represents. 
        /// </summary>
        public string[] PropertyNames { get; set; }
        /// <summary>
        /// The property from where attributes are pulled.
        /// </summary>
        
        public IMemberInfo Property { get { return PropertyInfos == null ? null : PropertyInfos.FirstOrDefault(); } }

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
        public SweepParam(IEnumerable<IMemberInfo> props, params object[] values) : this()
        {
            if (props.Count() < 1)
                throw new ArgumentException("Must contain at least one PropertyInfo", "props");

            Name = props.First().GetDisplayAttribute().GetFullName();
            PropertyInfos = props.Distinct().ToArray();
            PropertyNames = PropertyInfos.Select(prop => prop.GetDisplayAttribute().GetFullName()).ToArray();
            if (!props.All(p => object.Equals(p.TypeDescriptor, Type)))
                throw new ArgumentException("All sweep properties must be of the same type", "props");
            if (values != null && values.Length > 0)
            {
                Array.Resize(ref _values, values.Length);
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
            int oldCount = Values.Length;
            if (oldCount == newCount)
                return;

            var oldValues = Values;
            Array.Resize(ref _values, newCount);
            for (int i = oldCount; i < newCount; i++)
            {
                if (i == 0)
                {
                    _values.SetValue(DefaultValue, i);
                }
                else
                {
                    _values.SetValue(_values.GetValue(i - 1), i);
                }
            }
            var copyAmount = Math.Min(newCount, oldValues.Length);
            Array.Copy(oldValues, Values, copyAmount);
            bool[] isEnabled = IsEnabled;
            Array.Resize(ref isEnabled, newCount);
            IsEnabled = isEnabled;
            for (int i = oldCount; i < newCount; i++)
            {
                isEnabled[i] = true;
            }
        }
    }
}
