using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    /// <summary>
    /// Mixins can be used with EmbedPropertiesAttribute to give extra functionality to a test step.
    /// </summary>
    public interface IMixin : ITapPlugin
    {
        
    }

    abstract class MixinEvent<T2> where T2: IMixin
    {
        static readonly TraceSource log = Log.CreateSource("Mixin");
        protected static T1 Invoke<T1>(object target, Action<T2, T1> f, T1 arg) => Invoke(target, f, null, arg,  out _);
        protected static T1 Invoke<T1>(object target, Action<T2, T1> f, Func<T2, double> ordering, T1 arg, out bool anyInvoked)
        {
            anyInvoked = false;
            var emb = TypeData.GetTypeData(target).GetBaseType<EmbeddedTypeData>();
            if (emb == null) return arg;
            var embeddingMembers = emb.GetEmbeddingMembers();

            List<T2> objects = null;

            foreach (var mem in embeddingMembers)
            {
                if (!mem.TypeDescriptor.DescendsTo(typeof(T2))) continue;
                if (mem.Readable == false) continue;

                if (mem.GetValue(target) is T2 mixin)
                {
                    (objects ??= []).Add(mixin);
                }
            }

            if (objects != null)
            {

                IEnumerable<T2> orderedObjects = ordering != null ? objects.OrderBy(ordering) : objects;

                foreach (var mixin in orderedObjects)
                {
                    try
                    {
                        f(mixin, arg);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        log.Error("Caught error in mixin: {0}", e.Message);
                        log.Debug(e);
                    }
                }
                anyInvoked = true;
            }


            return arg;
        }
        
    }
    
    class TestStepPreRunEvent : MixinEvent<ITestStepPreRunMixin>
    {
        public static TestStepPreRunEventArgs Invoke(ITestStep step)
        {
            var eventArg = new TestStepPreRunEventArgs(step);
            Invoke(step, static (v, arg) => v.OnPreRun(arg), static v => (v as ITestStepPreRunMixinOrder)?.GetPreRunOrder() ?? 0.0, eventArg, out bool anyInvoked);
            eventArg.AnyPrerunsInvoked = anyInvoked;
            return eventArg;
        }
    }

    /// <summary> Event args for ITestStepPreRun mixin. </summary>
    public sealed class TestStepPreRunEventArgs
    {
        /// <summary> The step for which the event happens. </summary>
        public ITestStep TestStep { get; }
        /// <summary> Can be set to true to skip the step</summary>
        public bool SkipStep { get; set; }
        
        /// <summary> Indicates whether any prerun mixins were invoked. </summary>
        internal bool AnyPrerunsInvoked { get; set; }

        internal TestStepPreRunEventArgs(ITestStep step) => TestStep = step;
    }
    
    class TestPlanPreRunEvent : MixinEvent<ITestPlanPreRunMixin>
    {
        public static TestPlanPreRunEventArgs Invoke(TestPlan plan, TestPlanRun execStage) => 
            Invoke(plan, (v, arg) => v.OnPreRun(arg), new TestPlanPreRunEventArgs(plan, execStage));
    }
    
    /// <summary> Event args for ITestStepPreRun mixin. </summary>
    public sealed class TestPlanPreRunEventArgs
    {
        /// <summary> The step for which the event happens. </summary>
        public TestPlan TestPlan { get; }

        /// <summary> The plan run for the given test plan. </summary>
        public TestPlanRun PlanRun { get; }

        internal TestPlanPreRunEventArgs(TestPlan step, TestPlanRun planRun)
        {
            TestPlan = step;
            PlanRun = planRun;
        }
    }

    class TestStepPostRunEvent : MixinEvent<ITestStepPostRunMixin>
    {
        public static void Invoke(ITestStep step) => 
            Invoke(step, static (mixin, args) => mixin.OnPostRun(args), static v => (v as ITestStepPostRunMixinOrder)?.GetPostRunOrder() ?? 0, new TestStepPostRunEventArgs(step), out _);
    }
    
    /// <summary> Event args for ITestStepPostRun mixin. </summary>
    public sealed class TestStepPostRunEventArgs
    {
        /// <summary> The step for which the event happens. </summary>
        public ITestStep TestStep { get; }
        
        internal TestStepPostRunEventArgs(ITestStep step)  => TestStep = step;
    }

    class ResourcePreOpenEvent: MixinEvent<IResourcePreOpenMixin>
    {
        public static void Invoke(IResource resource) => 
            Invoke(resource, static (mixin, args) => mixin.OnPreOpen(args), new ResourcePreOpenEventArgs(resource));
    }

    /// <summary> Event args for IResourcePreOpenMixin mixin. </summary>
    public class ResourcePreOpenEventArgs
    {
        /// <summary> The resource for which the event happens. </summary>
        public IResource Resource { get; }
        
        internal ResourcePreOpenEventArgs(IResource resource) => Resource = resource;
    }
    
    /// <summary> This mixin is activated just after a step has been executed. It allows modifying the test step run. </summary>
    public interface ITestStepPostRunMixin : IMixin
    {
        /// <summary> Invoked after test step run.</summary>
        void OnPostRun(TestStepPostRunEventArgs eventArgs);
    }

    /// <summary> Defines an ordering for test step post run mixins. </summary>
    public interface ITestStepPostRunMixinOrder : ITestStepPostRunMixin
    {

        /// <summary>
        /// Gets the order. Default value is 0. The ordering is ascending.
        /// </summary>
        double GetPostRunOrder();
    }


    /// <summary> This mixin is activated just before a step is executed. It allows modifying the test step run. </summary>
    public interface ITestStepPreRunMixin : IMixin
    {
        /// <summary> Invoked before test step run.</summary>
        void OnPreRun(TestStepPreRunEventArgs eventArgs);
    }

    /// <summary> Defines an ordering for test step pre run mixins. </summary>
    public interface ITestStepPreRunMixinOrder : ITestStepPreRunMixin
    {
        /// <summary> Gets the order. Default value is 0. The ordering is ascending. </summary>
        double GetPreRunOrder();
    }

    /// <summary> This mixin is activated just before a resource opens. </summary>
    public interface IResourcePreOpenMixin : IMixin
    {
        /// <summary> Invoked just before IResource.Open is called.</summary>
        void OnPreOpen(ResourcePreOpenEventArgs eventArgs);
    }

    /// <summary> This mixin is activated just before a step is executed. It allows modifying the test step run. </summary>
    public interface ITestPlanPreRunMixin : IMixin
    {
        /// <summary> Invoked before test step run.</summary>
        void OnPreRun(TestPlanPreRunEventArgs eventArgs);
    }
}
