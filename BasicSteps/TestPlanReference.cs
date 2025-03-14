//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Xml.Serialization;
using System.Xml.Linq;
using OpenTap.Diagnostic;

namespace OpenTap.Plugins.BasicSteps
{
    [Display("Test Plan Reference", "References a test plan from an external file directly, without having to store the test plan as steps in the current test plan.", "Flow Control")]
    [AllowAnyChild]
    public class TestPlanReference : TestStep
    {
        /// <summary>
        /// Mapping between step GUIDs. Old version -> New Version.
        /// </summary>
        public class GuidMapping
        {
            public Guid Guid1 { get; set; }
            public Guid Guid2 { get; set; }
        }

        /// <summary>
        /// This is the list of path of test plan loaded and used to prevent recursive TestPlan references.
        /// </summary>
        [ThreadStatic]
        static HashSet<string> referencedPlanPaths;
        
        /// <summary>  Gets or sets if the loaded test steps should be hidden from the user. </summary>
        [Display("Hide Steps", Order: 0, Description: "Set if the steps should run hidden (isolated) or if they should be loaded into the test plan.")]
        private bool HideSteps { get; set; }

        ITestStepParent parent;
        // The PlanDir of 'this' should be ignored when calculating Filepath, so the MacroString context is set to the parent.
        [XmlIgnore]
        public override ITestStepParent Parent { get => parent; set { Filepath.Context = value; parent = value; } }

        MacroString filepath = new MacroString();
        string currentlyLoaded;

        public bool CanOpenFile => File.Exists(filepath.Expand());
        
        [Display("Referenced Plan", Order: 0, Description: "A file path pointing to a test plan which will be loaded as read-only test steps.")]
        [Browsable(true)]
        [FilePath(FilePathAttribute.BehaviorChoice.Open, "TapPlan")]
        [DeserializeOrder(1.0)]
        [Unsweepable]
        public MacroString Filepath
        {
            get => filepath; 
            set {

                filepath = value;
                filepath.Context = this;
                try
                {
                    var rp = TapSerializer.GetObjectDeserializer(this)?.ReadPath;
                    if (rp != null) rp = System.IO.Path.GetDirectoryName(rp);
                    var fp = filepath.Expand(testPlanDir: rp);
                    
                    if (currentlyLoaded != fp)
                    {
                        LoadTestPlan();
                        currentlyLoaded = fp;
                    }
                }
                catch { }
            }
        }
        
        [Browsable(false)]
        public string Hash { get; set; }
        
        [AnnotationIgnore]
        public string Path => Filepath.Expand();
        
        bool isExpandingPlanDir = false;
        [MetaData(macroName: "TestPlanDir")]
        [AnnotationIgnore]
        public string PlanDir
        {
            get
            {
                if (isExpandingPlanDir) return null;
                isExpandingPlanDir = true;
                try
                {
                    var exp = new MacroString(this) { Text = Filepath.Text }.Expand();
                    if (string.IsNullOrWhiteSpace(exp))
                        return "";
                    var path = System.IO.Path.GetFullPath(exp);
                    return string.IsNullOrWhiteSpace(path) ? "" : System.IO.Path.GetDirectoryName(path);
                }
                catch
                {        
                    return "";
                }
                finally
                {
                    isExpandingPlanDir = false;
                }
            }
        }


        

        public TestPlanReference()
        {
            ChildTestSteps.IsReadOnly = true;
            Filepath = new MacroString(this);
            Rules.Add(() => (string.IsNullOrWhiteSpace(Filepath) || File.Exists(Filepath)), "File does not exist.", nameof(Filepath));
            StepMapping = new List<GuidMapping>();
        }

        public override void PrePlanRun()
        {
            base.PrePlanRun();

            if (string.IsNullOrWhiteSpace(Filepath))
                throw new OperationCanceledException(string.Format("Execution aborted by {0}. No test plan configured.", Name));
            
            // Detect if the plan was not loaded or if the path has been changed since loading it.
            // Note, there is some funky things related to TestPlanDir that are not checked but 99% of use-cases are.
            var expandedPath = filepath.Text;
            if (loadedPlanPath != expandedPath && File.Exists(expandedPath) && HideSteps)
            {
                LoadTestPlan();
            }
            if (loadedPlanPath != expandedPath)
                throw new OperationCanceledException(string.Format("Execution aborted by {0}. Test plan not loaded.", Name));
        }

        class SubPlanResultListener : ResultListener
        {
            readonly ResultSource proxy;
            public SubPlanResultListener(ResultSource proxy) => this.proxy = proxy;

