//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
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

        int verdict_index = -1;
        /// <summary>
        /// <see cref="OpenTap.Verdict"/> resulting from the run.
        /// </summary>
        [DataMember]
        [MetaData(macroName: "Verdict")]
        public Verdict Verdict
        {
            get
            {
                var result = Parameters.GetIndexed(nameof(Verdict), ref verdict_index);
                if (result is Verdict verdict) return verdict;
                return (Verdict) StringConvertProvider.FromString((string)result, TypeData.FromType(typeof(Verdict)), null);
            }
            protected internal set => Parameters.SetIndexed(nameof(Verdict), ref verdict_index, value);
        }

        /// <summary> Length of time it took to run. </summary>
        public virtual TimeSpan Duration
        {
            get
            {
                var param = Parameters[nameof(Duration)];   
                if (param is double duration)
                    return Time.FromSeconds(duration);
                return TimeSpan.Zero;
            }
            internal protected set => Parameters[nameof(Duration)] = value.TotalSeconds;
        }

        /// <summary>
        /// Time when the test started as a <see cref="DateTime"/> object.  
        /// </summary>
        [DataMember]
        [MetaData(macroName: "Date")]
        public DateTime StartTime {
            get => (DateTime)Parameters[nameof(StartTime)];
            set => Parameters[nameof(StartTime)] = value;
        }
        /// <summary>
        /// Time when the test started as ticks of the high resolution hardware counter. 
        /// Use with <see cref="Stopwatch.GetTimestamp"/> and <see cref="Stopwatch.Frequency"/> to convert to a timestamp.  
        /// </summary>
        [DataMember]
        public long StartTimeStamp { get; protected set; }

        /// <summary>
        /// A list of parameters associated with this run that can be used by <see cref="ResultListener"/>. 
        /// </summary>
        [DataMember]
        public ResultParameters Parameters { get; protected set; }

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
        
        
        /// <summary>
        /// Calculated abort condition...
        /// </summary>
        internal BreakCondition BreakCondition { get; set; }

    }

    /// <summary>
    /// Test step Run parameters.
    /// Contains information about a test step run. Unique for each time a test plan is run.
    /// If the same step is run multiple times during the same TestStep run, multiple instances of this object will be created.
    /// </summary>
    [Serializable]
    [DataContract(IsReference = true)]
    [KnownType(typeof(TestPlanRun))]
    public class TestStepRun : TestRun
    {
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

        ManualResetEventSlim completedEvent = null;//new ManualResetEventSlim(false);
        readonly object completedEventLock = new object();
        bool completed = false;
        /// <summary>  Waits for the test step run to be entirely done. This includes any deferred processing.</summary>
        public void WaitForCompletion()
        {
            WaitForCompletion(CancellationToken.None);
        }

        /// <summary>  Waits for the test step run to be entirely done. This includes any deferred processing. It does not break when the test plan is aborted</summary>
        public void WaitForCompletion(CancellationToken cancellationToken)
        {
            if (completed) return;
            lock (completedEventLock)
            {
                if(completedEvent == null)
                    completedEvent = new ManualResetEventSlim(false);
            }

            if (completedEvent.IsSet) return;

            var currentThread = TapThread.Current;
            if(!WasDeferred && StepThread == currentThread) throw new InvalidOperationException("StepRun.WaitForCompletion called from the thread itself. This will either cause a deadlock or do nothing.");
            if (cancellationToken == CancellationToken.None)
            {
                completedEvent.Wait();
                return;
            }
            var waits = new[] { completedEvent.WaitHandle, cancellationToken.WaitHandle };
            while (WaitHandle.WaitAny(waits, 100) == WaitHandle.WaitTimeout)
            {
                if (completed)
                    break;
            }
        }

        /// <summary>  The thread in which the step is running. </summary>
        public TapThread StepThread { get; private set; }

        /// <summary> Set to true if the step execution has been deferred. </summary>
        internal bool WasDeferred { get; set; }

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
            StepThread = null;
            // update values in the run. 
            ResultParameters.UpdateParams(Parameters, step);
            
            Duration = runDuration; // Requires update after TestStepRunStart and before TestStepRunCompleted
            UpgradeVerdict(step.Verdict);
            completed = true;
            completedEvent?.Set();
        }

        /// <summary>
        /// Constructor for TestStepRun.
        /// </summary>
        /// <param name="step">Property Step.</param>
        /// <param name="parent">Property Parent. </param>
        /// <param name="attachedParameters">Parameters that will be stored together with the actual parameters of the steps.</param>
        public TestStepRun(ITestStep step, Guid parent, IEnumerable<ResultParameter> attachedParameters = null)
        {
            TestStepId = step.Id;
            TestStepName = step.GetFormattedName();
            TestStepTypeName = TypeData.FromType(step.GetType()).AssemblyQualifiedName;
            Parameters = ResultParameters.GetParams(step, nameof(Duration), nameof(StartTime));
            Verdict = Verdict.NotSet;
            if (attachedParameters != null) Parameters.AddRange(attachedParameters);
            Parent = parent;
        }
        
        internal TestStepRun(ITestStep step, TestRun parent, IEnumerable<ResultParameter> attachedParameters = null)
        {
            TestStepId = step.Id;
            TestStepName = step.GetFormattedName();
            TestStepTypeName = TypeData.FromType(step.GetType()).AssemblyQualifiedName;
            Parameters = ResultParameters.GetParams(step, nameof(Duration), nameof(StartTime));
            Verdict = Verdict.NotSet;
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

        internal bool IsBreakCondition()
        {
            var verdict = Verdict;
            if (OutOfRetries 
                || (verdict == Verdict.Fail && BreakCondition.HasFlag(BreakCondition.BreakOnFail)) 
                || (verdict == Verdict.Error && BreakCondition.HasFlag(BreakCondition.BreakOnError))
                || (verdict == Verdict.Inconclusive && BreakCondition.HasFlag(BreakCondition.BreakOnInconclusive)))
            {
                return true;
            }

            return false;
        }
        
        internal void CheckBreakCondition()
        {
            if(IsBreakCondition())
                ThrowDueToBreakConditions();
        }
        
        internal bool OutOfRetries { get; set; }

        internal void ThrowDueToBreakConditions()
        {
            throw new TestStepBreakException(TestStepName, Verdict);
        }
    }

    class TestStepBreakException : OperationCanceledException
    {
        public string TestStepName { get; set; }
        public Verdict Verdict { get; set; }

        public TestStepBreakException(string testStepName, Verdict verdict)
        {
            TestStepName = testStepName;
            Verdict = verdict;
        }

        public override string Message =>
            $"Break issued from '{TestStepName}' due to verdict {Verdict}. See Break Conditions settings.";
    }
}
