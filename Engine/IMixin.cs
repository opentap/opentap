using System;
namespace OpenTap
{
    /// <summary>
    /// Mixins can be used with EmbedPropertiesAttribute to give extra functionality to a test step.
    /// </summary>
    public interface IMixin : ITapPlugin
    {
        
    }

    /// <summary>
    /// Mixin event args
    /// </summary>
    /// <typeparam name="T">The mixin type this should be applied to.</typeparam>
    public abstract class MixinEventArgs<T> : EventArgs where T: IMixin
    {
        readonly object target;
        /// <summary> Creates a new instance of MixinEventArgs. </summary>
        /// <param name="target">The target object for the event. </param>
        protected MixinEventArgs(object target) => this.target = target;
        
        // invoke the event.
        internal void Invoke()
        {
            // Extract all the mixin properties of the given type.
            var emb = TypeData.GetTypeData(target).GetBaseType<EmbeddedTypeData>();
            if (emb == null) return;
            foreach (var mem in emb.GetEmbeddingMembers())
            {
                if (!mem.TypeDescriptor.DescendsTo(typeof(T))) continue;
                if (mem.Readable == false) continue;
                if (mem.GetValue(target) is T mixin)
                {
                    OnInvoke(mixin);
                }
            }
        }

        /// <summary> Defines how to invoke the mixin. </summary>
        protected abstract void OnInvoke(T v);
    }
    
    /// <summary> Event args for ITestStepPreRun mixin. </summary>
    public sealed class TestStepPreRunEventArgs : MixinEventArgs<ITestStepPreRunMixin>
    {
        /// <summary> The step for which the event happens. </summary>
        public ITestStep TestStep { get; }
        /// <summary> Can be set to true to skip the step</summary>
        public bool SkipStep { get; set; }
        
        internal TestStepPreRunEventArgs(ITestStep step) : base(step) => TestStep = step;
        
        /// <summary> Calls ITestStepPreRunMixin.OnPreRun</summary>
        protected override void OnInvoke(ITestStepPreRunMixin v) => v.OnPreRun(this);

        internal static TestStepPreRunEventArgs Invoke(ITestStep step)
        {
            var args = new TestStepPreRunEventArgs(step);
            args.Invoke();
            return args;
        }
    }

    /// <summary> Event args for ITestStepPostRun mixin. </summary>
    public sealed class TestStepPostRunEventArgs : MixinEventArgs<ITestStepPostRunMixin>
    {
        /// <summary> The step for which the event happens. </summary>
        public ITestStep TestStep { get; }
        
        TestStepPostRunEventArgs(ITestStep step) : base(step) => TestStep = step;
        
        internal static void Invoke(ITestStep step) 
            => new TestStepPostRunEventArgs(step).Invoke();
        
        /// <summary> Calls ITestStepPostRunMixin.OnPostRun. </summary>
        protected override void OnInvoke(ITestStepPostRunMixin v) => v.OnPostRun(this);
    }

    /// <summary> Event args for IResourcePreOpenMixin mixin. </summary>
    public class ResourcePreOpenEventArgs :  MixinEventArgs<IResourcePreOpenMixin>
    {
        /// <summary> The resource for which the event happens. </summary>
        public IResource Resource { get; }
        
        internal ResourcePreOpenEventArgs(IResource resource): base(resource) => Resource = resource;
        
        /// <summary> Calls IResourcePreOpenMixin.OnPreOpen. </summary>
        protected override void OnInvoke(IResourcePreOpenMixin v) => v.OnPreOpen(this);
        
        internal static void Invoke(IResource resource) => new ResourcePreOpenEventArgs(resource).Invoke();
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
}