            public override void OnResultPublished(Guid stepRunId, ResultTable result)
            {
                base.OnResultPublished(stepRunId, result);
                proxy.PublishTable(result);
            }
        }

        class LogForwardingTraceListener : ILogListener
        {
            readonly ILogContext2 forwardTo;
            public LogForwardingTraceListener(ILogContext2 forwardTo) => this.forwardTo = forwardTo;
            public void EventsLogged(IEnumerable<Event> Events)
            {
                foreach (var evt in Events)
                {
                    if (evt.Source == "TestPlan" || evt.Source == "N/A")
                        if(evt.EventType != (int)LogEventType.Error)
                            continue;
                    forwardTo.AddEvent(evt);
                }
            }

            public void Flush() { }
        }
        public override void Run()
        {
            if (HideSteps)
            {
                LogForwardingTraceListener forwarder = null;
                var xml = plan.SerializeToString();
                if(OpenTap.Log.Context is ILogContext2 ctx2)
                    forwarder = new LogForwardingTraceListener(ctx2);
                using (Session.Create())
                {   
                    var plan2 = Utils.DeserializeFromString<TestPlan>(xml);
                    plan2.PrintTestPlanRunSummary = false;
                    if(forwarder != null)
                        OpenTap.Log.AddListener(forwarder);
                    var subRun = plan2.Execute(new IResultListener[] {new SubPlanResultListener(Results)});
                    UpgradeVerdict(subRun.Verdict);
                }
            }
            else
            {
                foreach (var run in RunChildSteps())
                    UpgradeVerdict(run.Verdict);
            }
        }

        [ThreadStatic] static List<GuidMapping> CurrentMappings;

        static readonly Memorizer<string, XDocument> dict = new Memorizer<string, XDocument>(p =>
        {
            return XDocument.Load(p, LoadOptions.SetLineInfo);
            }) 
            {
                // Validator is to reload the file if it has been changed.
                // Assuming it is much faster to check file write time than to read and parse it. Testing has verified this.
                Validator = str =>
                {
                    var file = new FileInfo(str);
                    return $"{file.LastWriteTime} {file.Length}";
                },
                MaxNumberOfElements = 100
            };

        
        
        static XDocument ReadCachedXmlFile(string path)
        {
            var cached = dict.Invoke(path);
            // The deserializer may modify the XDocument class so it must be cloned by constructing a new XDocument (this causes a deep clone to be made).
            return new XDocument(cached);
        }

        internal TestPlan plan;
        string loadedPlanPath;
        
