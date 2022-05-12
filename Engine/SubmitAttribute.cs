using System;

namespace OpenTap
{
    /// <summary> Specifies that a property finalizes input.</summary>
    public class SubmitAttribute : Attribute
    {
        /// <summary>  References a method to be called when the changes are been submitted. This maybe be null. This should be a parameterless method that may optionally return a Task (async method).</summary>
        public string CallbackMethodName { get; }
        /// <summary> Creates a new instance of SubmitAttribute.</summary>
        public SubmitAttribute()
        {
            
        }

        /// <summary> Creates an instance of SubmitAttribute which names a callback method to call when the user input is submitted.</summary>
        public SubmitAttribute(string callback)
        {
            CallbackMethodName = callback;
        }
    }
}