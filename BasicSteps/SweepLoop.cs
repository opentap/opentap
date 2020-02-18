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
using System.Collections;
using System.Threading;

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
                else
                    return SweepParameters
                        .SelectMany(param => param.Values.Cast<object>())
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
                            var tp = TypeData.GetTypeData(val);
                            if (paramProp.TypeDescriptor == sweepParameter.Member.TypeDescriptor && (tp.DescendsTo(paramProp.TypeDescriptor) || val == null))
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
                        if (!values.TryGetValue(childTestStep, out var cons))
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

    public class BasicStepsAnnotator : IAnnotator
    {
        class SweepParamsAnnotation : IMultiSelect, IAvailableValuesAnnotation, IOwnedAnnotation, IStringReadOnlyValueAnnotation, IHideOnMultiSelectAnnotation
        {
            [Display("Select All")]
            class AllItems
            {
                public override string ToString()
                {
                    return "Select All";
                }
            }

            static AllItems allItemsSelect = new AllItems();
            static AllItems noItems = new AllItems();

            object[] selectedValues;


            IEnumerable available = Array.Empty<object>();
            public IEnumerable AvailableValues => available;
            List<object> allAvailable = new List<object>();
            public IEnumerable Selected
            {
                get => selectedValues;
                set => selectedValues = value.Cast<object>().ToArray();
            }

            public string Value {
                get
                {
                    int count = 0;
                    if(selectedValues != null)
                        count = selectedValues.Count(x => (x is AllItems) == false);
                    return string.Format("{0} selected", count);
                }
            }
            static bool memberCanSweep(IMemberData mem) => false == mem.HasAttribute<UnsweepableAttribute>();
            public void Read(object source)
            {
                if (source is ITestStep == false) return;
                List<object> allItems = new List<object>();
                available = allItems;

                Dictionary<IMemberData, object> members = new Dictionary<IMemberData, object>();
                getPropertiesForItem((ITestStep)source, members);
                
                foreach(var member in members)
                {
                    if (!memberCanSweep(member.Key)) 
                        continue;
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
                    if (swe.Member == null) continue;
                    if (members.TryGetValue(swe.Member, out object value))
                        swe.DefaultValue = value;
                }

                var items = (source as SweepLoop).SweepParameters.Select(x => x.Member).Cast<object>().ToList();
                if(items.Count == allItems.Count)
                {
                    items.Insert(0, noItems);
                    allItems.Insert(0, noItems);
                }
                else
                {
                    allItems.Insert(0, allItemsSelect);
                }
                var grp = items.GroupBy(x => x is IMemberData mem ? (mem.GetDisplayAttribute().GetFullName() + mem.TypeDescriptor.Name) : "");
                allAvailable = allItems;
                available = allItems.GroupBy(x => x is IMemberData mem ? (mem.GetDisplayAttribute().GetFullName() + mem.TypeDescriptor.Name) : "")
                    .Select(x => x.FirstOrDefault()).ToArray();
                selectedValues = grp.Select(x => x.FirstOrDefault()).ToArray();
                
            }

            void getPropertiesForItem(ITestStep step, Dictionary<IMemberData, object> members)
            {
                if (step is TestPlanReference) return;
                foreach (ITestStep cs in step.ChildTestSteps)
                {
                    foreach(var member in TypeData.GetTypeData(cs).GetMembers())
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
                if (selectedValues == null) return;
                var otherMember = annotation.ParentAnnotation.Get<IMembersAnnotation>().Members.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member.Name == nameof(SweepLoop.SweepParameters));
                var sweepPrams = otherMember.Get<IObjectValueAnnotation>().Value as List<SweepParam>;
                var avail = allAvailable.OfType<IMemberData>().ToLookup(x => x.GetDisplayAttribute().GetFullName() + x.TypeDescriptor.Name, x => x);
                HashSet<SweepParam> found = new HashSet<SweepParam>();
                int initCount = sweepPrams.FirstOrDefault()?.Values.Length ?? 0;
                Dictionary<IMemberData, object> members = null;
                
                if (selectedValues.Contains(allItemsSelect))
                    selectedValues = this.AvailableValues.Cast<object>().ToArray();
                if (selectedValues.Contains(noItems) == false && this.AvailableValues.Cast<object>().Contains(noItems))
                    selectedValues = Array.Empty<object>();

                var group = selectedValues.OfType<IMemberData>().Select(x => x.GetDisplayAttribute().GetFullName() + x.TypeDescriptor.Name).Distinct();

                foreach (var memname in group)
                {
                    var mem = avail[memname];
                    
                    SweepParam existing = null;

                    foreach (var smem in mem)
                    {
                        existing = sweepPrams.Find(x => x.Members.Contains(smem));
                    }
                    if(existing == null)
                    {
                        existing = new SweepParam(mem.ToArray());
                        if (members == null)
                        {
                            members = new Dictionary<IMemberData, object>();
                            getPropertiesForItem((ITestStep)source, members);
                        }
                        foreach (var smem in mem)
                        {
                            if (members.TryGetValue(smem, out object val))
                                existing.DefaultValue = val;
                        }
                        sweepPrams.Add(existing);
                    }
                    else
                    {
                        existing.Members = existing.Members.Concat(mem).Distinct().ToArray();
                    }
                    existing.Resize(initCount);
                    found.Add(existing);
                }
                sweepPrams.RemoveIf(x => found.Contains(x) == false);
                (source as SweepLoop)?.sanitizeSweepParams(false);
                otherMember.Read(source);
            }

            AnnotationCollection annotation;

            public SweepParamsAnnotation(AnnotationCollection annotation)
            {
                this.annotation = annotation;
            }
        }

        class SweepRow
        {
            public bool Enabled { get; set; }
            public object[] Values { get;}
            public SweepRow(bool enabled, object[] Values)
            {
                this.Values = Values;
                Enabled = enabled;
            }
        }

        class ValueContainerAnnotation : IObjectValueAnnotation
        {            
            public object Value { get; set; }
        }
        class SweepParamsMembers : IMembersAnnotation, IOwnedAnnotation
        {
            List<AnnotationCollection> _members = null;
            public IEnumerable<AnnotationCollection> Members {
                get{
                    if (_members != null) return _members;
                    var step2 = annotation?.ParentAnnotation.Get<SweepParamsAggregation>();
                    var r = step2.RowAnnotations;

                    var val = annotation.Get<IObjectValueAnnotation>().Value as SweepRow;
                    
                    var parent = annotation.ParentAnnotation.Source as SweepLoop;
                    var p = parent.SweepParameters;

                    var allsteps = r.SelectMany(x => x.Value).Distinct().ToArray();
                    var superAnnotation = AnnotationCollection.Annotate(allsteps);
                    var allMembers = superAnnotation.Get<IMembersAnnotation>().Members.ToArray();
                    List<AnnotationCollection> lst = new List<AnnotationCollection>(p.Count);
                    var members = r.Keys.ToHashSet();
                    var subSet = allMembers.Where(x => members.Contains(x.Get<IMemberAnnotation>()?.Member)).ToArray();
                    var enabled = annotation.Get<IMembersAnnotation>(from: this).Members.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member.Name == "Enabled");
                    lst.Add(enabled);
                    for(int i = 0; i < p.Count; i++)
                    {
                        var p2 = p[i].Member;
                        var sub = subSet.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member == p2);
                        if (sub == null)
                            continue;
                        
                        var v = new ValueContainerAnnotation { Value = val.Values[i]};

                        // a bit complicated..
                        // we have to make sure that the ValueAnnotation is put very early in the chain.
                        sub.Insert(sub.IndexWhen(x => x is IObjectValueAnnotation) + 1, v);
                        
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
                if (_members == null) return;
                var src = source as SweepRow;
                int index = 0;
                foreach (var member in _members)
                {
                    member.Write(source);
                    if (index > 0)
                    {
                        src.Values[index - 1] = member.Get<IObjectValueAnnotation>().Value;
                    }
                    else
                    {
                        src.Enabled = (bool)member.Get<IObjectValueAnnotation>().Value;
                    }

                    index += 1;
                }
                
            }
        }

        class SweepParamsAggregation : ICollectionAnnotation, IOwnedAnnotation, IStringReadOnlyValueAnnotation, IAccessAnnotation, IHideOnMultiSelectAnnotation
        { 
            
            AnnotationCollection annotation;
            public SweepParamsAggregation(AnnotationCollection annotation)
            {
                this.annotation = annotation;
            }

            Dictionary<IMemberData, List<object>> rowAnnotations = new Dictionary<IMemberData, List<object>>();

            public Dictionary<IMemberData, List<object>> RowAnnotations
            {
                get
                {
                    if (rowAnnotations.Count == 0)
                    {
                        var steps = sweep.RecursivelyGetChildSteps(TestStepSearch.All).ToArray();
                        var properties = steps.Select(x => TypeData.GetTypeData(x).GetMembers()).ToArray();
                        rowAnnotations = new Dictionary<IMemberData, List<object>>();
                        var swparams = annotation.Get<IObjectValueAnnotation>().Value as List<SweepParam>;
                        foreach (var param in swparams)
                        {
                            if(param.Member != null)
                                rowAnnotations.Add(param.Member, new List<object>());
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

            IEnumerable<AnnotationCollection> annotatedElements;

            public IEnumerable<AnnotationCollection> AnnotatedElements
            {
                get
                {
                    
                    if (annotatedElements == null)
                    {
                        if (sweep == null) return Enumerable.Empty<AnnotationCollection>();
                        List<AnnotationCollection> lst = new List<AnnotationCollection>();
                        for (int i = 0; i < sweep.EnabledRows.Length; i++)
                            lst.Add(annotateIndex(i));

                        annotatedElements = lst;
                    }
                    
                    return annotatedElements;
                }
                set => annotatedElements = value.ToArray();
            }

            public string Value => $"Sweep rows: {sweep?.EnabledRows.Length ?? 0}";

            public bool IsReadOnly => (annotation.Get<IObjectValueAnnotation>()?.Value as List<SweepParam>) == null;

            public bool IsVisible => true;

            public AnnotationCollection NewElement()
            {
                return annotateIndex(-1);
            }

            AnnotationCollection annotateIndex(int index = -1)
            {
                var sparams = sweep.SweepParameters;
                SweepRow row;
                if (index == -1 || index >= sweep.EnabledRows.Length)
                    row = new SweepRow(true, sparams.Select(x => x.Values.DefaultIfEmpty(x.DefaultValue).LastOrDefault()).ToArray());
                else
                    row = new SweepRow(sweep.EnabledRows[index], sparams.Select(x => x.Values[index]).ToArray());
                return this.annotation.AnnotateSub(TypeData.GetTypeData(row),row);
            }

            SweepLoop sweep;
            
            public void Read(object source)
            {
                sweep = annotation.ParentAnnotation.Get<IObjectValueAnnotation>()?.Value as SweepLoop;
                annotatedElements = null;
                rowAnnotations.Clear();
            }

            public void Write(object source)
            {
                IList lst = (IList)annotatedElements;
                if (lst == null) return;
                
                var sweepParams = sweep.SweepParameters;
                var count = lst.Count;
                foreach(var param in sweepParams)
                     param.Resize(count);
                sweep.EnabledRows = new bool[count];

                int index = 0;
                foreach(AnnotationCollection a in lst)
                {
                    a.Write();
                    var row = a.Get<IObjectValueAnnotation>().Value as SweepRow;
                    sweep.EnabledRows[index] = row.Enabled;
                    for (int i = 0; i < row.Values.Length; i++)
                    {
                        sweepParams[i].Values[index] = row.Values[i];
                    }

                    index += 1;
                }
                
                sweep.sanitizeSweepParams(log: false); // update size of EnabledRows.
            }
        }

        class SweepRangeMembersAnnotation : IMultiSelect, IAvailableValuesAnnotation, IOwnedAnnotation, IStringReadOnlyValueAnnotation, IAccessAnnotation, IHideOnMultiSelectAnnotation
        {
            object[] selectedValues = Array.Empty<object>();

            IEnumerable available = Array.Empty<object>();
            public IEnumerable AvailableValues => available;

            public IEnumerable Selected
            {
                get => selectedValues;
                set => selectedValues = value.Cast<object>().ToArray();
            }
            public string Value => string.Format("{0} selected", selectedValues?.Count() ?? 0);

            public bool IsReadOnly { get; private set; }

            public bool IsVisible => true;

            public void Read(object source)
            {
                if (source is ITestStep == false)
                {
                    IsReadOnly = true;
                    return;
                }
                
                List<object> allItems = new List<object>();
                available = allItems;

                Dictionary<IMemberData, object> members = SweepLoopRange.GetPropertiesForItems((ITestStep)source);
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
            


            public void Write(object source)
            {
                if (IsReadOnly) return;
                var sweepPrams = fac.Get<IObjectValueAnnotation>().Value as List<IMemberData>;
                
                HashSet<IMemberData> found = new HashSet<IMemberData>();
                foreach (var _mem in selectedValues)
                {
                    var mem = (IMemberData)_mem;
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

        public void Annotate(AnnotationCollection annotation)
        {
            var mem = annotation.Get<IMemberAnnotation>();
            if(mem != null && mem.Member.TypeDescriptor is TypeData cst)
            {
                if (mem.Member.DeclaringType.DescendsTo(typeof(SweepLoop)))
                {
                    if (cst.DescendsTo(typeof(IEnumerable)))
                    {
                        var elem = cst.Type.GetEnumerableElementType();
                        if (elem == typeof(IMemberData))
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
                    if (cst.DescendsTo(typeof(IEnumerable)))
                    {
                        var elem = cst.Type.GetEnumerableElementType();
                        if (elem == typeof(IMemberData))
                        {
                            annotation.Add(new SweepRangeMembersAnnotation(annotation));
                        }
                    }
                }
            }
            var reflect = annotation.Get<IReflectionAnnotation>();
            if (reflect?.ReflectionInfo is TypeData type)
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
            public IEnumerable AvailableValues => sub.Get<IAvailableValuesAnnotation>().AvailableValues;

            AnnotationCollection sub;
            public SubAvailable(AnnotationCollection subcol)
            {
                this.sub = subcol;
            }
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

        class SubAccess : IAccessAnnotation
        {
            private AnnotationCollection sub;

            public bool IsReadOnly => sub.Get<IAccessAnnotation>().IsReadOnly;

            public bool IsVisible => sub.Get<IAccessAnnotation>().IsVisible;

            public SubAccess(AnnotationCollection sub)
            {
                this.sub = sub;
            }
        }

        class SubMember : IOwnedAnnotation
        {
            private AnnotationCollection sub;

            public SubMember(AnnotationCollection sub)
            {
                this.sub = sub;
            }
            public void Read(object source)
            {
                sub.Read();
            }

            public void Write(object source)
            {
                sub.Write();
            }
        }

        public void Annotate(AnnotationCollection annotation)
        {
            var obj = annotation.ParentAnnotation?.Get<IObjectValueAnnotation>().Value as TestPlanReference;
            if (obj == null) return;
            var member = annotation.Get<IMemberAnnotation>()?.Member as ExpandedMemberData;
            if (member == null) return;
            
            var subannotation = AnnotationCollection.Annotate(member.ExternalParameter.Properties.Select(x => x.Key).ToArray());
            annotation.Add(new SubMember(subannotation));
            var subMembers = subannotation.Get<IMembersAnnotation>();
            var thismember = subMembers.Members.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member == member.ExternalParameter.PropertyInfos.First());
            if (thismember == null) return;

            annotation.RemoveType<IAccessAnnotation>();
            annotation.Add(new SubAccess(thismember));

            IAvailableValuesAnnotation avail = annotation.Get<IAvailableValuesAnnotation>();
            if (avail != null) annotation.Add(new SubAvailable(thismember));

            ISuggestedValuesAnnotation suggested = annotation.Get<ISuggestedValuesAnnotation>();
            if (suggested != null) annotation.Add(new SubSuggested(thismember));
        }
    }

    public class SweepParam
    {
        internal SweepLoop Step;
        public string Name => Member?.Name;

        object[] _values = Array.Empty<object>();
        public object[] Values { get => _values; set => _values = value; }

        object _defaultValue;
        [XmlIgnore]
        public object DefaultValue
        {
            get => cloneObject(_defaultValue);
            set => _defaultValue = value;
        }

        [XmlIgnore]
        public ITypeData Type => Member?.TypeDescriptor; 

        public string[] MemberNames { get; set; }

        internal static string GetMemberName(IMemberData member) => member.DeclaringType.Name + "." + member.Name;
        

        IMemberData[] members = Array.Empty<IMemberData>();
        [XmlIgnore]
        public IMemberData[] Members
        {
            get => members;
            set
            {
                members = value;
                MemberNames = value.Select(GetMemberName).ToArray();
                Step?.parametersChanged();
            }
        }
        
        /// <summary>
        /// The property from where attributes are pulled.
        /// </summary>
        
        public IMemberData Member => Members?.FirstOrDefault();

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
        public SweepParam(IEnumerable<IMemberData> props) : this()
        {
            if (props.Count() < 1)
                throw new ArgumentException("Must contain at least one member", nameof(props));

            Members = props.Distinct().ToArray();
            if (!props.All(p => object.Equals(p.TypeDescriptor, Type)))
                throw new ArgumentException("All members must be of the same type", nameof(props));
        }

        public SweepParam(IEnumerable<IMemberData> members, params object[] values):this(members)
        {
            Resize(values.Length);
            for(int i = 0; i < values.Length; i++)
            {
                Values[i] = values[i];
            }
        }

        public override string ToString()
        {
            return string.Format("{0} : {1}", Name, Type.Name);
        }

        TapSerializer serializer = null;
        object cloneObject(object newValue)
        {
            if (StringConvertProvider.TryGetString(newValue, out string str))
            {
                if (StringConvertProvider.TryFromString(str, TypeData.GetTypeData(newValue), this.Step, out object result))
                {
                    newValue = result;
                }
            }
            else
            {

                string serialized = null;

                serializer = new TapSerializer();
                try
                {

                    serialized = serializer.SerializeToString(newValue);
                    newValue = serializer.DeserializeFromString(serialized, TypeData.GetTypeData(newValue));
                }
                catch
                {

                }

            }
            return newValue;
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
                object newValue = null; 
                if (i == 0)
                    newValue = DefaultValue;
                else
                    newValue = _values.GetValue(i - 1);
                newValue = cloneObject(newValue);
                _values.SetValue(newValue, i);
            }
            var copyAmount = Math.Min(newCount, oldValues.Length);
            Array.Copy(oldValues, Values, copyAmount);
            Step?.parametersChanged();
        }
    }
}
