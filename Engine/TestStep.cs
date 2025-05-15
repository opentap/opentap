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
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;


namespace OpenTap
{
    /// <summary>
    /// All TestSteps that are instances of the TestStep abstract class should override the <see cref="TestStep.Run"/> method. 
    /// Additionally, the  <see cref="TestStep.PrePlanRun"/> and <see cref="TestStep.PostPlanRun"/> methods can be overridden.
    /// </summary>
    /// <remarks>
    /// <see cref="ITestStep"/> can also be inherited from instead.
    /// </remarks>
    [ComVisible(true)]
    [Guid("d0b06600-7bac-47fb-9251-f834e420623f")]
    public abstract class TestStep : ValidatingObject, ITestStep, IBreakConditionProvider, IDescriptionProvider, 
        IDynamicMembersProvider, IInputOutputRelations, IParameterizedMembersCache, IDynamicMemberValue
    {
        #region Properties
        /// <summary>
        /// Gets or sets the verdict. Only available during test step run. 
        /// The value of this property will be propagated to the TestStepRun when the step run completes.
        /// </summary>
        [Browsable(false)]
        [ColumnDisplayName(Order : -99, IsReadOnly : true)]
        [XmlIgnore]
        [Output(OutputAvailability.AfterDefer)]
        [SettingsIgnore]
        [MetaData]
        public Verdict Verdict { get; set; }

        bool enabled = true;
        /// <summary>
        /// Gets or sets boolean indicating whether this step is enabled in the TestPlan
        /// </summary>
        [ColumnDisplayName("", Order : -101)]
        [Display("Enabled", "Enabling/Disabling the test step decides if" +
                            " it should be used when the test plan is executed. " +
                            "This value should not be changed during test plan run.", Group: "Common", Order: 20000, Collapsed: true)]
        [Unsweepable]
        [NonMetaData]
        [DefaultValue(true)]
        public bool Enabled
        {
            get => enabled; 
            set
            {
                if (enabled == value) return;
                enabled = value;
                OnPropertyChanged(nameof(Enabled));
            }
        }

        /// <summary>
        /// Gets or sets boolean indicating whether this step is read-only in the TestPlan. 
        /// This is mostly a declaration of intent, GUIs should respect it, but few things enforces it.
        /// </summary>
        [Browsable(false)]
        [XmlIgnore]
        [AnnotationIgnore]
        [SettingsIgnore]
        public bool IsReadOnly { get; set; }

        private string name;
        /// <summary>
        /// Gets or sets the name of the TestStep instance. Not allowed to be null.
        /// In many cases the name is unique within a test plan, but this should not be assumed, use <see cref="Id"/>for an unique identifier.
        /// May not be null.
        /// </summary>
        [ColumnDisplayName(nameof(Name), Order : -100)]
        [Display("Step Name", "The name of the test step, this value can be used to identifiy a test step. " +
                              "Test step names are not guaranteed to be unique. " +
                              "Name can include names of a setting of the step, this property will dynamically be " +
                              "replaced with it's current value in some views.", Group: "Common", Order: 20001, Collapsed: true)]
        [Unsweepable]
        [MetaData(Frozen = true)]
        public string Name
        {
            get => name;
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value), "TestStep.Name cannot be null.");
                if (value == name) return;
                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        string typeName;
        /// <summary>
        /// This TestStep type as a <see cref="string"/>.   
        /// </summary>
        [ColumnDisplayName("Type", Order : 1)]
        [Browsable(false)]
        public string TypeName => typeName ?? (typeName = TypeData.GetTypeData(this)
            .GetDisplayAttribute().GetFullName());

        TestStepList childTestSteps;
        /// <summary>
        /// Gets or sets a List of child <see cref="TestStep"/>s. Any TestSteps in this list will be
        /// executed instead of the Run method of this TestStep.
        /// </summary>
        [Browsable(false)]
        [AnnotationIgnore]
        [SettingsIgnore]
        public TestStepList ChildTestSteps
        {
            get => childTestSteps; 
            set
            {
                childTestSteps = value;
                childTestSteps.Parent = this;
                OnPropertyChanged(nameof(ChildTestSteps));
            }
        }
        
        /// <summary>
        /// The parent of this TestStep. Can be another TestStep or the <see cref="TestPlan"/>.  
        /// </summary>
        [XmlIgnore]
        [AnnotationIgnore]
        [SettingsIgnore]
        public virtual ITestStepParent Parent { get; set; }

        /// <summary>
        /// Result proxy that stores TestStep run results until they are propagated to the <see cref="ResultListener"/>.   
        /// </summary>
        [XmlIgnore]
        [AnnotationIgnore]
        [SettingsIgnore]
        public ResultSource Results { get; internal set; }

        /// <summary>
        /// The enumeration of all enabled Child Steps.
        /// </summary>
        [AnnotationIgnore]
        [SettingsIgnore]
        public IEnumerable<ITestStep> EnabledChildSteps => this.GetEnabledChildSteps();

        string CalcVersion()
        {
            // sometimes Assembly does not have a well-formatted semantic version.
            // ins this case we just use Version.ToString(3).
            var asm = TypeData.GetTypeDataSource(TypeData.GetTypeData(this));
            if (asm == null) return null;
            return asm.Version;
        }
        
