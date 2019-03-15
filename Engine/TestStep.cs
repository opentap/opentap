//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.CompilerServices;

namespace OpenTap
{
    /// <summary>
    /// An abstract class for TestSteps. 
    /// All TestSteps that are instances of the TestStep abstract class should override the <see cref="TestStep.Run"/> method. 
    /// Additionally, the  <see cref="TestStep.PrePlanRun"/> and <see cref="TestStep.PostPlanRun"/> methods can be overridden.
    /// </summary>
    /// <remarks>
    /// <see cref="ITestStep"/> can also be inherited from instead.
    /// </remarks>
    [ComVisible(true)]
    //[ClassInterface(ClassInterfaceType.AutoDual)]
    [Guid("d0b06600-7bac-47fb-9251-f834e420623f")]
    public abstract class TestStep : ValidatingObject, ITestStep
    {
        #region Properties
        private Verdict _Verdict = Verdict.NotSet;
        /// <summary>
        /// Gets or sets the verdict. Only available during test step run. 
        /// The value of this property will be propagated to the TestStepRun when the step run completes.
        /// </summary>
        [Browsable(false)]
        [ColumnDisplayName(Order : -99, IsReadOnly : true)]
        [XmlIgnore]
        [Output]
        public Verdict Verdict
        {
            get { return _Verdict; }
            set { _Verdict = value; }
        }

        bool enabled = true;
        /// <summary>
        /// Gets or sets boolean indicating whether this step is enabled in the TestPlan
        /// </summary>
        [Browsable(false)]
        [ColumnDisplayName("", Order : -101)]
        public bool Enabled
        {
            get { return enabled; }
            set
            {
                if (enabled == value) return;
                enabled = value;
                OnPropertyChanged(nameof(Enabled));
            }
        }

        /// <summary>
        /// Gets or sets boolean indicating whether this step is readonly in the TestPlan
        /// </summary>
        [Browsable(false)]
        [XmlIgnore]
        public bool IsReadOnly { get; set; }

