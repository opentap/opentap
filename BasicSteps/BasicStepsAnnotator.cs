using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap.Plugins.BasicSteps
{
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
                    
                    Dictionary<object, AnnotationCollection> annotated = new Dictionary<object, AnnotationCollection>();
                    foreach (var elems in r.Values)
                    {
                        var elem = elems[0];
                        if (annotated.ContainsKey(elem)) continue;
                        annotated[elem] = AnnotationCollection.Annotate(elem);
                    }
                    var allsteps = r.Select(x => annotated[x.Value[0]].AnnotateMember(x.Key)).ToArray();
                    var allMembers = allsteps;
                    
                    List<AnnotationCollection> lst = new List<AnnotationCollection>(p.Count);
                    var members = r.Keys.ToHashSet();
                    var subSet = allMembers.Where(x => members.Contains(x.Get<IMemberAnnotation>()?.Member)).ToArray();
                    var enabled = annotation.Get<IMembersAnnotation>(@from: this).Members.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member.Name == "Enabled");
                    lst.Add(enabled);
                    for(int i = 0; i < p.Count; i++)
                    {
                        var p2 = p[i].Member;
                        var sub = subSet.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member == p2);
                        if (sub == null)
                            continue;
                        // Sweep loop does not support EnabledIf.
                        var enabledif = sub.FirstOrDefault(x => x.GetType().Name.Contains("EnabledIfAnnotation"));
                        if(enabledif != null)
                            sub.Remove(enabledif);
                        var v = new ValueContainerAnnotation { Value = val.Values[i]};
                        // This is a bit complicated..
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
                        sweepParams[i].Values[index] = cloneIfPossible(row.Values[i], sweep);
                    }

                    index += 1;
                }
                
                sweep.sanitizeSweepParams(log: false); // update size of EnabledRows.
            }
            TapSerializer tapSerializer;
            object cloneIfPossible(object value, object context)
            {
                if (StringConvertProvider.TryGetString(value, out string result))
                {
                    if (StringConvertProvider.TryFromString(result, TypeData.GetTypeData(value), context, out object result2))
                    {
                        return result2;
                    }
                }
                if(tapSerializer == null) tapSerializer = new TapSerializer();
                try
                {
                    return tapSerializer.DeserializeFromString(tapSerializer.SerializeToString(value),
                               TypeData.GetTypeData(value)) ?? value;
                }
                catch
                {
                    return value;
                }
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

            {
                var member = annotation.Get<IMemberAnnotation>()?.Member as IParameterMemberData;
                if (member == null) return;

                // If the member is a forwarded member on a loopTestStep, it should not be editable because the value
                // is controlled in the sweep, however it should still be shown in the GUI.
                if (member.DeclaringType.DescendsTo(typeof(ISelectedParameters)))
                    annotation.Add(new DisabledLoopMemberAnnotation(annotation, member));
            }
        }
        class DisabledLoopMemberAnnotation : IEnabledAnnotation
        {
            readonly AnnotationCollection annotation;
            readonly IMemberData member;
            public DisabledLoopMemberAnnotation(AnnotationCollection annotation, IMemberData member)
            {
                this.annotation = annotation;
                this.member = member;
            }

            public bool IsEnabled
            {
                get
                { 
                    if (annotation.Source is ISelectedParameters sw && sw.SelectedParameters.Contains(member.Name))
                        return false;
                    return true;
                }
            }
        }
    }
}