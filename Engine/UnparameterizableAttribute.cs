using System;

namespace OpenTap
{
    /// <summary> Marks on a property that it cannot be parameterized. </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class UnparameterizableAttribute : Attribute { }
}