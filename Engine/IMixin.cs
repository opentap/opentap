using System;
namespace OpenTap
{
    /// <summary>
    /// Mixins can be used with EmbedPropertiesAttribute to give extra functionality to a test step.
    /// </summary>
    public interface IMixin : ITapPlugin
    {
        
    }

    /// <summary> Event args for ITestStepPreRun mixin. </summary>
    public class TestStepPreRunEventArgs : EventArgs
    {
        /// <summary> The step for which the event happens. </summary>
        public ITestStep TestStep { get; }
        /// <summary> Can be set to true to skip the step</summary>
        public bool SkipStep { get; set; }
        
        internal TestStepPreRunEventArgs(ITestStep step) => TestStep = step;
    }

    /// <summary> Event args for ITestStepPostRun mixin. </summary>
    public class TestStepPostRunEventArgs : EventArgs
    {
        /// <summary> The step for which the event happens. </summary>
        public ITestStep TestStep { get; }
        
        internal TestStepPostRunEventArgs(ITestStep step) => TestStep = step;
    }

    /// <summary> Event args for IResourcePreOpenMixin mixin. </summary>
    public class ResourcePreOpenEventArgs : EventArgs
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
}
