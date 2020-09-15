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
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

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
        /// This counter is used to prevent recursive TestPlan references.
        /// </summary>
        private static int LevelCounter = 0;

        ITestStepParent parent;
        // The PlanDir of 'this' should be ignored when calculating Filepath, so the MacroString context is set to the parent.
        [XmlIgnore]
        public override ITestStepParent Parent { get => parent; set { Filepath.Context = value; parent = value; } }

        MacroString filepath = new MacroString();
        string currentlyLoaded = null;
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
            Rules.Add(() => (string.IsNullOrWhiteSpace(Filepath) || File.Exists(Filepath)), "File does not exist.", "Filepath");
            StepMapping = new List<GuidMapping>();
        }

        public override void PrePlanRun()
        {
            base.PrePlanRun();

            if (string.IsNullOrWhiteSpace(Filepath))
                throw new OperationCanceledException(string.Format("Execution aborted by {0}. No test plan configured.", Name));
        }

        public override void Run()
        {
            foreach (var run in RunChildSteps())
                UpgradeVerdict(run.Verdict);
        }

        [ThreadStatic]
        static List<GuidMapping> CurrentMappings;

        static Memorizer<string, XDocument> dict = new Memorizer<string, XDocument>(p =>
        {
            using (var fstr = File.OpenRead(p))
                return XDocument.Load(fstr, LoadOptions.SetLineInfo);
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
        
        void UpdateStep()
        {
            if(CurrentMappings == null)
                CurrentMappings = new List<GuidMapping>();
            // Load GUID mappings which is every two GUIDS between <Guids/> and <Guids/>
            var mapping = new Dictionary<Guid, Guid>();
            var allMapping = this.mapping.Concat(CurrentMappings).ToList();
            foreach (var mapitem in allMapping)
            {
                mapping[mapitem.Guid1] = mapitem.Guid2;
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
            
            var Data = Filepath.Expand(testPlanDir: testplandir as string);
            Data = Data.Replace('\\', '/');
            if (!File.Exists(Data))
            {
                Log.Warning("File does not exist: \"{0}\"", Data);
                return;
            }
            ChildTestSteps.Clear();
            var prevc = CurrentMappings;
            try
            {
                LevelCounter++;
                try
                {
                    if (LevelCounter > 16)
                        throw new Exception("Test plan reference level is too high. You might be trying to load a recursive test plan.");

                    var newSerializer = new TapSerializer();
                    if (currentSerializer != null)
                        newSerializer.GetSerializer<ExternalParameterSerializer>().PreloadedValues.MergeInto(currentSerializer.GetSerializer<ExternalParameterSerializer>().PreloadedValues);
                    var ext = newSerializer.GetSerializer<ExternalParameterSerializer>();
                    ExternalParameters.ToList().ForEach(e =>
                    {
                        ext.PreloadedValues[e.Name] = StringConvertProvider.GetString(e.GetValue(plan));
                    });

                    CurrentMappings = allMapping;

                    TestPlan tp = (TestPlan)newSerializer.Deserialize(readXmlFile(Data), TypeData.FromType(typeof(TestPlan)), true, Data) ;
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
                    ChildTestSteps.AddRange(tp.ChildTestSteps);

                    foreach (var step in RecursivelyGetChildSteps(TestStepSearch.All))
                    {
                        step.IsReadOnly = true;
                        step.ChildTestSteps.IsReadOnly = true;
                        step.OnPropertyChanged("");
                    }
                }
                finally
                {
                    LevelCounter--;
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
