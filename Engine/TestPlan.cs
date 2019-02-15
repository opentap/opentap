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
        #region Nested Types
        /// <summary>
        /// Exception that can be thrown by TestStep code to abort execution of the TestPlan immediately
        /// (other exceptions cause the TestPlan to skip to next TestStep in the plan).
        /// </summary>
        public class AbortException : Exception
        {
            /// <summary>
            /// Gets or sets the TestStep that caused the exception.
            /// </summary>
            public ITestStep Step { get; set; }

            bool setVerdict; // explicit to avoid obsolete warnings.

            /// <summary>
            /// Whether or no to set the verdict to Aborted when caught.
            /// </summary>
            [Obsolete("This is no longer being used by test plan execution. To be removed in 9.0.")]
            public bool SetVerdict { get { return setVerdict; } set { setVerdict = value; } }

            /// <summary>
            /// Aborts the execution of the TestPlan.
            /// </summary>
            /// <param name="step">Inputs the TestStepBase step.</param>
            /// <param name="message">Inputs the message.</param>
            /// <param name="inner">Inputs the inner exception.</param>
            /// <param name="setVerdict">If true (the default), this exception will also cause the verdict of the currently executing TestStep to become TestStep.RunVerdict.Aborted </param>
            public AbortException(ITestStep step, string message, Exception inner, bool setVerdict = true)
                : base(message, inner)
            {
                Step = step;
                this.setVerdict = setVerdict;
            }
            /// <summary>
            /// Aborts the execution of the TestPlan.
            /// </summary>
            /// <param name="message">Inputs the message.</param>
            /// <param name="setVerdict">If true (the default), this exception will also cause the verdict of the currently executing TestStep to become TestStep.RunVerdict.Aborted </param>
            public AbortException(string message, bool setVerdict = true)
                : base(message)
            {
                this.setVerdict = setVerdict;
            }
        }


        #endregion
        internal static readonly TraceSource Log =  OpenTap.Log.CreateSource("TestPlan");
        private CancellationTokenSource abortTokenSource = new CancellationTokenSource();
        private TestStepList _Steps;

        /// <summary>
        /// Field for external test plan parameters.
        /// </summary>
        [XmlIgnore]
        public ExternalParameters ExternalParameters { get; private set; }
        /// <summary>
        /// Gets the currently running <see cref="TestPlan"/>.
        /// </summary>
        internal static TestPlan CurrentlyRunningPlan { get; private set; }

        /// <summary>
        /// A collection of TestStepBase steps.
        /// </summary>
        public TestStepList Steps
        {
            get
            {
                return _Steps;
            }
            set
            {
                if (value == _Steps) return;
                _Steps = value;
                _Steps.Parent = this;
                OnPropertyChanged("Steps");
                OnPropertyChanged("ChildTestSteps");
            }
        }

        /// <summary>
        /// List of test steps that make up this plan.  
        /// </summary>
        public TestStepList ChildTestSteps
        {
            get { return _Steps; }
        }


        /// <summary>
        /// Always null for test plan.
        /// </summary>
        [XmlIgnore]
        public ITestStepParent Parent { get { return null; } set { } }

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
        public bool IsInBreak
        {
            get { return breakRefCount > 0; }
        }

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
            Sleep();
        }

        bool locked = false;
        /// <summary>
        /// Locks the TestPlan to signal that it should not be changed.
        /// The GUI respects this.
        /// </summary>
        [XmlAttribute]
        public bool Locked
        {
            get
            {
                return locked;
            }
            set
            {
                if (value != locked)
                {
                    locked = value;
                    OnPropertyChanged("Locked");
                }
            }
        }

        private bool _IsRunning;
        /// <summary>
        /// True if this TestPlan is currently running.
        /// </summary>
        [XmlIgnore]
        public System.Boolean IsRunning
        {

            get { return _IsRunning; }
            private set
            {
                if (value)
                    CurrentlyRunningPlan = this;
                else
                    CurrentlyRunningPlan = null;
                _IsRunning = value;
                OnPropertyChanged("IsRunning");
            }
        }

        /// <summary>
        /// A cancellationtoken that represents a user abort.
        /// </summary>
        public CancellationToken AbortToken { get { return abortTokenSource.Token; } }

        /// <summary> </summary>
        public TestPlan()
        {
            _Steps = new TestStepList();
            _Steps.Parent = this;
            ExternalParameters = new ExternalParameters(this);
        }

        // Abort to specify why the test plan run was aborted. In case it is because of an error.
        string abortReason = null;

        /// <summary>
        /// Requests to abort the test plan execution.
        /// </summary>
        public void RequestAbort()
        {
            RequestAbort(null);
        }

        /// <summary> RequestAbort where it's possible to declare the reason. </summary>
        /// <param name="reason">The reason for abort. Can be null.</param>
        internal void RequestAbort(string reason)
        {
            abortReason = reason ?? "Aborted by user";
            Log.Info("Test plan abort requested...");
            abortTokenSource.Cancel();
        }

        /// <summary>
        /// Aborts the TestPlan execution if someone has requested this using <see cref="RequestAbort()"/>
        /// </summary>
        private void abortIfRequested()
        {
            if (abortTokenSource.IsCancellationRequested)
            {
                throw new AbortException(abortReason);
            }
        }

        /// <summary>
        /// Waits the specified number of milliseconds (like <see cref="System.Threading.Thread.Sleep(int)"/>) and also continuously 
        /// checks whether an abort has been requested.
        /// </summary>
        /// <param name="milliSeconds">Number of milliseconds to wait.</param>
        public static void Sleep(int milliSeconds = 0)
        {
            if (milliSeconds == 0)
            {
                var plan = CurrentlyRunningPlan;
                if (plan != null && plan.abortAllowed)
                {
                    plan.abortIfRequested();
                }
            }
            else
            {
                Sleep(TimeSpan.FromMilliseconds(milliSeconds));
            }
        }

        /// <summary>
        /// Like Sleep(int) but with a timespan instead;
        /// </summary>
        /// <param name="timeSpan"></param>
        public static void Sleep(TimeSpan timeSpan)
        {
            {
                var plan = CurrentlyRunningPlan;
                if (plan != null && plan.abortAllowed)
                {
                    plan.abortIfRequested();
                }
            }
            if (timeSpan <= TimeSpan.Zero)
                return;

            TimeSpan min(TimeSpan a, TimeSpan b)
            {
                return a < b ? a : b;
            }
            TimeSpan shortTime = TimeSpan.FromMilliseconds(100);
            TimeSpan longTime = TimeSpan.FromHours(1); 
            var sw = Stopwatch.StartNew();

            while(true)
            {
                var timeleft = timeSpan - sw.Elapsed;
                if (timeleft <= TimeSpan.Zero)
                    break;

                var plan = CurrentlyRunningPlan;
                if (plan != null && plan.abortAllowed)
                {
                    // if plan.abortAllowed is false, the token might be canceled,
                    // but we still cannot abort, so in that case we need to default to Thread.Sleep
                    plan.abortTokenSource.Token.WaitHandle.WaitOne(min(timeleft, longTime));
                    plan.abortIfRequested();
                }
                else
                     Thread.Sleep(min(timeleft, shortTime));
            }
            {
                var plan = CurrentlyRunningPlan;
                if (plan != null && plan.abortAllowed)
                {
                    plan.abortIfRequested();
                }
            }
        }

        /// <summary>
        /// True if the test plan is aborted.
        /// </summary>
        public static bool Aborted { get { if (CurrentlyRunningPlan != null) return CurrentlyRunningPlan.AbortToken.IsCancellationRequested; else return false; } }

        bool abortAllowed = false;

        #region Load/Save TestPlan
        /// <summary>
        /// Saves this TestPlan to a stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> in which to save the TestPlan.</param>
        public void Save(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            var ser = new TapSerializer();
            ser.Serialize(stream, this);
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
            OnPropertyChanged("Path");
            OnPropertyChanged("Name");
        }

        /// <summary>
        /// Load a TestPlan.
        /// </summary>
        /// <param name="filepath">The file path of the TestPlan.</param>
        /// <returns>Returns the new test plan.</returns>
        public static TestPlan Load(string filepath)
        {
            if (filepath == null)
                throw new ArgumentNullException("filepath");
            var timer = Stopwatch.StartNew();
            // Open document
            using (FileStream fs = new FileStream(filepath, FileMode.Open, FileAccess.Read))
            {
                var loadedPlan = Load(fs, filepath);
                Log.Info(timer, "Loaded test plan from {0}", filepath);
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


        /// <summary>
        /// Load a TestPlan.
        /// </summary>
        /// <param name="stream">The stream from which the file is actually loaded.</param>
        /// <param name="path">The path to the file. This will be tha value of <see cref="TestPlan.Path"/> on the new Testplan.</param>
        /// <returns>Returns the new test plan.</returns>
        public static TestPlan Load(Stream stream, string path)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            var serializer = new TapSerializer();
            var plan = (TestPlan) serializer.Deserialize(stream, type: typeof(TestPlan), path: path);
            var errors = serializer.Errors;
            if (errors.Any())
            {
                //var lst = new List<IPlatformRequest>();
                var req = new PlatformRequest<PlanLoadErrorResponse>() { Message = "Parts of the test plan did not load correctly. See the log for more details.\n\nDo you want to ignore these errors and use a corrupt test plan?", Response = PlanLoadErrorResponse.Ignore };
                var resp = PlatformInteraction.WaitForInput(new List<IPlatformRequest> { req }, TimeSpan.MaxValue, PlatformInteraction.RequestType.Custom, Title: "Errors occured while loading test plan.");
                var respValue = (PlanLoadErrorResponse)resp.First().Response;
                if (respValue == PlanLoadErrorResponse.Abort)
                {
                    throw new PlanLoadException(string.Join("\n", errors));
                }
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
            var plan = (TestPlan) serializer.Deserialize(filestream, type: typeof(TestPlan), path: Path);
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
                ValidatingObject.PropertyChangedDispatcher(() => { PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(name)); });
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
        public string Directory {
            get {
                try
                {
                    return string.IsNullOrWhiteSpace(Path) ? null : System.IO.Path.GetDirectoryName(Path);
                }
                catch
                {
                    // probably an invalid path.
                    return null;
                }
            } }
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