        private string name;
        ///  <summary>
        ///  Name of the step intended to be read and set by the user.
        ///  Different across instances of the same type.
        /// </summary>
        [ColumnDisplayName("Step Name", Order : -100)]
        [Browsable(false)]
        public string Name
        {
            get { return name; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value", "TestStep.Name cannot be null.");
                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        /// <summary>
        /// This TestStep type as a <see cref="string"/>.   
        /// </summary>
        [ColumnDisplayName("Step Type", Order : 1)]
        [Browsable(false)]
        public string TypeName
        {
            get { return GetType().GetDisplayAttribute().GetFullName(); }
        }

        private TestStepList _ChildTestSteps;
        /// <summary>
        /// Gets or sets a List of child <see cref="TestStep"/>s. Any TestSteps in this list will be
        /// executed instead of the Run method of this TestStep.
        /// </summary>
        [Browsable(false)]
        public TestStepList ChildTestSteps
        {
            get { return _ChildTestSteps; }
            set
            {
                _ChildTestSteps = value;
                _ChildTestSteps.Parent = this;
                OnPropertyChanged("ChildTestSteps");
            }
        }
        
        /// <summary>
        /// The parent of this TestStep. Can be another TestStep or the <see cref="TestPlan"/>.  
        /// </summary>
        [XmlIgnore]
        public virtual ITestStepParent Parent { get; set; }

        /// <summary>
        /// Result proxy that stores TestStep run results until they are propagated to the <see cref="ResultListener"/>.   
        /// </summary>
        [XmlIgnore]
        public ResultSource Results { get; internal set; }

        /// <summary>
        /// The enumeration of all enabled Child Steps.
        /// </summary>
        public IEnumerable<ITestStep> EnabledChildSteps
        {
            get { return this.GetEnabledChildSteps(); }
        }
        
        /// <summary>
        /// Version of this test step.
        /// </summary>
        [XmlAttribute("Version")]
        [Browsable(false)]
        public string Version
        {
            get { return this.GetType().Assembly?.GetSemanticVersion().ToString(); }
            set
            {
                var installedVersion = this.GetType().Assembly?.GetSemanticVersion();
                if (installedVersion == null)
                {
                    Log.Warning("Could not get assembly version");
                    return;
                }
                if(SemanticVersion.TryParse(value, out SemanticVersion createdVersion))
                {
                    if (createdVersion == null)
                    {
                        Log.Warning("Could not get created version");
                        return;
                    }
                    if (!createdVersion.IsCompatible(installedVersion))
                    {
                        Log.Warning("Test plan file specified version {0} of step '{1}', but version {2} is installed, compatibility issues may occur.", createdVersion, Name, installedVersion);
                    }
                }
                else
                {
                    Log.Warning("Could not parse test plan file specified version {0} of step '{1}' as a semantic version, but version {2} is installed, compatibility issues may occur.", value, Name, installedVersion);
                }
            }
        }

        #endregion

        /// <summary>
        /// Recursively collects a completely list of child steps using the specified pattern. Order is depth-first.
        /// </summary>
        /// <param name="searchKind">Pattern.</param>
        /// <returns>Unevaluated IEnumerable of test steps.</returns>
        public IEnumerable<ITestStep> RecursivelyGetChildSteps(TestStepSearch searchKind)
        {
            return (this as ITestStep).RecursivelyGetChildSteps(searchKind);
        }

        /// <summary>
        /// Gets children following a specific search patterns. Not recursive.
        /// </summary>
        /// <param name="searchKind">Search pattern to use.</param>
        /// <returns>Unevaluated IEnumerable.</returns>
        public IEnumerable<ITestStep> GetChildSteps(TestStepSearch searchKind)
        {
            return (this as ITestStep).GetChildSteps(searchKind);
        }

        /// <summary>
        /// Log used to Log trace messages from TestSteps. These messages will be written with
        /// "TestStep" as the source.
        /// </summary>
        protected static readonly TraceSource Log =  OpenTap.Log.CreateSource("TestStep");

        /// <summary>
        /// Returns a default name for a step type.
        /// </summary>
        /// <param name="stepType"></param>
        /// <returns></returns>
        public static string[] GenerateDefaultNames(Type stepType)
        {
            if (stepType == null)
                throw new ArgumentNullException("stepType");
            var disp = stepType.GetDisplayAttribute();
            return disp.Group.Append(disp.Name).ToArray();
        }
        static ConditionalWeakTable<Type, string> defaultNames = new ConditionalWeakTable<Type, string>();

        /// <summary>
        /// Initializes a new instance of the TestStep base class.
        /// </summary>
        public TestStep()
        {
            name = defaultNames.GetValue(GetType(), type => GenerateDefaultNames(type).Last());
            
            Enabled = true;
            IsReadOnly = false;
            ChildTestSteps = new TestStepList();

            var things = loaderLookup.GetValue(GetType(), loadDefaultResources);
            foreach (var loader in things)
                loader(this);
            Results = null; // this will be set by DoRun just before calling Run()
        }

        static readonly ConditionalWeakTable<Type, Action<object>[]> loaderLookup = new ConditionalWeakTable<Type, Action<object>[]>();

        static Action<object>[] loadDefaultResources(Type t)
        {
            List<Action<object>> loaders = new List<Action<object>>();
            var props = t.GetPropertiesTap();
            foreach (var prop in props.Where(p => p.GetSetMethod() != null))
            {
                Type propType = prop.PropertyType;
                if (propType.DescendsTo(typeof(IList)) && propType.IsGenericType)
                {
                    Type genericType = propType.GetGenericArguments().FirstOrDefault();
                    if (ComponentSettingsList.HasContainer(genericType))
                    {
                        loaders.Add((x) =>
                        {
                            IList values = ComponentSettingsList.GetContainer(genericType);
                            if (values != null)
                            {
                                var interestingValues = values.Cast<object>()
                                    .Where(o => o != null && o.GetType().DescendsTo(genericType)).ToList();
                                if (interestingValues.Count > 0)
                                {
                                    IList vlist = Activator.CreateInstance(propType) as IList;
                                    vlist.Insert(0, interestingValues.First());
                                    object value = vlist;
                                    try
                                    {
                                        prop.SetValue(x, value, null);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Warning("Caught exception while setting default value on {0}.", prop);
                                        Log.Debug(ex);
                                    }
                                }
                            }
                        });
                        
                    }
                }
                else if(ComponentSettingsList.HasContainer(prop.PropertyType))
                {

                    loaders.Add(x =>
                    {
                        IList list = ComponentSettingsList.GetContainer(prop.PropertyType);
                        if (list != null)
                        {
                            object value = list.Cast<object>()
                               .Where(o => o != null && o.GetType().DescendsTo(propType))
                               .FirstOrDefault();

                            try
                            {
                                prop.SetValue(x, value, null);
                            }
                            catch (Exception ex)
                            {
                                Log.Warning("Caught exception while setting default value on {0}.", prop);
                                Log.Debug(ex);
                            }
                        }
                    });
                }

                if (prop.PropertyType.DescendsTo(typeof(IValidatingObject)))
                {
                    // load forwarded validation rules.
                    loaders.Add(x =>
                    {
                        var step = (IValidatingObject)x;
                        step.Rules.Forward(step, prop.Name);
                    });
                }
            }
            if (loaders.Count == 0) return Array.Empty<Action<object>>();
            return loaders.ToArray();
        }

        static Dictionary<Type, bool> prePostPlanRunUsedLookup = new Dictionary<Type, bool>();

        static bool preOrPostPlanRunOverridden(Type t)
        {
            bool isMethodOverridden(string methodName)
            {
                var m1 = typeof(TestStep).GetMethod(methodName).MethodHandle.Value;
                var m2 = t.GetMethod(methodName).MethodHandle.Value;
                return m1 != m2;
            }
            lock (prePostPlanRunUsedLookup)
            {
                if (prePostPlanRunUsedLookup.ContainsKey(t) == false)
                {
                    prePostPlanRunUsedLookup[t] = isMethodOverridden(nameof(PrePlanRun)) || isMethodOverridden(nameof(PostPlanRun));
                }
                return prePostPlanRunUsedLookup[t];
            }
        }

        bool? prePostPlanRunUsed;

        /// <summary> True if Pre- or PostPlanRUn has been overridden. </summary>
        internal bool PrePostPlanRunUsed
        {
            get
            {
                if (prePostPlanRunUsed.HasValue == false)
                {
                    prePostPlanRunUsed = preOrPostPlanRunOverridden(GetType());
                }
                return prePostPlanRunUsed.Value;
            }
        }



        /// <summary>
        /// Sets the Verdict if it is not already set to a more serious verdict (for example, a Pass verdict would be upgraded to Fail, which is more serious).  
        /// </summary>
        /// <param name="verdict">New verdict to set.</param>
        protected void UpgradeVerdict(Verdict verdict)
        {
            if ((int)verdict > (int)this.Verdict)
            {
                this.Verdict = verdict;
            }
        }

        /// <summary>
        /// Searches up through the Parent steps and returns the first step of the requested type that it finds.  
        /// </summary>
        /// <typeparam name="T">The type of TestStep to get.</typeparam>
        /// <returns>The closest TestStep of the requested type in the hierarchy.</returns>
        protected T GetParent<T>() where T : ITestStepParent
        {
            return (this as ITestStep).GetParent<T>();
        }

        /// <summary>
        /// Called by TestPlan.Run() for each step in the test plan prior to calling the <see cref="TestStep.Run"/> method of each step.
        /// </summary>
        public virtual void PrePlanRun()
        {

        }

        /// <summary>
        /// Called by TestPlan.Run() to run each TestStep. 
        /// If this step has children (ChildTestSteps.Count > 0), then these are executed instead.
        /// </summary>
        public abstract void Run();

        /// <summary>
        /// Called by TestPlan.Run() after completing all <see cref="TestStep.Run"/> methods in the <see cref="TestPlan"/>. 
        /// /// </summary>
        /// <remarks>
        /// Note that <see cref="TestStep.PostPlanRun"/>run is run in reverse order. 
        /// For example, suppose you had three tests: T1, T2, and T3. 
        /// PrePlanRun would run for T1, T2 and T3 (in that order), and PostPlanRun would run for T3, T2 and T1 (in that order).
        /// </remarks>

        public virtual void PostPlanRun()
        {

        }

        /// <summary> 
        /// Raises the <see cref="TestPlan.BreakOffered"/> event on the <see cref="TestPlan"/> object to which this TestStep belongs. 
        /// </summary>
        /// <remarks> This method allows a user interface implementation to break/pause the execution of the TestPlan at the point at which it is called.</remarks>
        public void OfferBreak(bool isTestStepStarting = false)
        {
            (this as ITestStep).OfferBreak(StepRun, isTestStepStarting);
        }

        /// <summary>
        /// Runs all enabled <see cref="TestStep.ChildTestSteps"/> of this TestStep. Upgrades parent verdict to the resulting verdict of the childrens run. Throws an exception if the child step does not belong or isn't enabled.
        /// </summary>
        /// <param name="attachedParameters">Parameters that will be stored together with the actual parameters of the steps.</param>
        protected IEnumerable<TestStepRun> RunChildSteps(IEnumerable<ResultParameter> attachedParameters = null)
        {
            return RunChildSteps(attachedParameters, CancellationToken.None);
        }

        /// <summary>
        /// Runs all enabled <see cref="TestStep.ChildTestSteps"/> of this TestStep. Upgrades parent verdict to the resulting verdict of the childrens run. Throws an exception if the child step does not belong or isn't enabled.
        /// </summary>
        /// <param name="attachedParameters">Parameters that will be stored together with the actual parameters of the steps.</param>
        /// <param name="cancellationToken">Provides a way to cancel the execution of child steps before all steps are executed.</param>
        protected IEnumerable<TestStepRun> RunChildSteps(IEnumerable<ResultParameter> attachedParameters, CancellationToken cancellationToken)
        {
            return this.RunChildSteps(PlanRun, StepRun, attachedParameters, cancellationToken);
        }

        /// <summary>
        /// Runs the specified child step if enabled. Upgrades parent verdict to the resulting verdict of the child run. Throws an exception if childStep does not belong or isn't enabled.
        /// </summary>
        /// <param name="childStep">The child step to run.</param>
        /// <param name="attachedParameters">Parameters that will be stored together with the actual parameters of the step.</param>
        protected TestStepRun RunChildStep(ITestStep childStep, IEnumerable<ResultParameter> attachedParameters = null)
        {
            var steprun = this.RunChildStep(childStep, PlanRun, StepRun, attachedParameters);
            Results.Defer(() => steprun.WaitForCompletion());
            return steprun;
        }

        /// <summary>
        /// Gets or sets the <see cref="TestPlanRun"/>.  
        /// </summary>
        [Browsable(false)]
        [XmlIgnore]
        public TestPlanRun PlanRun { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="TestStepRun"/>. 
        /// </summary>
        [Browsable(false)]
        [XmlIgnore]
        public TestStepRun StepRun { get; set; }

        #region ID

        private Guid _Id = Guid.NewGuid();

        /// <summary>
        /// Unique ID used to store references to test steps. 
        /// </summary>
        [XmlAttribute("Id")]
        [Browsable(false)]
        public Guid Id
        {
            get { return _Id; }
            set { _Id = value; }
        }

        #endregion
    }

    /// <summary>
    /// An extension class for the ITestStep interface.
    /// </summary>
    public static class TestStepExtensions
    {
        /// <summary>
        /// Searches up through the Parent steps and returns the first step of the requested type that it finds.  
        /// </summary>
        /// <typeparam name="T">The type of TestStep to get.</typeparam>
        /// <returns>The closest TestStep of the requested type in the hierarchy.</returns>
        public static T GetParent<T>(this ITestStep Step) where T : ITestStepParent
        {
            ITestStepParent parent = Step.Parent;
            while (parent != null)
            {
                if (parent is T)
                {
                    return (T)parent;
                }
                parent = parent.Parent;
            }
            return default(T);
        }
        /// <summary> 
        /// Raises the <see cref="TestPlan.BreakOffered"/> event on the <see cref="TestPlan"/> object to which this TestStep belongs. 
        /// </summary>
        /// <remarks> This method allows a user interface implementation to break/pause the execution of the TestPlan at the point at which it is called.</remarks>
        [Obsolete("Use OfferBreak(TestStepRun stepRun, bool isTestStepStarting = false) instead.")]
        public static void OfferBreak(this ITestStep Step, bool isTestStepStarting = false)
        {
            OfferBreak(Step, Step.StepRun, isTestStepStarting);
        }
        /// <summary> 
        /// Raises the <see cref="TestPlan.BreakOffered"/> event on the <see cref="TestPlan"/> object to which this TestStep belongs. 
        /// </summary>
        /// <remarks> This method allows a user interface implementation to break/pause the execution of the TestPlan at the point at which it is called.</remarks>
        public static void OfferBreak(this ITestStep Step, TestStepRun stepRun, bool isTestStepStarting = false)
        {
            var plan = Step.GetParent<TestPlan>();

            if (plan != null)
            {
                BreakOfferedEventArgs args = new BreakOfferedEventArgs(stepRun, isTestStepStarting);
                plan.OnBreakOffered(args);
                stepRun.SuggestedNextStep = args.JumpToStep;
            }
        }
        /// <summary>
        /// Gets all the enabled Child Steps.
        /// </summary>
        public static IEnumerable<ITestStep> GetEnabledChildSteps(this ITestStep Step)
        {
            return Step.GetChildSteps(TestStepSearch.EnabledOnly);
        }
        /// <summary>
        /// Gets children following a specific search patterns. Not recursive.
        /// </summary>
        /// <param name="Step"></param>
        /// <param name="searchKind">Search pattern to use.</param>
        /// <returns>Unevaluated IEnumerable.</returns>
        public static IEnumerable<ITestStep> GetChildSteps(this ITestStep Step, TestStepSearch searchKind)
        {
            return Step.ChildTestSteps
                .Where(step => searchKind == TestStepSearch.All || ((searchKind == TestStepSearch.EnabledOnly) == step.Enabled));
        }
        /// <summary>
        /// Recursively collects a complete list of child steps using the specified pattern. Order is depth-first.  
        /// </summary>
        /// <param name="Step"></param>
        /// <param name="searchKind">Pattern.</param>
        /// <returns>Unevaluated IEnumerable of test steps.</returns>
        public static IEnumerable<ITestStep> RecursivelyGetChildSteps(this ITestStep Step, TestStepSearch searchKind)
        {
            var childSteps = Step.GetChildSteps(searchKind).ToList();
            return childSteps
                .Concat(childSteps
                    .SelectMany(step => step.RecursivelyGetChildSteps(searchKind)));
        }

        /// <summary>
        /// Runs all enabled <see cref="TestStep.ChildTestSteps"/> of this TestStep. Upgrades parent verdict to the resulting verdict of the childrens run. Throws an exception if the child step does not belong or isn't enabled.
        /// </summary>
        /// <param name="Step"></param>
        /// <param name="currentPlanRun">The current TestPlanRun.</param>
        /// <param name="currentStepRun">The current TestStepRun.</param>
        /// <param name="attachedParameters">Parameters that will be stored together with the actual parameters of the steps.</param>
        public static IEnumerable<TestStepRun> RunChildSteps(this ITestStep Step, TestPlanRun currentPlanRun, TestStepRun currentStepRun, IEnumerable<ResultParameter> attachedParameters = null)
        {
            return RunChildSteps(Step, currentPlanRun, currentStepRun, attachedParameters, CancellationToken.None);
        }

        /// <summary>
        /// Runs all enabled <see cref="TestStep.ChildTestSteps"/> of this TestStep. Upgrades parent verdict to the resulting verdict of the childrens run. Throws an exception if the child step does not belong or isn't enabled.
        /// </summary>
        /// <param name="Step"></param>
        /// <param name="currentPlanRun">The current TestPlanRun.</param>
        /// <param name="currentStepRun">The current TestStepRun.</param>
        /// <param name="attachedParameters">Parameters that will be stored together with the actual parameters of the steps.</param>
        /// <param name="cancellationToken">Provides a way to cancel the execution of child steps before all steps are executed.</param>
        public static IEnumerable<TestStepRun> RunChildSteps(this ITestStep Step, TestPlanRun currentPlanRun, TestStepRun currentStepRun, IEnumerable<ResultParameter> attachedParameters, CancellationToken cancellationToken)
        {
            if (currentPlanRun == null)
                throw new ArgumentNullException("currentPlanRun");
            if (currentStepRun == null)
                throw new ArgumentNullException("currentStepRun");
            if (Step == null)
                throw new ArgumentNullException("Step");
            if (Step.StepRun == null)
                throw new Exception("Cannot run child steps outside the Run method.");

            Step.StepRun.SupportsJumpTo = true;

            var steps = Step.ChildTestSteps;
            List<TestStepRun> runs = new List<TestStepRun>(steps.Count);
            try
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    var step = steps[i];
                    if (step.Enabled == false) continue;

                    var run = step.DoRun(currentPlanRun, currentStepRun, attachedParameters);
                    
                    if (cancellationToken.IsCancellationRequested) break;

                    // note: The following is copied inside TestPlanExecution.cs
                    if (run.SuggestedNextStep != null)
                    {
                        var stepidx = steps.IndexWhen(x => x.Id == run.SuggestedNextStep);
                        if (stepidx != -1)
                            i = stepidx - 1;
                        // if skip to next step, dont add it to the wait queue.
                    }
                    else
                    {
                        runs.Add(run);
                    }
                    TapThread.ThrowIfAborted();
                }
            }
            finally
            {

                if (runs.Count > 0) // Avoid deferring if there is nothing to do.
                {
                    if (Step is TestStep testStep)
                    {
                        testStep.Results.Defer(() =>
                        {
                            foreach (var run in runs)
                            {
                                run.WaitForCompletion();

                                if (run.Verdict > Step.Verdict)
                                    Step.Verdict = run.Verdict;
                            }
                        });
                    }
                    else
                    {
                        foreach (var run in runs)
                        {
                            run.WaitForCompletion();

                            if (run.Verdict > Step.Verdict)
                                Step.Verdict = run.Verdict;
                        }
                    }
                }
            }

            return runs;
        }
        /// <summary>
        /// Runs the specified child step if enabled. Upgrades parent verdict to the resulting verdict of the child run. Throws an exception if childStep does not belong or isn't enabled.
        /// </summary>
        /// <param name="Step"></param>
        /// <param name="childStep">The child step to run.</param>
        /// <param name="currentPlanRun">The current TestPlanRun.</param>
        /// <param name="currentStepRun">The current TestStepRun.</param>
        /// <param name="attachedParameters">Parameters that will be stored together with the actual parameters of the step.</param>
        public static TestStepRun RunChildStep(this ITestStep Step, ITestStep childStep, TestPlanRun currentPlanRun, TestStepRun currentStepRun, IEnumerable<ResultParameter> attachedParameters = null)
        {
            if (childStep == null)
                throw new ArgumentNullException("childStep");
            if (currentPlanRun == null)
                throw new ArgumentNullException("currentPlanRun");
            if (currentStepRun == null)
                throw new ArgumentNullException("currentStepRun");
            if (Step.StepRun == null)
                throw new Exception("Can only run child step during own step run.");
            if(childStep.Parent != Step)
                throw new ArgumentException("childStep must be a child step of Step", "childStep");
            if(childStep.Enabled == false)
                throw new ArgumentException("childStep must be enabled.", "childStep");

            var run = childStep.DoRun(currentPlanRun, currentStepRun, attachedParameters);

            if (Step is TestStep step)
            {
                step.Results.Defer(() =>
                {
                    run.WaitForCompletion();
                    if (run.Verdict > Step.Verdict)
                        Step.Verdict = run.Verdict;
                });
            }
            else
            {
                run.WaitForCompletion();
                if (run.Verdict > Step.Verdict)
                    Step.Verdict = run.Verdict;
            }

            return run;
        }

        internal static string GetStepPath(this ITestStep Step)
        {
            List<string> names = new List<string>();
            ITestStep s = Step;
            while (s != null)
            {
                bool containsMacro = s.Name.Contains('{') && s.Name.Contains('}');
                if (containsMacro)
                    names.Add(s.GetFormattedName());
                else
                    names.Add(s.Name);
                s = s.Parent as ITestStep;
            }
            names.Reverse();
            return '\"'+ string.Join(" \\ ", names) + '\"';
        }

        static void checkStepFailure(ITestStep Step, TestPlanRun planRun)
        {
            if ((Step.Verdict == Verdict.Fail && planRun.AbortOnStepFail) || (Step.Verdict == Verdict.Error && planRun.AbortOnStepError))
            {
                TestPlan.Log.Warning("OpenTAP is currently configured to abort run on verdict {0}. This can be changed in Engine Settings.", Step.Verdict);
                planRun.MainThread.Abort(String.Format("Verdict of '{0}' was '{1}'.", Step.Name, Step.Verdict));
            }
            else  if (Step.Verdict == Verdict.Aborted)
            {
                throw new OperationCanceledException(String.Format("Verdict of '{0}' was 'Abort'.", Step.Name), TapThread.Current.AbortToken);
            }
            else
            {
                return;
            }
        }
        
        internal static TestStepRun DoRun(this ITestStep Step, TestPlanRun planRun, TestStepRun parentStepRun, IEnumerable<ResultParameter> attachedParameters = null)
        {
            {
                // in case the previous action was not completed yet.
                // this is a problem because StepRun might be set to null later
                // if its not already the case.
                var prevRun = Step.StepRun;
                if (prevRun != null)
                    prevRun.WaitForCompletion();
                Debug.Assert(Step.StepRun == null);
            }

            Step.Verdict = Verdict.NotSet;

            TapThread.ThrowIfAborted();
            if (!Step.Enabled)
                throw new Exception("Step not enabled."); // Do not run step if it has been disabled
            planRun.ThrottleResultPropagation();
            Step.PlanRun = planRun;
            var stepRun = Step.StepRun = new TestStepRun(Step, parentStepRun == null ? planRun.Id : parentStepRun.Id, attachedParameters);
            if (parentStepRun == null)
                stepRun.TestStepPath = stepRun.TestStepName;
            else
                stepRun.TestStepPath = parentStepRun.TestStepPath + " \\ " + stepRun.TestStepName;

            var stepPath = stepRun.TestStepPath;
            //Raise an event prior to starting the actual run of the TestStep. 
            Step.OfferBreak(stepRun, true);
            if (stepRun.SuggestedNextStep != null) {
                Step.StepRun = null;
                return stepRun;
            }

            // Signal step is going to execute
            planRun.ExecutionHooks.ForEach(eh => eh.BeforeTestStepExecute(Step));
            planRun.ResourceManager.BeginStep(planRun, Step, TestPlanExecutionStage.Run, TapThread.Current.AbortToken);

            TestPlan.Log.Info(stepPath + " started.");

            // To properly support single stepping stopwatch has to be below offerBreak
            // since OfferBreak requires a TestStepRun, this has to be re-instantiated.
            var swatch = Stopwatch.StartNew();

            TapThread.ThrowIfAborted(); // if an OfferBreak handler called TestPlan.Abort, abort now.
            stepRun.StartStepRun(); // set verdict to running, set Timestamp.

            IResultSource resultSource = null;
            try
            {
                planRun.AddTestStepRunStart(stepRun);

                if (Step is TestStep)
                    resultSource = (Step as TestStep).Results = new ResultSource(stepRun, planRun);

                Step.Run();
                TapThread.ThrowIfAborted();
                //checkStepFailure(Step, planRun);
            }
            catch (ThreadAbortException)
            {
                if(Step.Verdict < Verdict.Aborted)
                    Step.Verdict = Verdict.Aborted;
                throw;
            }
            catch(OperationCanceledException)
            {
                if(Step.Verdict < Verdict.Aborted)
                    Step.Verdict = Verdict.Aborted;
                throw;
            }
            catch (Exception e)
            {
                while (e is AggregateException a && a.InnerExceptions.Count == 1)
                {
                        e = a.InnerException;
                }
                Step.Verdict = Verdict.Error; //use UpgradeVerdict.
                TestPlan.Log.Error("{0} failed, moving on. The error was '{1}'.", stepPath, e.Message);
                TestPlan.Log.Debug(e);
            }
            finally
            {
                planRun.AddTestStepStateUpdate(stepRun.TestStepId, stepRun, StepState.Deferred);
                planRun.ResourceManager.EndStep(Step, TestPlanExecutionStage.Run);
                planRun.ExecutionHooks.ForEach(eh => eh.AfterTestStepExecute(Step));
                
                void completeAction(Task runTask)
                {
                    try
                    {
                        runTask.Wait();
                    }
                    catch (Exception e)
                    {
                        
                        while (e is AggregateException a && a.InnerExceptions.Count == 1)
                        {
                            e = a.InnerException;
                        }

                        if (e is ThreadAbortException)
                        {
                            if(Step.Verdict < Verdict.Aborted)
                                Step.Verdict = Verdict.Aborted;
                            throw;
                        }

                        if (e is OperationCanceledException e2)
                        {
                            TestPlan.Log.Info("Stopping TestPlan. {0}", e2.Message);
                            if (Step.Verdict < Verdict.Aborted)
                                Step.Verdict = Verdict.Aborted;
                            throw;
                        }
                        else if (e is AggregateException a)
                        {
                            if (a.InnerExceptions.Count == 1)
                                e = a.InnerException;
                        }
                        Step.Verdict = Verdict.Error; //use UpgradeVerdict.
                        TestPlan.Log.Error("{0} failed, moving on. The error was '{1}'.", stepPath, e.Message);
                        TestPlan.Log.Debug(e);
                    }
                    finally
                    {
                        lock (Step)
                        {
                            if (Step.StepRun == stepRun)
                            {
                                Step.StepRun = null;
                                Step.PlanRun = null;
                            }
                        }
                        TimeSpan time = swatch.Elapsed;
                        stepRun.CompleteStepRun(planRun, Step, time);
                        if (Step.Verdict == Verdict.NotSet)
                        {
                            TestPlan.Log.Info(time, stepPath + " completed.");
                        }
                        else
                        {
                            TestPlan.Log.Info(time, "{0} completed with verdict '{1}'.", stepPath, Step.Verdict);
                        }

                        planRun.AddTestStepRunCompleted(stepRun);

                        checkStepFailure(Step, planRun);
                    }
                }

                if (resultSource != null)
                    resultSource.Finally(completeAction);
                else
                    completeAction(Task.FromResult(0));
            }
            return stepRun;
        }
        internal static void CheckResources(this ITestStep Step)
        {
            var resProps2 = GetStepSettings<IResource>(new[] { Step }, true);
            foreach(var res in resProps2)
            {
                if(res == null)
                    throw new Exception(String.Format("Resource setting not set on step {0}. Please configure or disable step.", Step.Name));
            }
            foreach(var step in Step.ChildTestSteps)
            {
                if(step.Enabled)
                    step.CheckResources();
            }
        }

        /// <summary>
        /// Returns the properties of a specific type from a number of objects. This will traverse IEnumerable and optionally IEnabled properties.
        /// </summary>
        /// <param name="objects">The objects to return properties from.</param>
        /// <param name="onlyEnabled">If true, Enabled and EnabledIf properties are only traversed into if they are enabled.</param>
        /// <param name="transform">This transform function is called on each object, and being passed the corresponding PropertyInfo instance from the parent object.</param>
        /// <returns></returns>
        internal static List<T3> GetObjectSettings<T,T2,T3>(IEnumerable<T2> objects, bool onlyEnabled, Func<T, PropertyInfo, T3> transform)
        {
            var stepTypes = objects.Where(x => x != null).ToLookup(x => x.GetType());
            HashSet<T3> items = new HashSet<T3>();

            foreach (var typeGroup in stepTypes)
            {
                var propertyInfos = typeGroup.Key.GetMemberData();
                
                foreach (var _prop in propertyInfos)
                {
                    var prop = _prop.Property;
                    // 3 cases:
                    if ((prop.PropertyType.HasInterface<IEnumerable>() && prop.PropertyType != typeof(string)) // 1: A list of things, one might be T.
                        || prop.PropertyType.HasInterface<IEnabled>() // 2: The thing might an IEnabled<T or supertype of T>.
                        || typeof(T).IsAssignableFrom(prop.PropertyType)) // 3: or the standard case prop.PropertyType is a 'T' or descendant.
                    {
                        var attrs = _prop.GetCustomAttributes<EnabledIfAttribute>();
                        foreach (var Step in typeGroup)
                        {
                            foreach (EnabledIfAttribute attr in attrs)
                            {
                                if (onlyEnabled)
                                {
                                    bool isenabled = EnabledIfAttribute.IsEnabled(prop, attr, Step);
                                    if (isenabled == false)
                                        goto nextstep;
                                }
                            }

                            object propertyValue = null;
                            try
                            {
                                propertyValue = prop.GetValue(Step, null);
                            }
                            catch
                            {
                                continue;
                            }
                            if (propertyValue is IEnabled)
                            {
                                IEnabled enabled = (IEnabled)propertyValue;
                                if (enabled.IsEnabled)
                                {
                                    var value = GetObjectSettings<T,object,T3>(new[] { propertyValue }, onlyEnabled, transform);
                                    foreach (var item in value)
                                    {
                                        if (!items.Contains(item))
                                            items.Add(item);
                                    }
                                    goto nextstep;
                                }
                            }
                            if (propertyValue is string == false && propertyValue is IEnumerable lst)
                            {
                                foreach (var item in lst.OfType<T>())
                                {
                                    var item2 = transform(item, prop);
                                    if (!items.Contains(item2))
                                        items.Add(item2);
                                }
                                goto nextstep;
                            }
                            if ((propertyValue is T) || ((propertyValue == null) && typeof(T).IsAssignableFrom(prop.PropertyType)))
                            {
                                var item2 = transform((T)propertyValue, prop);
                                if (!items.Contains(item2))
                                    items.Add(item2);
                            }
                            nextstep:;
                        }
                    }
                }
            }
            return items.ToList();
        }

        internal static List<T> GetStepSettings<T>(IEnumerable<ITestStep> steps, bool onlyEnabled)
        {
            return GetObjectSettings<T, ITestStep, T>(steps, onlyEnabled, (o, pi) => o);
        }
        
        struct Replace
        {
            public int StartIndex;
            public int EndIndex;
            public string Content;
        }
        
        static Dictionary<Type, Dictionary<string, PropertyInfo>> formatterLutCache 
            = new Dictionary<Type, Dictionary<string, PropertyInfo>>();
        
        /// <summary>
        /// Takes the name of step and replaces properties.
        /// </summary>
        /// <param name="step"></param>
        /// <returns></returns>
        public static string GetFormattedName(this ITestStep step)
        {
            if(step.Name.Contains('{') == false)
                return step.Name;
            Dictionary<string, PropertyInfo> props = null;
            if (formatterLutCache.ContainsKey(step.GetType()) == false)
            {
                // GetProperties potentially slow. GetFormattedName is in test plan exec thread, so the outcome is cached.
                
                props = new Dictionary<string, PropertyInfo>();
                foreach (var item in step.GetType().GetPropertiesTap())
                {
                    var browsable = item.GetAttribute<BrowsableAttribute>();
                    if (browsable != null && browsable.Browsable == false) continue;
                    if (!item.CanRead || item.GetGetMethod() == null)
                        continue;
                    var s = item.GetDisplayAttribute();
                    if ((s.Group != null) && (s.Group.Length > 0))
                    {
                        // GroupName [space] DisplayName
                        var fullFormat = string.Format("{0} {1}", string.Join(" ", s.Group.Select(g => g.Trim())), s.Name.Trim());
                        props[fullFormat.ToLower()] = item;
                        // just DisplayName
                        var shortFormat = s.Name.Trim().ToLower();
                        if (!props.ContainsKey(shortFormat))
                            props[shortFormat.ToLower()] = item;
                    }
                    else
                    {
                        props[s.Name.ToLower()] = item;
                    }
                }
                formatterLutCache[step.GetType()] = props;
            }
            else
            {
                props = formatterLutCache[step.GetType()];
            }
            var name = step.Name;
            if (name == null)
                return ""; // Since we are returning the formatted name we should not return null.
            int offset = 0;
            int seek = 0;

            List<Replace> replaces = new List<Replace>();
            
            while ((seek = name.IndexOf('{', offset)) >= 0)
            {
                if (seek == name.Length - 1) break;
                if (name[seek + 1] == '{') 
                { // take inner '{' if multiple {'s.
                    offset = seek + 1;
                    continue;
                }

                int seek2 = name.IndexOf('}', offset);
                if (seek2 == -1) break;
                var prop = name.Substring(seek + 1, seek2 - seek - 1);
                prop = prop.Trim().ToLower();
                while (prop.Contains("  "))
                   prop = prop.Replace("  ", " ");
                
                if (props.ContainsKey(prop))
                {
                    var property = props[prop];
                    var value = props[prop].GetValue(step);
                    var unitattr = property.GetAttribute<UnitAttribute>();
                    string valueString = null;
                    bool isCollection = value is IEnumerable && false == value is string;
                    if (value == null)
                    {
                        valueString = "Not Set";
                    }
                    else if (unitattr != null && !isCollection)
                    {
                        var fmt = new NumberFormatter(System.Globalization.CultureInfo.CurrentCulture, unitattr);
                        valueString = fmt.FormatNumber(value);
                    }
                    else if(isCollection)
                    {   
                        if (value.GetType().GetEnumerableElementType().IsNumeric())
                        {   // if IEnumerable<NumericType> use number formatter.
                            var fmt = new NumberFormatter(System.Globalization.CultureInfo.CurrentCulture, unitattr);
                            valueString = fmt.FormatRange(value as IEnumerable);
                        }else
                        {   // else use ToString.
                            valueString = string.Join(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator + " ", ((IEnumerable)value).Cast<object>().Select(o => o == null ? "NULL" : o));
                        }
                    }
                    else
                    {
                        valueString = value.ToString();
                    }
                    replaces.Add(new Replace { StartIndex = seek, EndIndex = seek2, Content = valueString });
                }
                offset = seek2 + 1;
            }

            StringBuilder outName = new StringBuilder(name);

            for (int i = replaces.Count - 1; i >= 0; i--)
            {
                var rep = replaces[i];
                outName.Remove(rep.StartIndex, rep.EndIndex - rep.StartIndex + 1);
                outName.Insert(rep.StartIndex, rep.Content);
             }
            return outName.ToString();
        }
    }
}
