using System;

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
        protected static T1 Invoke<T1>(object target, Action<T2, T1> f, T1 arg) 
        {
            var emb = TypeData.GetTypeData(target).GetBaseType<EmbeddedTypeData>();
            if (emb == null) return arg;
            foreach (var mem in emb.GetEmbeddingMembers())
            {
                if (!mem.TypeDescriptor.DescendsTo(typeof(T2))) continue;
                if (mem.Readable == false) continue;
                if (mem.GetValue(target) is T2 mixin)
                {
                    f(mixin, arg);
                }
            }
            return arg;
        }
        
    }
    
    class TestStepPreRunEvent : MixinEvent<ITestStepPreRunMixin>
    {
        public static TestStepPreRunEventArgs Invoke(ITestStep step) => 
            Invoke(step, (v, arg) => v.OnPreRun(arg), new TestStepPreRunEventArgs(step));
    }
    
    /// <summary> Event args for ITestStepPreRun mixin. </summary>
    public sealed class TestStepPreRunEventArgs
    {
        /// <summary> The step for which the event happens. </summary>
        public ITestStep TestStep { get; }
        /// <summary> Can be set to true to skip the step</summary>
        public bool SkipStep { get; set; }

        internal TestStepPreRunEventArgs(ITestStep step) => TestStep = step;
    }
    
    class TestPlanPreRunEvent : MixinEvent<ITestPlanPreRunMixin>
    {
        public static TestPlanPreRunEventArgs Invoke(TestPlan plan) => 
            Invoke(plan, (v, arg) => v.OnPreRun(arg), new TestPlanPreRunEventArgs(plan));
    }
    
    /// <summary> Event args for ITestStepPreRun mixin. </summary>
    public sealed class TestPlanPreRunEventArgs
    {
        /// <summary> The step for which the event happens. </summary>
        public TestPlan TestPlan { get; }

        internal TestPlanPreRunEventArgs(TestPlan step) => TestPlan = step;
    }
    
    class TestStepPostRunEvent : MixinEvent<ITestStepPostRunMixin>
    {
        public static void Invoke(ITestStep step) => 
            Invoke(step, (v, args) => v.OnPostRun(args), new TestStepPostRunEventArgs(step));
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
            Invoke(resource, (v, args) => v.OnPreOpen(args), new ResourcePreOpenEventArgs(resource));
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

    /// <summary> This mixin is activated just before a step is executed. It allows modifying the test step run. </summary>
    public interface ITestStepPreRunMixin : IMixin
    {
        /// <summary> Invoked before test step run.</summary>
        void OnPreRun(TestStepPreRunEventArgs eventArgs);
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