        /// <summary>
        /// Version of this test step.
        /// </summary>
        [XmlIgnore]
        public string Version
        {
            get => CalcVersion();
            set
            {
                var installedVersionStr = CalcVersion();
                if (installedVersionStr == null)
                {
                    return;
                }

                if (installedVersionStr == value)
                    return;
                
                if(SemanticVersion.TryParse(installedVersionStr, out var installedVersion) && SemanticVersion.TryParse(value, out SemanticVersion createdVersion))
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
        public static string[] GenerateDefaultNames(ITypeData stepType)
        {
            if (stepType == null)
                throw new ArgumentNullException(nameof(stepType));
            var disp = stepType.GetDisplayAttribute();
            return disp.Group.Append(disp.Name).ToArray();
        }

        /// <summary> Returns a default name for a step type. </summary>
        /// <param name="stepType"></param>
        /// <returns></returns>
        [Obsolete("Use other overload of GenerateDefaultNames instead.")]
        public static string[] GenerateDefaultNames(Type stepType) => GenerateDefaultNames(TypeData.FromType(stepType));
        
        static ConditionalWeakTable<ITypeData, string> defaultNames = new ConditionalWeakTable<ITypeData, string>();

        /// <summary>
        /// Initializes a new instance of the TestStep base class.
        /// </summary>
        public TestStep()
        {
            var t = TypeData.GetTypeData(this);
            name = defaultNames.GetValue(t, type => GenerateDefaultNames(type).Last());
            
            Enabled = true;
            IsReadOnly = false;
            ChildTestSteps = new TestStepList();

            var things = loaderLookup.GetValue(t, loadDefaultResources);
            foreach (var loader in things)
                loader(this);
            Results = null; // this will be set by DoRun just before calling Run()
        }

        static readonly ConditionalWeakTable<ITypeData, Action<object>[]> loaderLookup = new ConditionalWeakTable<ITypeData, Action<object>[]>();

        static Action<object>[] loadDefaultResources(ITypeData t)
        {
            List<Action<object>> loaders = new List<Action<object>>();
            var props = t.GetMembers();
            foreach (var prop in props.Where(p => p.Writable))
            {
                var propType = (prop.TypeDescriptor as TypeData)?.Load();
                if (propType == null) continue;
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
                                        prop.SetValue(x, value);
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
                else if(ComponentSettingsList.HasContainer(propType))
                {

                    loaders.Add(x =>
                    {
                        try
                        {
                            var currentValue = prop.GetValue(x);
                            if (currentValue != null)
                                return; // if the constructor already set a value, don't overwrite it
                        }
                        catch (Exception ex)
                        {
                            Log.Warning("Caught exception while getting default value on {0}.", prop);
                            Log.Debug(ex);
                        }
                        IList list = ComponentSettingsList.GetContainer(propType);
                        if (list != null)
                        {
                            object value = list.Cast<object>()
                                .FirstOrDefault(o => o != null && o.GetType().DescendsTo(propType));

                            try
                            {
                                prop.SetValue(x, value);
                            }
                            catch (Exception ex)
                            {
                                Log.Warning("Caught exception while setting default value on {0}.", prop);
                                Log.Debug(ex);
                            }
                        }
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
        protected T GetParent<T>()
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
        /// <param name="throwOnBreak">Whether an exception will be thrown due to break conditions or if they will be caught. Exceptions are still available on child test steps TestStepRun.Exception. </param>
        protected IEnumerable<TestStepRun> RunChildSteps(bool throwOnBreak)
        {
            return RunChildSteps(null, CancellationToken.None, throwOnBreak);
        }

        /// <summary>
        /// Runs all enabled <see cref="TestStep.ChildTestSteps"/> of this TestStep. Upgrades parent verdict to the resulting verdict of the childrens run. Throws an exception if the child step does not belong or isn't enabled.
        /// </summary>
        /// <param name="attachedParameters">Parameters that will be stored together with the actual parameters of the steps.</param>
        /// <param name="cancellationToken">Provides a way to cancel the execution of child steps before all steps are executed.</param>
        protected IEnumerable<TestStepRun> RunChildSteps(IEnumerable<ResultParameter> attachedParameters, CancellationToken cancellationToken)
        {
            return RunChildSteps(attachedParameters, cancellationToken, true);
        }

        /// <summary>
        /// Runs all enabled <see cref="TestStep.ChildTestSteps"/> of this TestStep. Upgrades parent verdict to the resulting verdict of the childrens run. Throws an exception if the child step does not belong or isn't enabled.
        /// </summary>
        /// <param name="attachedParameters">Parameters that will be stored together with the actual parameters of the steps.</param>
        /// <param name="cancellationToken">Provides a way to cancel the execution of child steps before all steps are executed.</param>
        /// <param name="throwOnBreak">Whether an exception will be thrown due to break conditions or if they will be caught. Exceptions are still available on child test steps TestStepRun.Exception. </param>
        protected IEnumerable<TestStepRun> RunChildSteps(IEnumerable<ResultParameter> attachedParameters, CancellationToken cancellationToken, bool throwOnBreak)
        {
            return this.RunChildSteps(PlanRun, StepRun, attachedParameters, cancellationToken, throwOnBreak);
        }

        /// <summary>
        /// Runs the specified child step if enabled. Upgrades parent verdict to the resulting verdict of the child run. Throws an exception if childStep does not belong or isn't enabled.
        /// </summary>
        /// <param name="childStep">The child step to run.</param>
        /// <param name="attachedParameters">Parameters that will be stored together with the actual parameters of the step.</param>
        protected TestStepRun RunChildStep(ITestStep childStep, IEnumerable<ResultParameter> attachedParameters = null)
        {
            return this.RunChildStep(childStep, PlanRun, StepRun, attachedParameters);
        }
        
        /// <summary>
        /// Runs the specified child step if enabled. Upgrades parent verdict to the resulting verdict of the child run. Throws an exception if childStep does not belong or isn't enabled.
        /// </summary>
        /// <param name="childStep">The child step to run.</param>
        /// <param name="throwOnBreak"></param>
        /// <param name="attachedParameters">Parameters that will be stored together with the actual parameters of the step.</param>
        protected TestStepRun RunChildStep(ITestStep childStep, bool throwOnBreak, IEnumerable<ResultParameter> attachedParameters = null)
        {
            return this.RunChildStep(childStep, throwOnBreak, PlanRun, StepRun, attachedParameters);
        }

        /// <summary>
        /// Gets or sets the <see cref="TestPlanRun"/>.  
        /// </summary>
        [Browsable(false)]
        [XmlIgnore]
        [AnnotationIgnore]
        public TestPlanRun PlanRun { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="TestStepRun"/>. 
        /// </summary>
        [Browsable(false)]
        [XmlIgnore]
        [AnnotationIgnore]
        public TestStepRun StepRun { get; set; }

        /// <summary> Gets or sets the ID used to uniquely identify a test step within a test plan. </summary>
        [XmlAttribute("Id")]
        [Browsable(false)]
        [AnnotationIgnore]
        [SettingsIgnore]
        public Guid Id { get; set; } = Guid.NewGuid();

        // Implementing this interface will make setting and getting break conditions faster.
        BreakCondition IBreakConditionProvider.BreakCondition { get; set; } = BreakCondition.Inherit;
        // Implementing this interface will make setting and getting descriptions faster.
        string IDescriptionProvider.Description { get; set; }
        // Implementing this interface will make setting and getting dynamic members faster.
        IImmutableDictionary<string, IMemberData> IDynamicMembersProvider.DynamicMembers { get; set; } = ImmutableDictionary<string, IMemberData>.Empty;

        InputOutputRelation[] IInputOutputRelations.Inputs { get; set; }
        InputOutputRelation[] IInputOutputRelations.Outputs { get; set; }

        ImmutableDictionary<IMemberData, ParameterMemberData> parameterMembers =
            ImmutableDictionary<IMemberData, ParameterMemberData>.Empty;

        void IParameterizedMembersCache.RegisterParameterizedMember(IMemberData mem, ParameterMemberData memberData)
        {
            parameterMembers = parameterMembers.Add(mem, memberData);
        }

        void IParameterizedMembersCache.UnregisterParameterizedMember(IMemberData mem, ParameterMemberData memberData)
        {
            parameterMembers = parameterMembers.Remove(mem);
        }

        ParameterMemberData IParameterizedMembersCache.GetParameterFor(IMemberData mem)
        {
            if (parameterMembers.TryGetValue(mem, out var r))
                return r;
            return null;
        }

        ImmutableDictionary<IMemberData, object> dynamicMemberValues = ImmutableDictionary<IMemberData, object>.Empty;


        int IDynamicMemberValue.TypeDataKey { get; set; }
        bool IDynamicMemberValue.TryGetValue(IMemberData member, out object obj)
        {
            return dynamicMemberValues.TryGetValue(member, out obj);
        }

        void IDynamicMemberValue.SetValue(IMemberData member, object value)
        {
            while (true)
            {
                var initValue = dynamicMemberValues;
                var newValue = dynamicMemberValues.SetItem(member, value);
                if (initValue == Interlocked.CompareExchange(ref dynamicMemberValues, newValue, initValue))
                    break;
            }
        }
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
        public static T GetParent<T>(this ITestStep step) => GetParent<T>((ITestStepParent)step);
        
        /// <summary>
        /// Searches up through the Parent steps and returns the first step of the requested type that it finds.  
        /// </summary>
        /// <typeparam name="T">The type of TestStep to get.</typeparam>
        /// <returns>The closest TestStep of the requested type in the hierarchy.</returns>
        public static T GetParent<T>(this ITestStepParent item)
        {
            item = item.Parent;
            while (item != null)
            {
                if (item is T p)
                    return p;
                item = item.Parent;
            }
            return default;
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
        /// <param name="step"></param>
        /// <param name="currentPlanRun">The current TestPlanRun.</param>
        /// <param name="currentStepRun">The current TestStepRun.</param>
        /// <param name="attachedParameters">Parameters that will be stored together with the actual parameters of the steps.</param>
        public static IEnumerable<TestStepRun> RunChildSteps(this ITestStep step, TestPlanRun currentPlanRun, TestStepRun currentStepRun, IEnumerable<ResultParameter> attachedParameters = null)
        {
            return RunChildSteps(step, currentPlanRun, currentStepRun, attachedParameters, CancellationToken.None);
        }

        /// <summary>
        /// Runs all enabled <see cref="TestStep.ChildTestSteps"/> of this TestStep. Upgrades parent verdict to the resulting verdict of the childrens run. Throws an exception if the child step does not belong or isn't enabled.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="currentPlanRun">The current TestPlanRun.</param>
        /// <param name="currentStepRun">The current TestStepRun.</param>
        /// <param name="attachedParameters">Parameters that will be stored together with the actual parameters of the steps.</param>
        /// <param name="cancellationToken">Provides a way to cancel the execution of child steps before all steps are executed.</param>
        public static IEnumerable<TestStepRun> RunChildSteps(this ITestStep step, TestPlanRun currentPlanRun,
            TestStepRun currentStepRun,
            IEnumerable<ResultParameter> attachedParameters, CancellationToken cancellationToken)
        {
            return step.RunChildSteps(currentPlanRun, currentStepRun, attachedParameters, cancellationToken, true);
        }


        /// <summary>
        /// Runs all enabled <see cref="TestStep.ChildTestSteps"/> of this TestStep. Upgrades parent verdict to the resulting verdict of the childrens run. Throws an exception if the child step does not belong or isn't enabled.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="currentPlanRun">The current TestPlanRun.</param>
        /// <param name="currentStepRun">The current TestStepRun.</param>
        /// <param name="attachedParameters">Parameters that will be stored together with the actual parameters of the steps.</param>
        /// <param name="cancellationToken">Provides a way to cancel the execution of child steps before all steps are executed.</param>
        /// <param name="throwOnBreak">Whether an exception will be thrown due to break conditions or if they will be caught. Exceptions are still available on child test steps TestStepRun.Exception. </param>
        public static IEnumerable<TestStepRun> RunChildSteps(this ITestStep step, TestPlanRun currentPlanRun, TestStepRun currentStepRun, 
            IEnumerable<ResultParameter> attachedParameters, CancellationToken cancellationToken, bool throwOnBreak)
        {
            if (currentPlanRun == null)
                throw new ArgumentNullException(nameof(currentPlanRun));
            if (currentStepRun == null)
                throw new ArgumentNullException(nameof(currentStepRun));
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            if (step.StepRun == null)
                throw new Exception("Cannot run child steps outside the Run method.");

            step.StepRun.SupportsJumpTo = true;

            var steps = step.ChildTestSteps;
            if (steps.Count == 0) return Array.Empty<TestStepRun>();
            List<TestStepRun> runs = new List<TestStepRun>(steps.Count);
            try
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    var stepI = steps[i];
                    if (stepI.Enabled == false) continue;

                    TestStepRun run = stepI.DoRun(currentPlanRun, currentStepRun, attachedParameters);

                    if (!run.Skipped)
                        runs.Add(run);

                    if (cancellationToken.IsCancellationRequested) break;

                    // note: The following is slightly modified from something inside TestPlanExecution.cs
                    if (run.SuggestedNextStep is Guid id)
                    {
                        if (id == step.Id)
                        {
                            // If suggested next step is the parent step, skip executing child steps.
                            break;
                        }

                        var stepidx = steps.IndexWhen(x => x.Id == id);
                        if (stepidx >= 0)
                            i = stepidx - 1; // next iteration will be that one.
                        else
                        {
                            var seek = step.Parent;
                            while (seek != null)
                            {
                                if (seek is ITestStep step2 && id == step2.Id)
                                {
                                    currentStepRun.SuggestedNextStep = id;
                                    return runs;
                                }
                                seek = seek.Parent;
                            }
                        }
                        // if skip to next step, don't add it to the wait queue.
                    }
                    if (run.BreakConditionsSatisfied())
                    {
                        run.LogBreakCondition();
                        if (throwOnBreak)
                        {
                            if (run.Exception != null)
                                ExceptionDispatchInfo.Capture(run.Exception).Throw();
                            run.ThrowDueToBreakConditions();
                        }
                        else break;
                    }
                    
                    TapThread.ThrowIfAborted();
                }
            }
            finally
            {

                if (runs.Count > 0) // Avoid deferring if there is nothing to do.
                {
                    void processRuns()
                    {
                        foreach (var run in runs)
                        {
                            run.WaitForCompletion();
                            step.UpgradeVerdict(run.Verdict);
                        }
                    }
                    if (step is TestStep testStep && runs.Any(x => x.WasDeferred))
                    {
                        testStep.Results.DeferNoCheck(processRuns);
                    }
                    else
                    {
                        processRuns();
                    }
                }
            }

            return runs;
        }

        /// <summary>
        /// Runs the specified child step if enabled. Upgrades parent verdict to the resulting verdict of the child run. Throws an exception if childStep does not belong or isn't enabled.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="childStep">The child step to run.</param>
        /// <param name="currentPlanRun">The current TestPlanRun.</param>
        /// <param name="currentStepRun">The current TestStepRun.</param>
        /// <param name="attachedParameters">Parameters that will be stored together with the actual parameters of the step.</param>
        public static TestStepRun RunChildStep(this ITestStep step, ITestStep childStep,
            TestPlanRun currentPlanRun, TestStepRun currentStepRun,
            IEnumerable<ResultParameter> attachedParameters = null)
        {
            return step.RunChildStep(childStep, true, currentPlanRun, currentStepRun, attachedParameters);
        }
        
        
        /// <summary>
        /// Runs the specified child step if enabled. Upgrades parent verdict to the resulting verdict of the child run. Throws an exception if childStep does not belong or isn't enabled.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="childStep">The child step to run.</param>
        /// <param name="throwOnBreak">Whether an exception will be thrown due to break conditions or if they will be caught. Exceptions are still available on child test steps TestStepRun.Exception. </param>
        /// <param name="currentPlanRun">The current TestPlanRun.</param>
        /// <param name="currentStepRun">The current TestStepRun.</param>
        /// <param name="attachedParameters">Parameters that will be stored together with the actual parameters of the step.</param>
        public static TestStepRun RunChildStep(this ITestStep step, ITestStep childStep, bool throwOnBreak, TestPlanRun currentPlanRun, TestStepRun currentStepRun, IEnumerable<ResultParameter> attachedParameters = null)
        {
            if (childStep == null)
                throw new ArgumentNullException(nameof(childStep));
            if (currentPlanRun == null)
                throw new ArgumentNullException(nameof(currentPlanRun));
            if (currentStepRun == null)
                throw new ArgumentNullException(nameof(currentStepRun));
            if (step.StepRun == null)
                throw new Exception("Can only run child step during own step run.");
            if(childStep.Parent != step)
                throw new ArgumentException("childStep must be a child step of Step", nameof(childStep));
            if(childStep.Enabled == false)
                throw new ArgumentException("childStep must be enabled.", nameof(childStep));

            var run = childStep.DoRun(currentPlanRun, currentStepRun, attachedParameters);
            if (step is TestStep testStep && run.WasDeferred)
            {
                testStep.Results.DeferNoCheck(() =>
                {
                    run.WaitForCompletion();
                    step.UpgradeVerdict(run.Verdict);
                });
            }
            else
            {
                if(run.WasDeferred)
                    run.WaitForCompletion();
                step.UpgradeVerdict(run.Verdict);
            }

            if (run.BreakConditionsSatisfied())
            {
                run.LogBreakCondition();
                if(run.Verdict == Verdict.Error && throwOnBreak)
                    run.ThrowDueToBreakConditions();
            }
            return run;
        }

        internal static void LogBreakCondition(this TestStepRun run)
        {
            Log.CreateSource("TestStep").Debug( $"Break issued from '{run.TestStepName}' due to verdict {run.Verdict}. See Break Conditions settings.");
        }

        internal static string GetStepPath(this ITestStep Step)
        {
            var name = Step.GetFormattedName();

            StringBuilder sb = StringBuilderCache.GetStringBuilder();
            sb.Append('"');

            void getParentNames(ITestStep step)
            {
                if (step.Parent is ITestStep parent2)
                    getParentNames(parent2);

                sb.Append(step.GetFormattedName());
                sb.Append(" \\ ");
            }

            if (Step.Parent is ITestStep parent)
                getParentNames(parent);
            sb.Append(name);
            sb.Append('"');
            return sb.ToString();
        }

        static readonly object upgradeVerdictLock = new object();
        internal static void UpgradeVerdict(this ITestStep step, Verdict newVerdict)
        {
            if (step.Verdict < newVerdict)
            {
                var lockObj = step.StepRun?.upgradeVerdictLock ?? upgradeVerdictLock;

                lock (lockObj)
                {
                    if (step.Verdict < newVerdict) 
                        step.Verdict = newVerdict;
                }
            }
        }

        /// <summary> This is the currently executing test step or null, used to detect deadlock when a step is waiting for its parent. </summary>
        [ThreadStatic]
        internal static ITestStep currentlyExecutingTestStep = null;

        internal static TestStepRun DoRun(this ITestStep Step, TestPlanRun planRun, TestRun parentRun, IEnumerable<ResultParameter> attachedParameters = null)
        {
            {
                // in case the previous action was not completed yet.
                // this is a problem because StepRun might be set to null later
                // if its not already the case.
                Step.StepRun?.WaitForCompletion();
                Debug.Assert(Step.StepRun == null);
            }
            Step.PlanRun = planRun;
            Step.Verdict = Verdict.NotSet;

            TapThread.ThrowIfAborted();
            if (!Step.Enabled)
                throw new Exception("Test step not enabled."); // Do not run step if it has been disabled

            Exception readInputsError = null;
            try
            {
                InputOutputRelation.UpdateInputs(Step);
            }
            catch (Exception e)
            {
                readInputsError = e;
            }

            var stepRun = Step.StepRun = new TestStepRun(Step, parentRun, attachedParameters, planRun)
            {
                TestStepPath = Step.GetStepPath()
            };

            // evaluate pre run mixins
            var prerun = TestStepPreRunEvent.Invoke(Step);
            var skipStep = prerun.SkipStep; 

            planRun.ThrottleResultPropagation();

            var previouslyExecutingTestStep = currentlyExecutingTestStep;
            currentlyExecutingTestStep = Step;
            
            //Raise an event prior to starting the actual run of the TestStep. 
            Step.OfferBreak(stepRun, true);
            skipStep |= stepRun.SuggestedNextStep != null;
            if (skipStep) {
                Step.StepRun = null;
                stepRun.Skipped = true;
                return stepRun;    
            }
            
            TapThread.ThrowIfAborted(); // if an OfferBreak handler called TestPlan.Abort, abort now.
            
            if (prerun.AnyPrerunsInvoked)
            {
                // Update parameters after running prerun mixins. This is needed to reflect updated properties.
                // Note that this does not handle the edge case where e.g. a PreRun mixin caused 
                // the removal of a member sourced from some TypeData, but it is impossible to distinguish a TypeData parameter
                // from a manually added 'steprun.Parameters["foo"] = "bar" - style parameter.
                stepRun.UpdateParams();
            } 
            
            // To properly support single stepping stopwatch has to be below offerBreak
            // since OfferBreak requires a TestStepRun, this has to be re-instantiated.
            var swatch = Stopwatch.StartNew();
            try
            {
                try
                {
                    // tell result listeners the step started.
                    Step.PlanRun.ResourceManager.BeginStep(Step.PlanRun, Step, TestPlanExecutionStage.Run,
                        TapThread.Current.AbortToken);
                    try
                    {
                        if (readInputsError != null)
                            ExceptionDispatchInfo.Capture(readInputsError).Throw();
                        
                        if (Step is TestStep _step)
                            _step.Results = new ResultSource(stepRun, Step.PlanRun);
                        TestPlan.Log.Info("{0} started.", stepRun.TestStepPath);
                        stepRun.StartStepRun(); // set verdict to running, set Timestamp.
                        parentRun.ChildStarted(stepRun);
                        planRun.AddTestStepRunStart(stepRun);
                        Step.Run();
                        
                        {
                            // Evaluate post run mixins.
                            // This needs to be done before 'AfterRun' as that waits for defer and publishes results
                            // which the mixins must be able to affect.
                            TestStepPostRunEvent.Invoke(Step);
                        }
                        
                        stepRun.AfterRun(Step);
                        
                        TapThread.ThrowIfAborted();
                    }
                    finally
                    {
                        planRun.AddTestStepStateUpdate(stepRun.TestStepId, stepRun, StepState.Deferred);
                        currentlyExecutingTestStep = previouslyExecutingTestStep;
                    }
                }
                finally
                {
                    planRun.ResourceManager.EndStep(Step, TestPlanExecutionStage.Run);
                }
            }
            catch (TestStepBreakException e)
            {
                TestPlan.Log.Info(e.Message);
                Step.UpgradeVerdict(e.Verdict);
                stepRun.Exception = e;
            }
            catch (ExpectedException e)
            {
                e.Handle(Step.Name);
                Step.Verdict = e.Verdict;
                throw e;
            }
            catch (Exception e)
            {
                
                if (e is ThreadAbortException || (e is OperationCanceledException && TapThread.Current.AbortToken.IsCancellationRequested))
                {
                    Step.Verdict = Verdict.Aborted;
                    if(e.Message == new OperationCanceledException().Message)
                        TestPlan.Log.Warning("Test step {0} was canceled.", stepRun.TestStepPath);
                    else
                        TestPlan.Log.Warning("Test step {0} was canceled with message '{1}'.", stepRun.TestStepPath, e.Message);
                }
                else
                {
                    Step.Verdict = Verdict.Error;
                    TestPlan.Log.Error("Error running {0}: {1}.", stepRun.TestStepPath, e.Message);
                }
                TestPlan.Log.Debug(e);
                stepRun.Exception = e;
            }
            finally
            {
                
                // if it was a ThreadAbortException we need 'finally'.
                void completeAction(Task runTask)
                {
                    try
                    {
                        runTask.Wait();
                    }
                    catch (Exception e)
                    {
                        // Tasks wrap exceptions in AggregateExceptions with a single exception
                        while (e is AggregateException aex && aex.InnerExceptions.Count == 1)
                            e = aex.InnerException;
                        
                        if (e is ThreadAbortException || (e is OperationCanceledException && TapThread.Current.AbortToken.IsCancellationRequested) )
                        {
                            if (TapThread.Current.AbortToken.IsCancellationRequested && Step.Verdict < Verdict.Aborted)
                                Step.Verdict = Verdict.Aborted;
                            if (e.Message == new OperationCanceledException().Message)
                                TestPlan.Log.Warning("Test step {0} was canceled.", stepRun.TestStepPath);
                            else
                                TestPlan.Log.Warning("Test step {0} was canceled with message '{1}'.", stepRun.TestStepPath, e.Message);
                        }
                        else if (e is TestStepBreakException brk)
                        {
                            TestPlan.Log.Info(brk.Message);
                            Step.UpgradeVerdict(brk.Verdict);
                        }
                        else
                        {
                            Step.Verdict = Verdict.Error;
                            TestPlan.Log.Error("Error running {0}: {1}.", stepRun.TestStepPath, e.Message);
                        }
                        TestPlan.Log.Debug(e);
                        stepRun.Exception = e;
                    }
                    finally
                    {
                        lock (Step)
                        {
                            if (Step.StepRun == stepRun)
                            {
                                Step.StepRun = null;
                            }
                        }
                        TimeSpan time = swatch.Elapsed;
                        
                        stepRun.CompleteStepRun(planRun, Step, time);
                        if (Step.Verdict == Verdict.NotSet)
                        {
                            TestPlan.Log.Info(time, "{0} completed.", stepRun.TestStepPath);
                        }
                        else
                        {
                            TestPlan.Log.Info(time, "{0} completed with verdict '{1}'.", stepRun.TestStepPath, Step.Verdict);
                        }

                        planRun.AddTestStepRunCompleted(stepRun);
                        stepRun.SignalCompleted();
                    }
                }

                if (stepRun.ResultSource != null)
                    stepRun.ResultSource.Finally(completeAction);
                else
                    completeAction(Task.FromResult(0));
            }
            
            return stepRun;
        }



        
        internal static void CheckResources(this ITestStep Step)
        {
            // collect null members into a set. Any null member here is an error.
            var nullMembers = new HashSet<IMemberData>();
            GetObjectSettings<IResource,ITestStep, IMemberData>(Step, true, (x, mem) => x == null ? mem : null, nullMembers);
            foreach(var res in nullMembers)
            {
                throw new Exception(String.Format("Resource setting {1} not set on step {0}. Please configure or disable step.", Step.Name, res.GetDisplayAttribute().GetFullName()));
            }
            foreach(var step in Step.ChildTestSteps)
            {
                if(step.Enabled)
                    step.CheckResources();
            }
        }

        // this dictionary may be accessed by multiple threads, so it is best to use ConcurrentDictionary.
        static readonly Cache<(TypeData target, ITypeData source), (IMemberData, bool hasEnabledAttribute)[]> 
            membersLookup = new Cache<(TypeData target, ITypeData source), (IMemberData, bool hasEnabledAttribute)[]>(() => PluginManager.CacheState);

        static (IMemberData, bool hasEnabledAttribute)[] GetSettingsLookup(TypeData targetType, ITypeData sourceType)
        {
            if(membersLookup.TryGetValue((targetType, sourceType), out var value))
                return value;
            var propertyInfos = sourceType.GetMembers();
            // if the target type is 'object' we accept any type as a value. 
            bool anyType = Equals(TypeData.FromType(typeof(object)), targetType);

            List<(IMemberData, bool)> result = null;
            foreach (var prop in propertyInfos)
            {
                // Ignore prop if it is an IParameterMemberData because it should be managed by the object that owns it
                if (prop is IParameterMemberData) continue;
                if (prop.Readable == false) continue;
                var td2 = prop.TypeDescriptor.AsTypeData();
                if (td2 == null) continue;
                if (!anyType)
                {
                    if (td2.IsValueType && targetType.IsValueType == false) continue;
                    if (td2.IsString && targetType.IsString == false) continue;
                }

                bool hasEnabled = prop.HasAttribute<EnabledIfAttribute>();
                if (td2.DescendsTo(targetType) || td2.ElementType.DescendsTo(targetType) || td2.DescendsTo(typeof(IEnabled)))
                {
                    if (prop.HasAttribute<SettingsIgnoreAttribute>()) continue;
                    if(result == null) result = new List<(IMemberData, bool)>();
                    result.Add((prop, hasEnabled));
                }
            }

            return membersLookup.AddValue((targetType, sourceType), (result?.ToArray() ?? Array.Empty<(IMemberData, bool)>()));
        }
        
        
        /// <summary>
        /// Returns the properties of a specific type from a number of objects. This will traverse IEnumerable and optionally IEnabled properties.
        /// </summary>
        /// <param name="item">The object to get properties from.</param>
        /// <param name="onlyEnabled">If true, Enabled and EnabledIf properties are only traversed into if they are enabled.</param>
        /// <param name="transform">This transform function is called on each object, and being passed the corresponding PropertyInfo instance from the parent object.</param>
        /// <param name="itemSet">The set of items to populate</param>
        /// <param name="targetType">The TypeData of the target type (Optional).</param>
        /// <returns></returns>
        internal static void GetObjectSettings<T,T2,T3>(T2 item, bool onlyEnabled, Func<T, IMemberData, T3> transform, HashSet<T3> itemSet, TypeData targetType = null)
        {
            if (transform == null) transform = (t, data) => (T3)(object)t; 
            if(targetType == null) targetType = TypeData.FromType(typeof(T));
            var enabledAttributes = new List<EnabledIfAttribute>();

            var properties = GetSettingsLookup(targetType, TypeData.GetTypeData(item));
            foreach (var (prop, hasEnabled) in properties)
            {
                if (onlyEnabled && hasEnabled)
                {
                    enabledAttributes.Clear();
                    prop.GetAttributes<EnabledIfAttribute>(enabledAttributes);
                    bool nextProperty = false;
                    foreach (var attr in enabledAttributes)
                    {
                        bool isEnabled = EnabledIfAttribute.IsEnabled(attr, item);
                        if (isEnabled == false)
                        {
                            nextProperty = true;
                            break;
                        }
                    }

                    if (nextProperty) continue;
                }

                object value;
                try
                {
                    value = prop.GetValue(item);
                }
                catch
                {
                    continue;
                }

                if (value is IEnabled e)
                {
                    if (e.IsEnabled == false && onlyEnabled)
                        continue;
                    GetObjectSettings(value, onlyEnabled, transform, itemSet, targetType);
                    continue;
                }

                if (value is T t2)
                {
                    itemSet.AddExceptNull(transform(t2, prop));
                }
                else if (value is string)
                    continue;
                else if (value == null && prop.TypeDescriptor.DescendsTo(targetType))
                {
                    itemSet.AddExceptNull(transform((T) value, prop));
                }
                else if (value is IEnumerable seq)
                {
                    if (seq is IList lst)
                    {
                        for (int i = 0; i < lst.Count; i++)
                        {
                            var value2 = lst[i];
                            if (value2 is T x)
                                itemSet.AddExceptNull(transform(x, prop));
                        }
                    }
                    else
                    {
                        if (value is IEnumerable<T> seq2)
                        {
                            foreach (var x in seq2.ToArray())
                                itemSet.AddExceptNull(transform(x, prop));
                        }
                        else
                        {
                            foreach (var x in seq.OfType<T>().ToArray())
                                itemSet.AddExceptNull(transform(x, prop));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the properties of a specific type from a number of objects. This will traverse IEnumerable and optionally IEnabled properties.
        /// </summary>
        /// <param name="objects">The objects to return properties from.</param>
        /// <param name="onlyEnabled">If true, Enabled and EnabledIf properties are only traversed into if they are enabled.</param>
        /// <param name="transform">This transform function is called on each object, and being passed the corresponding PropertyInfo instance from the parent object.</param>
        /// <param name="itemSet"> The set of elements being populated.  </param>
        /// <returns></returns>
        internal static void GetObjectSettings<T,T2,T3>(IEnumerable<T2> objects, bool onlyEnabled, Func<T, IMemberData, T3> transform, HashSet<T3> itemSet)
        {
            if (transform == null)
                transform = (x, prop) => (T3)(object)x;
            var targetType = TypeData.FromType(typeof(T));
            foreach (var item in objects)
                GetObjectSettings(item, onlyEnabled, transform, itemSet, targetType);
        }

        
        struct Replace
        {
            public int StartIndex;
            public int EndIndex;
            public string Content;
        }
        
        /// <summary> Cache for quickly getting the display names of members used to generate the formatted name. </summary>
        static readonly ConditionalWeakTable<ITypeData, Dictionary<string, IMemberData>> formatterLutCache 
            = new ConditionalWeakTable<ITypeData, Dictionary<string, IMemberData>>();
        
        /// <summary> Takes the name of step and replaces {} tokens with the value of properties. </summary>
        public static string GetFormattedName(this ITestStep step)
        {
            if(step.Name.Contains('{') == false || step.Name.Contains('}') == false)
                return step.Name;
            var type = TypeData.GetTypeData(step);
            if (formatterLutCache.TryGetValue(type, out var props) == false)
            {
                // GetProperties potentially slow. GetFormattedName is in test plan exec thread, so the outcome is cached.
                
                props = new Dictionary<string, IMemberData>();
                foreach (var item in type.GetMembers())
                {
                    var browsable = item.GetAttribute<BrowsableAttribute>();
                    if (browsable != null && browsable.Browsable == false) continue;
                    if (item.Readable == false) 
                        continue;
                    var s = item.GetDisplayAttribute();
                    if ((s.Group != null) && (s.Group.Length > 0))
                    {
                        // GroupName [space] DisplayName
                        var fullFormat = $"{string.Join(" ", s.Group.Select(g => g.Trim()))} {s.Name.Trim()}";
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

                formatterLutCache.GetValue(type, _ => props);
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
                    var value = property.GetValue(step);
                    var unitattr = property.GetAttribute<UnitAttribute>();
                    string valueString = null;
                    bool isCollection = value is IEnumerable && false == value is string;

                    if (value == null)
                        valueString = "Not Set";
                    else if (unitattr != null && !isCollection)
                    {
                        var fmt = new NumberFormatter(System.Globalization.CultureInfo.CurrentCulture, unitattr);
                        valueString = fmt.FormatNumber(value);
                    }
                    else if (isCollection)
                    {   
                        if (value.GetType().GetEnumerableElementType().IsNumeric())
                        {   // if IEnumerable<NumericType> use number formatter.
                            var fmt = new NumberFormatter(System.Globalization.CultureInfo.CurrentCulture, unitattr);
                            valueString = fmt.FormatRange(value as IEnumerable);
                        }
                        else
                        {   // else use ToString.
                            valueString = string.Join(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator + " ", ((IEnumerable)value).Cast<object>().Select(o => o ?? "NULL"));
                        }
                    }
                    else if (value is Enum e)
                        valueString = Utils.EnumToReadableString(e);
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
            return outName.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Marks a property as not a setting. This is a performance optimization for when finding resources throughout the test plan.
    /// </summary>
    internal class SettingsIgnoreAttribute : Attribute
    { }
}
