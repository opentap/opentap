namespace OpenTap
{
    /// <summary> This type of mixin gives access to process an output just before it gets assigned. </summary>
    public interface IAssignOutputMixin : IMixin
    {
        /// <summary>  Invoked just before the output is assigned. </summary>
        void OnAssigningOutput(AssignOutputEventArgs args);
    }
    
    class AssignOutputEvent: MixinEvent<IAssignOutputMixin>
    {
        public static object Invoke(ITestStepParent step, object value, IMemberData member) => 
            Invoke(step, (v, args) => v.OnAssigningOutput(args), new AssignOutputEventArgs(step, value, member)).Value;
    }
    
    /// <summary> Event args for ITestStepPostRun mixin. </summary>
    public sealed class AssignOutputEventArgs
    {
        /// <summary> The step for which the event happens. </summary>
        public ITestStepParent TestStep { get; }
        
        /// <summary> The member that the output is assigned to. </summary>
        public IMemberData Member { get; }
        
        /// <summary> The value that is going to be assigned. This can be overwritten. </summary>
        public object Value { get; set; }
        
        internal AssignOutputEventArgs(ITestStepParent step, object value, IMemberData member)  => (TestStep, Value, Member) = (step, value, member);
    }
}
