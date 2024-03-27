//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OpenTap
{
    /// <summary>
    /// Common base class for <see cref="TestStepRun"/> and <see cref="TestPlanRun"/>.
    /// </summary>
    [Serializable]
    [DataContract(IsReference = true)]
    [KnownType(typeof(TestPlanRun))]
    [KnownType(typeof(TestStepRun))]
    public abstract class TestRun
    {
        /// <summary>
        /// ID of this test run; can be used to uniquely identify a <see cref="TestStepRun"/> or <see cref="TestPlanRun"/>.  
        /// </summary>
        [DataMember]
        public Guid Id { get; protected set; }

        /// <summary> Creates a new TestRun </summary>
        public TestRun()
        {
            Id = Guid.NewGuid();
            Parameters = new ResultParameters();
        }

        internal const string GROUP = "";
        
        int verdictIndex = -1;
        /// <summary>
        /// <see cref="OpenTap.Verdict"/> resulting from the run.
        /// </summary>
        [DataMember]
        [MetaData(macroName: "Verdict")]
        public Verdict Verdict
        {
            get
            {
                switch ( Parameters.GetIndexed((nameof(Verdict), GROUP), ref verdictIndex))
                {
                    case Verdict verdict:
                        return verdict;
                    case string r:
                        return (Verdict) StringConvertProvider.FromString(r, TypeData.FromType(typeof(Verdict)), null);
                    default:
                        return Verdict.NotSet; // unexpected, but let's not fail.
                }
            }
            protected internal set => Parameters.SetIndexed((nameof(Verdict), GROUP), ref verdictIndex, value);
        }
        
        /// <summary> Exception causing the Verdict to be 'Error'. </summary>
        public Exception Exception { get; internal set; }
        
        /// <summary> Length of time it took to run. </summary>
        public virtual TimeSpan Duration
        {
            get
            {
                var param = Parameters[nameof(Duration), GROUP];   
                if (param is double duration)
                    return Time.FromSeconds(duration);
                return TimeSpan.Zero;
            }
            internal protected set => Parameters[nameof(Duration), GROUP] = value.TotalSeconds;
        }

        /// <summary>
        /// Time when the test started as a <see cref="DateTime"/> object.  
        /// </summary>
        [DataMember]
        [MetaData(macroName: "Date")]
        public DateTime StartTime {
            get
            {
                var param = Parameters[nameof(StartTime), GROUP];
                if (param is DateTime startTime)
                {
                    return startTime;
                }
                return new DateTime();
            }
            set => Parameters[nameof(StartTime), GROUP] = value;    
        }
        /// <summary>
        /// Time when the test started as ticks of the high resolution hardware counter. 
        /// Use with <see cref="Stopwatch.GetTimestamp"/> and <see cref="Stopwatch.Frequency"/> to convert to a timestamp.  
        /// </summary>
        [DataMember]
        public long StartTimeStamp { get; protected set; }

        ResultParameters parameters;
        
        /// <summary>
        /// A list of parameters associated with this run that can be used by <see cref="ResultListener"/>. 
        /// </summary>
        [DataMember]
        public ResultParameters Parameters
        {
            get => parameters;
            protected set
            {
                if (ReferenceEquals(parameters, value)) return;
                parameters = value;
                verdictIndex = -1;
            }
        }
        
        /// <summary>
        /// Upgrades <see cref="Verdict"/>.
        /// </summary>
        /// <param name="verdict"></param>
        internal void UpgradeVerdict(Verdict verdict)
        {
            // locks are slow. 
            // Hence first check if a verdict upgrade is needed, then do the actual upgrade.
            if (Verdict < verdict)
            {
                lock (upgradeVerdictLock)
                {
                    if (Verdict < verdict)
                        Verdict = verdict;
                }
            }
        }
        readonly object upgradeVerdictLock = new object();
        
        /// <summary> Calculated abort condition. </summary>
        internal BreakCondition BreakCondition { get; set; }

        /// <summary> This is invoked when a child run is started. </summary>
        /// <param name="stepRun"></param>
        internal virtual void ChildStarted(TestStepRun stepRun)
        {
            
        }
    }

    /// <summary>
    /// Test step Run parameters.
    /// Contains information about a test step run. Unique for each time a test plan is run.
    /// If the same step is run multiple times during the same TestStep run, multiple instances of this object will be created.
    /// </summary>
    [Serializable]
    [DataContract(IsReference = true)]
    [KnownType(typeof(TestPlanRun))]
    [DebuggerDisplay("TestStepRun {TestStepName}")]
    public class TestStepRun : TestRun
    {
        readonly TestPlanRun testPlanRun;
        /// <summary>
        /// Parent run that is above this run in the <see cref="TestPlan"/> tree.  
        /// </summary>
        [DataMember]
        public Guid Parent { get; private set; }

        /// <summary>
        /// <see cref="TestStep.Id"/> of the <see cref="TestStep"/> that this run represents.  
        /// </summary>
        [DataMember]
        public Guid TestStepId { get; protected set; }

        /// <summary>
        /// <see cref="TestStep.Name"/> of the <see cref="TestStep"/> that this run represents.  
        /// </summary>         
        [DataMember]
        public string TestStepName { get; protected set; }

        /// <summary>
        /// Assembly qualified name of the type of <see cref="TestStep"/> that this run represents.  
        /// </summary>
        [DataMember]
        public string TestStepTypeName { get; protected set; }

        /// <summary>
        /// If possible, the next step executed. This can be implemented to support 'jump-to' functionality.
        /// It requires that the suggested next step ID belongs to one of the sibling steps of TestStepId.
        /// </summary>
        [XmlIgnore]
        public Guid? SuggestedNextStep { get; set; }

        /// <summary>
        /// True if the step currently supports jumping to a step other than the next.
        /// Only true while the step is running or at BreakOffered.
        /// </summary>
        [XmlIgnore]
        public bool SupportsJumpTo { get; set; }

        /// <summary> This step run was skipped without execution. </summary>
        internal bool Skipped { get; set; }

        /// <summary>
        /// The path name of the steps. E.g the name of the step combined with all of its parent steps.
        /// </summary>
        internal string TestStepPath;

        readonly ManualResetEventSlim completedEvent = new ManualResetEventSlim(false);
        
        static readonly CancellationToken NoToken = CancellationToken.None;
        
        /// <summary>  Waits for the test step run to be entirely done. This includes any deferred processing.</summary>
        public void WaitForCompletion()
        {
            WaitForCompletion(NoToken);
        }

        /// <summary>  Waits for the test step run to be entirely done. This includes any deferred processing. It does not break when the test plan is aborted</summary>
        public void WaitForCompletion(CancellationToken cancellationToken)
        {
            if (completedEvent.IsSet) return;

            var currentThread = TapThread.Current;
            if(!WasDeferred && StepThread == currentThread) throw new InvalidOperationException("StepRun.WaitForCompletion called from the thread itself. This will either cause a deadlock or do nothing.");
            if (cancellationToken == NoToken)
            {
                completedEvent.Wait();
                return;
            }
            var waits = new[] { completedEvent.WaitHandle, cancellationToken.WaitHandle };
            while (WaitHandle.WaitAny(waits, 100) == WaitHandle.WaitTimeout)
            {
                if (completedEvent.Wait(0))
                    break;
            }
        }

        /// <summary>  The thread in which the step is running. </summary>
        public TapThread StepThread { get; private set; }

        /// <summary> Set to true if the step execution has been deferred. </summary>
        internal bool WasDeferred => (this.ResultSource as ResultSource)?.WasDeferred ?? false;

        #region Internal Members used by the TestPlan

        /// <summary>  Called by TestStep.DoRun before running the step. </summary>
        internal void StartStepRun()
        {
            if (Verdict != Verdict.NotSet)
                throw new ArgumentOutOfRangeException(nameof(Verdict), "StepRun.StartStepRun has already been called once.");
            StepThread = TapThread.Current;
            StartTime = DateTime.Now;
            StartTimeStamp = Stopwatch.GetTimestamp();
        }

        /// <summary> Called by TestStep.DoRun after running the step. </summary>
        internal void CompleteStepRun(TestPlanRun planRun, ITestStep step, TimeSpan runDuration)
        {
            
            // update values in the run. 
            ResultParameters.UpdateParams(Parameters, step);
            
            Duration = runDuration; // Requires update after TestStepRunStart and before TestStepRunCompleted
            UpgradeVerdict(step.Verdict);
        }

        internal void SignalCompleted()
        {
            StepThread = null;
            completedEvent.Set();
        }

        ITypeData stepTypeData;
        private ITestStep _step;
        
        TestStepRun(ITestStep step)
        {
            _step = step;
            TestStepId = step.Id;
            TestStepName = step.GetFormattedName();
            stepTypeData = TypeData.GetTypeData(step);
            TestStepTypeName = stepTypeData.AsTypeData().AssemblyQualifiedName;
            Parameters = ResultParameters.GetParams(step);
            Verdict = Verdict.NotSet;
        }
        
        /// <summary>
        /// Constructor for TestStepRun.
        /// </summary>
        /// <param name="step">Property Step.</param>
        /// <param name="parent">Property Parent. </param>
        /// <param name="attachedParameters">Parameters that will be stored together with the actual parameters of the steps.</param>
        public TestStepRun(ITestStep step, Guid parent, IEnumerable<ResultParameter> attachedParameters = null): this(step)
        {
            if (attachedParameters != null) Parameters.AddRange(attachedParameters);
            Parent = parent;
        }

        internal TestStepRun(ITestStep step, TestRun parent, IEnumerable<ResultParameter> attachedParameters, TestPlanRun testPlanRun): this(step)
        {
            this.testPlanRun = testPlanRun;
            if (attachedParameters != null) Parameters.AddRange(attachedParameters);
            Parent = parent.Id;
            BreakCondition = calculateBreakCondition(step, parent);
        }
        
        
        static BreakCondition calculateBreakCondition(ITestStep step, TestRun parentStepRun)
        {
            BreakCondition breakCondition = BreakConditionProperty.GetBreakCondition(step);
            
            if (breakCondition.HasFlag(BreakCondition.Inherit))
                return parentStepRun.BreakCondition | BreakCondition.Inherit;
            return breakCondition;
        }

        internal TestStepRun Clone()
        {
            var run = (TestStepRun) this.MemberwiseClone();
            run.Parameters = run.Parameters.Clone();
            return run;
        }
        
        #endregion

        /// <summary> Returns true if the break conditions are satisfied for the test step run.</summary>
        public bool BreakConditionsSatisfied()
        {
            var verdict = Verdict;
            if (OutOfRetries 
                || (verdict == Verdict.Fail && BreakCondition.HasFlag(BreakCondition.BreakOnFail)) 
                || (verdict == Verdict.Error && BreakCondition.HasFlag(BreakCondition.BreakOnError))
                || (verdict == Verdict.Inconclusive && BreakCondition.HasFlag(BreakCondition.BreakOnInconclusive))
                || (verdict == Verdict.Pass && BreakCondition.HasFlag(BreakCondition.BreakOnPass)))
            {
                return true;
            }
            return false;
        }
        
        internal bool OutOfRetries { get; set; }
        internal bool Completed => deferDone.IsSet;

        internal void ThrowDueToBreakConditions()
        {
            throw new TestStepBreakException(_step, this);
        }

        internal bool IsStepChildOf(ITestStep step, ITestStep possibleParent)
        {
            do
            {
                step = step.Parent as ITestStep;
            }
            while (step != null && step != possibleParent);
            return step != null;
        }

        internal void WaitForOutput(OutputAvailability mode, ITestStep waitingFor)
        {
            ITestStep currentStep = TestStepExtensions.currentlyExecutingTestStep;
            var currentThread = TapThread.Current;
            switch (mode)
            {
                case OutputAvailability.BeforeRun: 
                    return;
                case OutputAvailability.AfterDefer:
                    if ((StepThread == currentThread && runDone.Wait(0) == false) ||
                        (currentStep != null && IsStepChildOf(currentStep, waitingFor)))
                        throw new Exception("Deadlock detected");
                    deferDone.Wait(TapThread.Current.AbortToken);
                    break; 
                case OutputAvailability.AfterRun:
                    if ((StepThread == currentThread && runDone.Wait(0) == false) ||
                        (currentStep != null && IsStepChildOf(currentStep, waitingFor)))
                        throw new Exception("Deadlock detected");
                    runDone.Wait(TapThread.Current.AbortToken);
                    break;
            }
        }

        ManualResetEventSlim runDone = new ManualResetEventSlim(false, 0);
        ManualResetEventSlim deferDone = new ManualResetEventSlim(false, 0);
        
        internal void AfterRun(ITestStep step)
        {
            // load member data for results.
            List<IMemberData> resultMembers = null;
            List<IMemberData> primitiveMembers = null;
            foreach (var member in stepTypeData.GetMembers())
            {
                if (member.HasAttribute<ResultAttribute>())
                {
                    var td = member.TypeDescriptor.AsTypeData();
                    
                    if (td != null && (td.IsPrimitive() || td.IsString))
                    {
                        primitiveMembers ??= new List<IMemberData>();
                        primitiveMembers.Add(member);
                    }
                    else
                    {
                        resultMembers ??= new List<IMemberData>();
                        resultMembers.Add(member);
                    }
                }
                
            }
            

            void publishResults()
            {
                // primitive members are collapsed into one row with several columns with the step
                // name as a result name, and each (primitive) property as a column.
                if (primitiveMembers != null)
                {
                    var arrays = primitiveMembers.SelectValues(r =>
                    {
                        var value = r.GetValue(step);

                        // skip null values.
                        if (value == null) 
                            return null; 

                        var array = Array.CreateInstance(value.GetType(), 1);
                        array.SetValue(value, 0);
                        return array;
                    }).ToArray();
                    
                    var names = primitiveMembers.Select(r => r.GetDisplayAttribute().Name).ToList();
                    ((ResultSource)ResultSource).PublishTable(step.StepRun.TestStepName, names, arrays);
                }
                if (resultMembers != null)
                {
                    // handle the non-primitive members normally.
                    foreach (var r in resultMembers)
                    {
                        if (r.TypeDescriptor.IsPrimitive())
                            continue;
                        
                        var value = r.GetValue(step);
                        
                        // skip null values.
                        if (value == null) 
                            continue;
                        
                        var name = r.GetDisplayAttribute().Name;
                        
                        ((ResultSource)ResultSource).Publish(name, value);
                    }
                }
            }

            // ResultSource may be null.
            if (ResultSource != null && (resultMembers != null || primitiveMembers != null))
            {
                if (WasDeferred)
                    ResultSource.Defer(publishResults);
                else
                    publishResults();   
            }

            runDone.Set();
            if (WasDeferred && ResultSource != null)
            {
                ResultSource.Defer(() =>
                {
                    deferDone.Set();
                    stepRuns = ImmutableDictionary<Guid, TestStepRun>.Empty;
                });
            }
            else
            {
                deferDone.Set();
                stepRuns = ImmutableDictionary<Guid, TestStepRun>.Empty;
            }
        }

        internal IResultSource ResultSource;
        /// <summary> Sets the result source for this run. </summary>
        public void SetResultSource(IResultSource resultSource) => this.ResultSource = resultSource;
        bool isCompleted => completedEvent.IsSet;

        List<(Guid, Guid)> waitingFor = new List<(Guid, Guid)>();
        
        /// <summary> Will throw an exception when it times out. </summary>
        internal TestStepRun WaitForChildStepStart(Guid childStep, bool wait, Guid waiterStep)
        {
            if (stepRuns.TryGetValue(childStep, out var run))
                return run;
            if (!wait) return null;
            if (isCompleted) return null;
            
            var sem = new ManualResetEventSlim(false, 0 );

            TestStepRun stepRun = null;
            void onChildStarted(TestStepRun id)
            {
                if (childStep == id.TestStepId)
                {
                    sem.Set();
                    stepRun = id;
                }
            }

            lock (waitingFor)
            {
                childStarted += onChildStarted;
                waitingFor.Add((waiterStep, childStep));
                
                if (Utils2.IsLooped(waitingFor, waiterStep))
                    throw new InvalidOperationException("Input / output loop detected in WaitForChildStepStart");
            }

            try
            {
                while (true)
                {
                    if (stepRuns.TryGetValue(childStep, out run))
                        return run;
                    
                    if (sem.Wait(5000, TapThread.Current.AbortToken))
                        return stepRun;
                }
            }
            finally
            {
                lock (waitingFor)
                {
                    childStarted -= onChildStarted;
                    waitingFor.Remove((waiterStep, childStep));
                }
            }
        }

        Action<TestStepRun> childStarted;
        
        // we keep a mapping of the most recent run of any child step. This is important to be able to update inputs.
        // the guid is the ID of a step.
        ImmutableDictionary<Guid, TestStepRun> stepRuns = ImmutableDictionary<Guid, TestStepRun>.Empty; 
        internal override void ChildStarted(TestStepRun stepRun)
        {
            base.ChildStarted(stepRun);
            Utils.InterlockedSwap(ref stepRuns, () => stepRuns.SetItem(stepRun.TestStepId, stepRun));
            childStarted?.Invoke(stepRun);
        }
        
        /// <summary> Publishes an artifact for the test plan run. </summary>
        /// <param name="stream"> The artifact data as a stream. When publishing an artifact stream, the stream will be disposed by the callee and does not have to be disposed by the caller.</param>
        /// <param name="artifactName"> The name of the published artifact. </param>
        public void PublishArtifact(Stream stream, string artifactName) 
            => testPlanRun.PublishArtifactWithRunAsync(stream, artifactName, this).Wait();
        
        /// <summary> Publishes an artifact for the test plan run. </summary>
        /// <param name="stream"> The artifact data as a stream. When publishing an artifact stream, the stream will be disposed by the callee and does not have to be disposed by the caller.</param>
        /// <param name="artifactName"> The name of the published artifact. </param>
        public Task PublishArtifactAsync(Stream stream, string artifactName) 
            => testPlanRun.PublishArtifactWithRunAsync(stream, artifactName, this);

        
        /// <summary> Publishes an artifact file for the test plan run. </summary>
        public void PublishArtifact(string file) => testPlanRun.PublishArtifactWithRunAsync(file, this).Wait(); 
        
        /// <summary> Publishes an artifact file for the test plan run. </summary>
        public Task PublishArtifactAsync(string file) => testPlanRun.PublishArtifactWithRunAsync(file, this);
    }

    class TestStepBreakException : OperationCanceledException
    {
        public string TestStepName => Step.GetFormattedName();
        public Verdict Verdict => Run.Verdict;
        public ITestStep Step { get; set; }
        public TestStepRun Run { get; set; }

        public TestStepBreakException(ITestStep step, TestStepRun run)
        {
            Step = step;
            Run = run;
        }

        public override string Message =>
            $"Break issued from '{TestStepName}' due to verdict {Verdict}. See Break Conditions settings.";
    }
}