        void UpdateStep()
        {
            if (CurrentMappings == null)
                CurrentMappings = new List<GuidMapping>();
            // Load GUID mappings which is every two GUIDS between <Guids/> and <Guids/>
            var mapping = new Dictionary<Guid, Guid>();
            var allMapping = this.mapping.Concat(CurrentMappings).ToList();
            foreach (var mapItem in allMapping)
            {
                mapping[mapItem.Guid1] = mapItem.Guid2;
            }
            ITestStep parent = this;
            while(parent != null)
            {
                if(parent is TestPlanReference tpr)
                {
                    foreach(var mp in tpr.mapping)
                    {
                        mapping[mp.Guid1] = mp.Guid2;
                    }
                }
                parent = parent.Parent as ITestStep;
            }

            object testplandir = null;
            var currentSerializer = TapSerializer.GetObjectDeserializer(this);
            if (currentSerializer != null && currentSerializer.ReadPath != null)
                testplandir = System.IO.Path.GetDirectoryName(currentSerializer.ReadPath);
            
            var refPlanPath = Filepath.Expand(testPlanDir: testplandir as string);
            refPlanPath = refPlanPath.Replace('\\', '/');
            if (!File.Exists(refPlanPath))
            {
                Log.Warning("File does not exist: \"{0}\"", refPlanPath);
                return;
            }
            ChildTestSteps.Clear();
            var prevc = CurrentMappings;
            try
            {
                try
                {
                    if (referencedPlanPaths == null)
                        referencedPlanPaths = new HashSet<string>();
                    if (currentSerializer?.ReadPath == refPlanPath || referencedPlanPaths.Add(refPlanPath) == false)
                        throw new Exception("Test plan reference is trying to load itself leading to recursive loop.");

                    var newSerializer = new TapSerializer();
                    if (currentSerializer != null)
                        newSerializer.GetSerializer<ExternalParameterSerializer>().PreloadedValues.MergeInto(currentSerializer.GetSerializer<ExternalParameterSerializer>().PreloadedValues);
                    var ext = newSerializer.GetSerializer<ExternalParameterSerializer>();
                    foreach (var e in ExternalParameters)
                    {
                        /* If the value can be converted to/from a string, do so */
                        if (StringConvertProvider.TryGetString(e.GetValue(plan), out var str))
                        {
                            ext.PreloadedValues[e.Name] = str;
                        }
                        /* otherwise, write a null value to ensure the current value is not overwritten in the serializer. We can write it back later. */
                        else
                        {
                            ext.PreloadedValues[e.Name] = null;
                        }
                    }

                    CurrentMappings = allMapping;

                    loadedPlanPath = filepath.Text;

                    var doc = ReadCachedXmlFile(refPlanPath);
                    TestPlan tp = (TestPlan)newSerializer.Deserialize(doc, TypeData.FromType(typeof(TestPlan)), true, refPlanPath) ;
                    plan = tp;

                    using (var algo = System.Security.Cryptography.SHA1.Create())
                    {
                        using (var ms = new MemoryStream())
                        {
                            doc.Save(ms, SaveOptions.DisableFormatting);
                            Hash = BitConverter.ToString(algo.ComputeHash(ms.ToArray()), 0, 8)
                                .Replace("-", string.Empty);
                        }
                    }

                    { /* write back any parameters that could not be converted to a string */
                        var newParameters = TypeData.GetTypeData(tp).GetMembers().OfType<ParameterMemberData>()
                            .ToArray();
                        ILookup<string, ParameterMemberData> lookup = null;
                        foreach (var par in newParameters)
                        {
                            if (par.GetValue(plan) == null)
                            {
                                lookup ??= ExternalParameters.ToLookup(m => m.Name);
                                var prevParameter = lookup[par.Name].FirstOrDefault();
                                if (prevParameter != null)
                                    par.SetValue(plan, prevParameter.GetValue(plan));
                            }
                        }
                        ExternalParameters = newParameters;
                    }


                    var flatSteps = Utils.FlattenHeirarchy(tp.ChildTestSteps, x => x.ChildTestSteps);

                    StepIdMapping = flatSteps.ToDictionary(x => x, x => x.Id);

                    foreach (var step in flatSteps)
                    {
                        Guid id;
                        if (mapping.TryGetValue(step.Id, out id))
                            step.Id = id;
                    }

                    if (HideSteps == false)
                    {
                        foreach (var item in tp.ChildTestSteps)
                            ChildTestSteps.Add(item);

                        foreach (var step in RecursivelyGetChildSteps(TestStepSearch.All))
                        {
                            step.IsReadOnly = true;
                            step.ChildTestSteps.IsReadOnly = true;
                            step.OnPropertyChanged("");
                        }
                    }

                    if (currentSerializer == null)
                    {
                        // if currentSerializer is set, it means that we are loading a previously saved test plan reference.
                        // Hence these things will be automatically set up.
                        // otherwise we need to trasfer mixins and dynamic member values.
                        
                        // transfer mixins
                        var thisType = TypeData.GetTypeData(this);

                        foreach (var member in TypeData.GetTypeData(tp).GetMembers())
                        {
                            if (member is MixinMemberData mixinMember)
                            {
                                if (thisType.GetMember(member.Name) != null) continue;
                                var mixin = mixinMember.Source;
                                var mem = mixin.ToDynamicMember(thisType);
                                if (mem == null)
                                {
                                    if (mixin is IValidatingObject validating && validating.Error is string err && string.IsNullOrEmpty(err) == false)
                                    {
                                        Log.Error($"Unable to load mixin: {err}");
                                    }
                                    else
                                    {
                                        Log.Error($"Unable to load mixin: {TypeData.GetTypeData(mixin)?.GetDisplayAttribute()?.Name ?? mixin.ToString()}");
                                    }
                                    continue;
                                }
                                // transfer  the value from the test plan instance to this instance.
                                var value = member.GetValue(tp);
                                DynamicMember.AddDynamicMember(this, mem);
                                mem.SetValue(this, value);
                            }
                        }

                        // transfer dynamic member values.
                        foreach (var member in TypeData.GetTypeData(tp).GetMembers().Where(mem => mem is DynamicMember))
                        {
                            if (member.HasAttribute<XmlIgnoreAttribute>())
                                continue;
                            member.SetValue(this, member.GetValue(tp));
                        }
                    }


                }
                finally
                {
                    if (referencedPlanPaths != null)
                    {
                        referencedPlanPaths.Remove(refPlanPath);
                        if (referencedPlanPaths.Count == 0) referencedPlanPaths = null;
                    }
                    CurrentMappings = prevc;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Unable to read '{0}'.", Filepath.Text);
                Log.Error(ex);
            }
        }
        
        /// <summary> Used to determine if a step ID has changed from the time it was loaded to the time it is saved. </summary>
        Dictionary<ITestStep, Guid> StepIdMapping { get; set; }
        public List<GuidMapping> mapping;

        [Browsable(false)]
        [AnnotationIgnore]
        public List<GuidMapping> StepMapping
        {
            get {
                if (StepIdMapping == null)
                    return new List<GuidMapping>();
                var subMappings = Utils.FlattenHeirarchy(ChildTestSteps, x => x.ChildTestSteps).OfType<TestPlanReference>().SelectMany(x => (IEnumerable< KeyValuePair < ITestStep, Guid > >)x.StepIdMapping ?? Array.Empty<KeyValuePair<ITestStep, Guid>>()).ToArray();
                return StepIdMapping.Concat(subMappings).Select(x => new GuidMapping { Guid2 = x.Key.Id, Guid1 = x.Value }).Where(x => x.Guid1 != x.Guid2).ToList(); }
            set { mapping = value; }
        }

        [Browsable(true)]
        [Display("Load Test Plan", Order: 1, Description: "Load the selected test plan.")]
        [EnabledIf(nameof(CanOpenFile))]
        public void LoadTestPlan() => loadTestPlan();
        
        public void loadTestPlan()
        {
            ChildTestSteps.Clear();
            
            if (string.IsNullOrWhiteSpace(Filepath))
            {
                ExternalParameters = Array.Empty<ParameterMemberData>();
                return;
            }
            
            UpdateStep();
        }

        string GetPath()
        {
            object testplandir = null;
            var currentSerializer = TapSerializer.GetObjectDeserializer(this);
            if (currentSerializer != null && currentSerializer.ReadPath != null)
                testplandir = System.IO.Path.GetDirectoryName(currentSerializer.ReadPath);
            
            var refPlanPath = Filepath.Expand(testPlanDir: testplandir as string);
            refPlanPath = refPlanPath.Replace('\\', '/');
            
            return refPlanPath;
        }

        public bool anyStepsLoaded => ChildTestSteps.Any() && GetPath() is string path && File.Exists(path);

        
        
        [Display("Are you sure?")]
        class ConvertWarning
        {
            public enum ConvertOrCancel
            {
                Convert,
                Cancel
            }
            [Browsable(true)]
            [Layout(LayoutMode.FullRow | LayoutMode.WrapText)]
            public string Message { get; }

            [Layout(LayoutMode.FloatBottom | LayoutMode.FullRow)]
            [Submit]
            public ConvertOrCancel Response { set; get; } = ConvertOrCancel.Cancel;

            public ConvertWarning(bool multiSelect)
            {
                Message = "Are you sure you want to convert the Test Plan Reference to a Sequence?\n\nAny future changes to the referenced test plan will not be reflected.";
                if(multiSelect)
                    Message += "\n\nThis will affect all selected test steps.";
            }
        }
            

        /// <summary> Convert the test plan reference to a sequence step. This will pop up a dialog. </summary>
        [Browsable(true)]
        [Display("Convert to Sequence", "Convert the test plan reference to a sequence step.", Order: 1.1)]
        [EnabledIf(nameof(anyStepsLoaded), HideIfDisabled = true)]
        public void ConvertToSequence()
        {
            // only show the dialog if the user input interface has been set.
            // otherwise treat this as a normal method.
            bool showDialog = UserInput.Interface != null;

            ConvertToSequence(showDialog, false);
        }

        internal bool ConvertToSequence(bool userShouldConfirm, bool multiSelect)
        {
            if (userShouldConfirm)
            {
                var warn = new ConvertWarning(multiSelect);
                UserInput.Request(warn, true);
                if (warn.Response == ConvertWarning.ConvertOrCancel.Cancel)
                    return false;
            }

            // This test plan contains a clone of all the test steps in 'this'.
            var subPlan = TestPlan.Load(GetPath());
            
            // the new sequence step.
            var seq = new SequenceStep
            {
                // Replace Test Plan Reference if the name starts with that.
                Name = Name.StartsWith("Test Plan Reference") ? ("Sequence" + Name.Substring("Test Plan Reference".Length)) : Name,
                Parent = Parent,
                Id = this.Id
            };
            
            ChildItemVisibility.SetVisibility(seq, ChildItemVisibility.GetVisibility(this));
            
            // first figure out which steps are parameterized to this in the list of child steps.
            var parameters = TypeData.GetTypeData(subPlan).GetMembers().OfType<ParameterMemberData>()
                .Select(e => new {Name = e.Name , Members = e.ParameterizedMembers.ToArray()}).ToArray();
            foreach (var param in TypeData.GetTypeData(subPlan).GetMembers().OfType<ParameterMemberData>().ToArray())
            {
                // unparameterize those.
                param.Remove();
            }
            
            // now copy the steps over to the new step. 
            var steps = subPlan.ChildTestSteps.ToArray();
            subPlan.ChildTestSteps.Clear();
            foreach (var step in steps)
            {
                seq.ChildTestSteps.Add(step);
            }
            ChildTestSteps.IsReadOnly = false;
            var parent = this.Parent;
            var idx = parent.ChildTestSteps.IndexOf(this);
            parent.ChildTestSteps[idx] = seq;
            
            // This section copies parameters over from the test plan reference to the sequence
            foreach (var parameter in parameters)
            {
                var name = parameter.Name;
                ParameterMemberData parameterMember = null;
                foreach (var member in parameter.Members)
                {
                    parameterMember = member.Member.Parameterize(seq, member.Source, name);
                }
                parameterMember.SetValue(seq, ExternalParameters.First(x => x.Name == name).GetValue(this));
            }
            
            // This section copies mixins over from the test plan reference to the sequence           
            var seqType = TypeData.GetTypeData(seq);

            // Some MixinMemberData members may be seen multiple times
            // e.g. multiple EmbeddedMemberData can have the same OwnerMember
            // Store the seen members so that we can skip it
            HashSet<MixinMemberData> seen = new HashSet<MixinMemberData>();

            foreach (var member in TypeData.GetTypeData(this).GetMembers())
            {
                MixinMemberData mixinMember = member switch
                {
                    MixinMemberData mixin => mixin,
                    IEmbeddedMemberData emb => emb.OwnerMember as MixinMemberData,
                    var _ => null
                };


                if (mixinMember != null && !seen.Contains(mixinMember))
                {
                    seen.Add(mixinMember);
                    var mixin = mixinMember.Source;
                    var mem = mixin.ToDynamicMember(seqType);
                    if (mem == null)
                    {
                        if (mixin is IValidatingObject validating && validating.Error is string err && string.IsNullOrEmpty(err) == false)
                        {
                            Log.Error($"Unable to load mixin: {err}");
                        }
                        else
                        {
                            Log.Error($"Unable to load mixin: {TypeData.GetTypeData(mixin)?.GetDisplayAttribute()?.Name ?? mixin.ToString()}");
                        }
                        continue;
                    }
                    DynamicMember.AddDynamicMember(seq, mem);
                    mem.SetValue(seq, mixinMember.GetValue(this));
                }
            }

            
            // This section migrates parameters to the new step.
            
            ParameterMemberData GetParameter(object target, object source, IMemberData parameterizedMember)
            {
                var parameterMembers = TypeData.GetTypeData(target).GetMembers().OfType<ParameterMemberData>();
                foreach (var fwd in parameterMembers)
                {
                    if (fwd.ContainsMember((source, parameterizedMember)))
                        return fwd;
                }
                return null;
            }
            foreach (var member in TypeData.GetTypeData(this).GetMembers())
            {
                if (member.IsParameterized(this))
                {
                    
                    var parent2 = Parent;
                    while (parent2 != null)
                    {
                        var p = GetParameter(parent2, this, member);
                        if (p != null)
                        {
                            TypeData.GetTypeData(seq).GetMember(member.Name).Parameterize(parent2, seq, p.Name);
                            member.Unparameterize(p, this);
                            
                            break;
                        }
                        parent2 = parent2.Parent;
                    }
                }
            }


            // This section copies dynamic member values.

            foreach (var member in TypeData.GetTypeData(this).GetMembers().Where(mem => mem is DynamicMember))
            {
                if (member.HasAttribute<XmlIgnoreAttribute>())
                    continue;
                var val = member.GetValue(this);
                val = new ObjectCloner(val).Clone(false, this, member.TypeDescriptor);
                member.SetValue(seq, val);
            }
            // .. and done.
            return true;
        }
        
        internal ParameterMemberData[] ExternalParameters { get; private set; } = Array.Empty<ParameterMemberData>();
    }
}
