using System;

namespace OpenTap
{
    /// <summary> Marks a property on a test step that cannot be merged with another property from another step..</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class UnmergableAttribute : Attribute { }
}