//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace OpenTap
{
    /// <summary>
    /// Class containing a test plan.
    /// </summary>
    public partial class TestPlan : INotifyPropertyChanged, ITestStepParent
    {
        internal static readonly TraceSource Log = OpenTap.Log.CreateSource(nameof(TestPlan));        
        private TestStepList _Steps;
        private ITestPlanRunMonitor[] monitors; // For locking/unlocking or generally monitoring test plan start/stop.
        
        /// <summary>
        /// Field for external test plan parameters.
        /// </summary>
        [XmlIgnore]
        public ExternalParameters ExternalParameters { get; private set; }

        /// <summary>
        /// A collection of TestStepBase steps.
        /// </summary>
        [Browsable(false)]
        public TestStepList Steps
        {
            get => _Steps;
            set
            {
                if (value == _Steps) return;
                _Steps = value;
                _Steps.Parent = this;
                OnPropertyChanged(nameof(Steps));
                OnPropertyChanged(nameof(ChildTestSteps));
            }
        }

        /// <summary>
        /// List of test steps that make up this plan.  
        /// </summary>
        public TestStepList ChildTestSteps => _Steps;


        /// <summary>
        /// Always null for test plan.
        /// </summary>
        [XmlIgnore]
        [Browsable(false)]
        public ITestStepParent Parent { get => null; set { } }

        /// <summary>
        /// Gets the subset of steps that are enabled.
        /// </summary>
        [XmlIgnore]
        public ReadOnlyCollection<ITestStep> EnabledSteps
        {
            get
            {
                return Steps
                    .GetSteps(TestStepSearch.EnabledOnly)
                    .ToList()
                    .AsReadOnly();
            }
        }

        /// <summary>
        /// Gets or sets the name. This is usually the name of the file where the test plan is saved, without the .TapPlan extension.
        /// </summary>
        [XmlIgnore]
        [MetaData(macroName: "TestPlanName")]
        [Browsable(true)]
        [Display("Name", "The test plan name. This is usually based on the name of the file to which it is saved.", Order: 1)]
        public string Name
        {
            get
            {
                if (Path == null)
                    return "Untitled";
                return System.IO.Path.GetFileNameWithoutExtension(Path);
            }
        }

        /// <summary>
        /// A synchronous event that allows breaking the execution of the TestPlan by blocking the TestPlan execution thread. 
        /// It is raised prior to executing the <see cref="TestStep.Run"/> method of each <see cref="TestStep"/> in the TestPlan. 
        /// TestSteps may also raise this event from inside the <see cref="TestStep.Run"/> method.  
        /// </summary>
        public event EventHandler<BreakOfferedEventArgs> BreakOffered;

        /// <summary>
        /// True if the test plan is waiting in a break.
        /// </summary>
        public bool IsInBreak => breakRefCount > 0; 

        int breakRefCount = 0;

        /// <summary>   Raises the <see cref="BreakOffered"/> event. </summary>
        /// <remarks> If called by Tap internal code at the start of a run, the TestStepRun.Verdict will be equal to pending.</remarks>
        /// <remarks> If called via a user written TestStep, the TestStepRun.Verdict will be equal to running.</remarks>
        /// <param name="args"> Event information to send to registered event handlers. </param>
        internal void OnBreakOffered(BreakOfferedEventArgs args)
        {
            if (args == null) return;
            if (args.TestStepRun == null) return;

            if (BreakOffered != null)
            {
                Interlocked.Increment(ref breakRefCount);
                try
                {
                    BreakOffered(this, args);
                }
                finally
                {
                    Interlocked.Decrement(ref breakRefCount);
                }
            }

            //Just in case the user requested to abort
            TapThread.ThrowIfAborted();
        }

        bool locked = false;
        /// <summary>
        /// Locks the TestPlan to signal that it should not be changed.
        /// The GUI respects this.
        /// </summary>
        [XmlAttribute]
        [Display("Locked", "Checking this makes the test plan read-only.", Order: 2)]
        public bool Locked
        {
            get => locked;
            set
            {
                if (value != locked)
                {
                    locked = value;
                    OnPropertyChanged(nameof(Locked));
                }
            }
        }

        TestPlanRun _currentRun;
        TestPlanRun CurrentRun
        {
            set
            {
                _currentRun = value;
                OnPropertyChanged(nameof(IsRunning));
            }
            get => _currentRun;
        }

        /// <summary> True if this TestPlan is currently running. </summary>
        public bool IsRunning => CurrentRun != null;

        /// <summary> </summary>
        public TestPlan()
        {
            _Steps = new TestStepList();
            _Steps.Parent = this;
            ExternalParameters = new ExternalParameters(this);
        }
        
        /// <summary>  Gets or sets if the test plan XML for this test plan should be cached. </summary>
        [XmlIgnore]
        public bool CacheXml { get; set; }

        byte[] xmlCache = null;
        
        /// <summary>  Flushes the test plan XML cache. </summary>
        public void FlushXmlCache() => xmlCache = null;

        /// <summary> Get the cached XML (or null if it is not cached)</summary>
        public byte[] GetCachedXml() => xmlCache;

        
        #region Load/Save TestPlan
        /// <summary>
        /// Saves this TestPlan to a stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> in which to save the TestPlan.</param>
        public void Save(Stream stream)
        {
            if (xmlCache != null)
            {
                stream.Write(xmlCache, 0, xmlCache.Length);
                return;
            }
            
            if (stream == null)
                throw new ArgumentNullException("stream");
            var ser = new TapSerializer();
            if (CacheXml)
            {
                var str = new MemoryStream();
                ser.Serialize(str, this);
                xmlCache = str.ToArray();
                str.CopyTo(stream);
            }
            else
            {
                ser.Serialize(stream, this);
            }
        }
        /// <summary>
        /// Saves this TestPlan to a file path.
        /// </summary>
        /// <param name="filePath">The file path in which to save the TestPlan.</param>
        public void Save(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath");
            using (StreamWriter file = new StreamWriter(filePath))
            {
                Save(file.BaseStream);
            }
            Path = filePath;
            OnPropertyChanged(nameof(Path));
            OnPropertyChanged(nameof(Name));
        }

        /// <summary> Load a TestPlan. </summary>
        /// <param name="filePath">The file path of the TestPlan.</param>
        /// <returns>Returns the new test plan.</returns>
        public static TestPlan Load(string filePath) => Load(filePath, false);
        
        /// <summary> Load a TestPlan. </summary>
        /// <param name="filePath">The file path of the TestPlan.</param>
        /// <param name="cacheXml"> Gets or sets if the XML should be cached. </param>
        /// <returns>Returns the new test plan.</returns>
        public static TestPlan Load(string filePath, bool cacheXml)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));
            var timer = Stopwatch.StartNew();
            // Open document
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var loadedPlan = Load(fs, filePath, cacheXml);
                Log.Info(timer, "Loaded test plan from {0}", filePath);
                return loadedPlan;
            }
        }

        enum PlanLoadErrorResponse
        {
            Ignore,
            Abort
        }

        /// <summary>
        /// Exception occuring when a test plan loads.
        /// </summary>
        public class PlanLoadException : Exception
        {
            /// <summary> </summary>
            /// <param name="message"></param>
            public PlanLoadException(string message) : base(message)
            {

            }
        }

        class PlanLoadError
        {
            [Browsable(true)]
            [Layout(LayoutMode.FullRow, 2)]
            public string Message { get; private set; } = "Parts of the test plan did not load correctly. See the log for more details.\n\nDo you want to ignore these errors and use a corrupt test plan?";
            public string Name { get; private set; } = "Errors occured while loading test plan.";
            [Layout(LayoutMode.FullRow | LayoutMode.FloatBottom)]
            [Submit]
            public PlanLoadErrorResponse Response { get; set; } = PlanLoadErrorResponse.Ignore;
        }

        /// <summary> Load a TestPlan. </summary>
        /// <param name="stream">The stream from which the file is actually loaded.</param>
        /// <param name="path">The path to the file. This will be tha value of <see cref="TestPlan.Path"/> on the new TestPlan.</param>
        /// <returns>Returns the new test plan.</returns>
        public static TestPlan Load(Stream stream, string path) => Load(stream, path, false);
        
        /// <summary> Load a TestPlan. </summary>
        /// <param name="stream"> The stream from which the file is actually loaded. </param>
        /// <param name="path"> The path to the file. This will be tha value of <see cref="TestPlan.Path"/> on the new TestPlan. </param>
        /// <param name="cacheXml"> Gets or sets if the XML should be cached. </param>
        /// <param name="serializer">Optionally the serializer used for deserializing the test plan. </param>
        /// <returns>Returns the new test plan.</returns>
        public static TestPlan Load(Stream stream, string path, bool cacheXml, TapSerializer serializer = null)
        {
            return Load(stream, path, cacheXml, serializer, false);
        }

        internal static TestPlan Load(Stream stream, string path, bool cacheXml, TapSerializer serializer, bool IgnoreLoadErrors)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            
            if (cacheXml)
            {
                var memstr = new MemoryStream();
                stream.CopyTo(memstr);
                stream = memstr;
                memstr.Seek(0, SeekOrigin.Begin);
            }
            
            serializer = serializer ?? new TapSerializer();
            var plan = (TestPlan)serializer.Deserialize(stream, type: TypeData.FromType(typeof(TestPlan)), path: path);
            var errors = serializer.Errors;
            if (IgnoreLoadErrors == false && errors.Any())
            {
                // eventual errors were already printed.
                var err = new PlanLoadError();
                UserInput.Request(err, false);
                var respValue = err.Response;
                
                if (respValue == PlanLoadErrorResponse.Abort)
                {
                    throw new PlanLoadException(string.Join("\n", errors));
                }
            }

            if (cacheXml)
            {
                plan.CacheXml = true;
                plan.xmlCache = ((MemoryStream) stream).ToArray();
            }
            return plan;
        }

        /// <summary>
        /// Reload a TestPlan transferring the current execution state of the current plan to the new one.
        /// </summary>
        /// <param name="filestream">The filestream from which the plan loaded</param>
        /// <returns>Returns the new test plan.</returns>
        public TestPlan Reload(Stream filestream)
        {
            if (filestream == null)
                throw new ArgumentNullException("filestream");
            if (this.IsRunning)
                throw new InvalidOperationException("Cannot reload while running.");
            var serializer = new TapSerializer();
            var plan = (TestPlan)serializer.Deserialize(filestream, type: TypeData.FromType(typeof(TestPlan)), path: Path);
            plan.currentExecutionState = this.currentExecutionState;
            plan.Path = this.Path;
            return plan;
        }

        /// <summary>
        /// Returns the list of plugins that are required to use the test plan. 
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        public static List<string> GetPluginsRequiredToLoad(string filepath)
        {
            if (filepath == null)
                throw new ArgumentNullException("filepath");
            List<string> types = new List<string>();

            string[] lines = File.ReadAllLines(filepath);
            Regex rx = new Regex("<TestStep\\s+type=\"(?<name>[^\"]+)\"");
            foreach (string line in lines)
            {
                Match match = rx.Match(line);
                if (match.Success)
                {
                    types.Add(match.Groups["name"].Value);
                }
            }
            return types;
        }

        #endregion

        #region INotifyPropertyChanged Members
        /// <summary>
        /// Event handler to on property changed.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// When a property changes this function is called.
        /// </summary>
        /// <param name="name">Inputs the string for the property changed.</param>
        protected virtual void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
                UserInput.NotifyChanged(this, name);
            }
        }
        #endregion

        /// <summary>
        /// Gets where this plan was last saved or loaded from. It might be null.
        /// </summary>
        [XmlIgnore]
        public string Path { get; internal set; }

        /// <summary> The directory where the test plan is stored.</summary>
        [MetaData(macroName: "TestPlanDir")]
        public string Directory
        {
            get
            {
                try
                {
                    return string.IsNullOrWhiteSpace(Path) ? null : System.IO.Path.GetDirectoryName(Path);
                }
                catch
                {
                    // probably an invalid path.
                    return null;
                }
            }
        }
    }

    /// <summary>
    /// Provides data for the <see cref="TestPlan.BreakOffered"/> event.
    /// </summary>
    public class BreakOfferedEventArgs : EventArgs
    {
        /// <summary>
        /// Details of the currently running (or about to be running) step.
        /// </summary>
        public TestStepRun TestStepRun { get; private set; }

        /// <summary>
        /// Indicates whether the this event was raised by the engine when it starts running a TestStep (true) 
        /// or during the run of a TestStep from within a TestStep itself (false).
        /// </summary>
        /// <remarks>This value can also be determined as TestStepRun.Verdict == TestStep.VerdictType.Pending.</remarks>
        public bool IsTestStepStarting { get; private set; }
        
        /// <summary>
        /// Specifies that the current step should not be run, but instead flow control should move to another step.
        /// It is up to the test step to honor this, in some cases it will not be possible. When supported,
        /// TestStepRun.SupportsJumpTo should be set to true.
        /// </summary>
        public Guid? JumpToStep { get; set; }

        internal BreakOfferedEventArgs(TestStepRun testStepRun, bool isTestStepStarting)
        {
            IsTestStepStarting = isTestStepStarting;
            TestStepRun = testStepRun;
        }
    }
}
