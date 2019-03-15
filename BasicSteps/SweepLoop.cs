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
        public string IterationInfo
        {
            get
            {
                return string.Format("{0} of {1}", Iteration + 1, EnabledRows.Length);
            }
        }

        [Browsable(false)]
        public bool[] EnabledRows { get; set; } = Array.Empty<bool>();

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

        [Display("Sweep Values", Order: 2, Description: "Select the ranges of values to sweep.")]
        [EnabledIf(nameof(SweepParametersEnabled), true)]
        [DeserializeOrder(1)]
        public List<SweepParam> SweepParameters
        {
            get { return sweepParameters; }
            set
            {
                crossPlanSweepIndex = 0;
                Iteration = 0;
                sweepParameters = value;
                OnPropertyChanged(nameof(SweepParameters));
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
                            if (object.Equals(paramProp.TypeDescriptor, sweepParameter.Member.TypeDescriptor))
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

        Dictionary<ITestStep, Tuple<IMemberData, object>> getSweepParameterValues(SweepParam sweepParameter, TestStepList childTestSteps)
        {
            Dictionary<ITestStep, Tuple<IMemberData, object>> values = new Dictionary<ITestStep, Tuple<IMemberData, object>>();
            populateChildParameters(values, sweepParameter, childTestSteps);
            return values;
        }

        static void populateChildParameters(Dictionary<ITestStep, Tuple<IMemberData, object>> values, SweepParam sweepParameter, TestStepList childTestSteps)
        {
            foreach (ITestStep childTestStep in childTestSteps)
            {
                ITypeData stepType = TypeData.GetTypeData(childTestStep);
                foreach (IMemberData property in sweepParameter.Members)
                {
                    IMemberData paramProp = stepType.GetMember(property.Name);
                    if (object.Equals(paramProp, property))
                        values[childTestStep] = new Tuple<IMemberData, object>(paramProp, paramProp.GetValue(childTestStep));
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

        bool isRunning => StepRun != null;

        string validateSweep()
        {   // Mostly copied from Run
            if (SweepParameters.Count <= 0) return "";
            if (sweepParameters.All(p => p.Values.Length == 0)) return "No values selected to sweep";
            sanitizeSweepParams();

            if (isRunning) return ""; // Avoid changing the value during run when the gui asks for validation errors.

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
                        kvp.Value.Item1.SetValue(kvp.Key, kvp.Value.Item2);
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

        public override void Run()
        {
            base.Run();
            if (SweepParameters.Count == 0) return;
            int valuesCount = EnabledRows.Count(v => v);
            if (valuesCount == 0)
                return;

            if (CrossPlan == SweepBehaviour.Across_Runs)
            {
                var AdditionalParams = RegisterAdditionalParams(crossPlanSweepIndex);

                // loop until the parameters are enabled.
                while (false == EnabledRows[crossPlanSweepIndex])
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
                        kvp.Value.Item1.SetValue(kvp.Key, kvp.Value.Item2);
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
        class SweepParamsAnnotation : IMultiSelect, IAvailableValuesAnnotation, IOwnedAnnotation, IStringReadOnlyValueAnnotation
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

            public void Read(object source)
            {
                if (source is ITestStep == false) return;
                List<object> allItems = new List<object>();
                available = allItems;

                Dictionary<IMemberData, object> members = new Dictionary<IMemberData, object>();
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
                var otherMember = fac.ParentAnnotation.Get<IMembersAnnotation>().Members.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member.Name == nameof(SweepLoop.SweepParameters));
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
            public bool Enabled { get; set; }
            public List<SweepParam> lst;
            public int index;
            public SweepRow(List<SweepParam> lst, int index)
            {
                this.lst = lst;
                this.index = index;
                Enabled = true;
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
                    var enabled = annotation.Get<IMembersAnnotation>(from: this).Members.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member.Name == "Enabled");
                    lst.Add(enabled);
                    for(int i = 0; i < p.Count; i++)
                    {
                        var p2 = p[i].Member;
                        var sub = subSet.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member == p2);
                        if (sub == null)
                            continue;
                        var fst = sub.IndexWhen(x => x is IObjectValueAnnotation);
                        var v = new ValueAnnotation { Param = p[i], Index = row.index };
                        v.Read(null);

                        // a bit complicated..
                        // we have to make sure that the ValueAnnotation is put very early in the chain.
                        sub.Insert(fst, v);
                        
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

        class SweepParamsAggregation : ICollectionAnnotation, IOwnedAnnotation, IStringReadOnlyValueAnnotation, IAccessAnnotation
        {
            
            AnnotationCollection fac;
            public SweepParamsAggregation(AnnotationCollection fac)
            {
                this.fac = fac;
            }

            Dictionary<IMemberData, List<object>> rowAnnotations = new Dictionary<IMemberData, List<object>>();

            public Dictionary<IMemberData, List<object>> RowAnnotations
            {
                get
                {
                    if (rowAnnotations.Count == 0)
                    {

                        var loop = fac.ParentAnnotation.Get<IObjectValueAnnotation>().Value as SweepLoop;
                        var steps = loop.RecursivelyGetChildSteps(TestStepSearch.All).ToArray();
                        var properties = steps.Select(x => TypeData.GetTypeData(x).GetMembers()).ToArray();
                        rowAnnotations = new Dictionary<IMemberData, List<object>>();
                        var swparams = fac.Get<IObjectValueAnnotation>().Value as List<SweepParam>;
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

            public string Value => string.Format("Sweep rows: {0}", count);

            public bool IsReadOnly => (fac.Get<IObjectValueAnnotation>()?.Value as List<SweepParam>) == null;

            public bool IsVisible => true;

            public AnnotationCollection NewElement()
            {
                return annotation(count);
            }

            Dictionary<int, AnnotationCollection> annotations = new Dictionary<int, AnnotationCollection>();

            AnnotationCollection annotation(int index)
            {
                if (annotations.TryGetValue(index, out AnnotationCollection current)) return current;
                var sparams = (List<SweepParam>)fac.Get<IObjectValueAnnotation>().Value;
                var step = fac.Source as SweepLoop;
                var param = new SweepRow(sparams, index);
                var e = step.EnabledRows;
                if (e.Length > index)
                    param.Enabled = e[index];
                var a= fac.AnnotateSub(TypeData.GetTypeData(param), param);
                annotations[index] = a;
                return a;
            }

            int count = 0;
            public void Read(object source)
            {
                var sparams = (List<SweepParam>)fac.Get<IObjectValueAnnotation>().Value;
                count = sparams?.FirstOrDefault()?.Values.Length ?? 0;
                rowAnnotations.Clear();
                annotations.Clear();
            }

            public void Write(object source)
            {
                var sparams = (List<SweepParam>)fac.Get<IObjectValueAnnotation>().Value;
                var sweep = fac.ParentAnnotation.Get<IObjectValueAnnotation>()?.Value as SweepLoop;
                foreach(var param in sparams)
                     param.Resize(count);

                sweep.sanitizeSweepParams(log: false); // update size of EnabledRows.
                foreach(var a in annotations)
                {
                    if (a.Key < count)
                    {
                        a.Value.Write();
                        var row = a.Value.Get<IObjectValueAnnotation>().Value as SweepRow;
                        sweep.EnabledRows[row.index] = row.Enabled;
                    }
                }
            }
        }

        class SweepRangeMembersAnnotation : IMultiSelect, IAvailableValuesAnnotation, IOwnedAnnotation, IStringReadOnlyValueAnnotation, IAccessAnnotation
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

                Dictionary<IMemberData, object> members = new Dictionary<IMemberData, object>();
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

            void getPropertiesForItem(ITestStep step, Dictionary<IMemberData, object> members)
            {
                foreach (ITestStep cs in step.ChildTestSteps)
                {
                    foreach (var member in TypeData.GetTypeData(cs).GetMembers())
                    {
                        if (member.TypeDescriptor is TypeData t)
                        {
                            if (t.Type.IsNumeric() && members.ContainsKey(member) == false)
                            {
                                members.Add(member, member.GetValue(cs));
                            }
                        }
                    }
                    getPropertiesForItem(cs, members);
                }
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

        [XmlIgnore]
        public object DefaultValue;

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
            Step?.parametersChanged();
        }
    }
}
