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
        /// Mapping between step GUIDs. Oldversion -> New Version.
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

        [XmlIgnore]
        [Browsable(false)]
        [AnnotationIgnore]
        public new TestStepList ChildTestSteps
        {
            get { return base.ChildTestSteps; }
            set { base.ChildTestSteps = value; }
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

        static XDocument readXmlFile(string path) => dict.Invoke(path);

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
                    ExternalParameters.ToList().ForEach(e =>
                    {
                        ext.PreloadedValues[e.Name] = StringConvertProvider.GetString(e.GetValue(plan));
                    });

                    CurrentMappings = allMapping;

                    loadedPlanPath = filepath.Text;

                    TestPlan tp = (TestPlan)newSerializer.Deserialize(readXmlFile(refPlanPath), TypeData.FromType(typeof(TestPlan)), true, refPlanPath) ;
                    plan = tp;

                    ExternalParameters = TypeData.GetTypeData(tp).GetMembers().OfType<ParameterMemberData>().ToArray();

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
                        ChildTestSteps.AddRange(tp.ChildTestSteps);

                        foreach (var step in RecursivelyGetChildSteps(TestStepSearch.All))
                        {
                            step.IsReadOnly = true;
                            step.ChildTestSteps.IsReadOnly = true;
                            step.OnPropertyChanged("");
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
        public void LoadTestPlan()
        {
            loadTestPlan();
        }
        
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
        internal ParameterMemberData[] ExternalParameters { get; private set; } = Array.Empty<ParameterMemberData>();
    }
}
