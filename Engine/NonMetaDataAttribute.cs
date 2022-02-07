using System;

namespace OpenTap
{
    /// <summary>
    /// Specifies that a property is does not constitute meta data for test plan runs. 
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class NonMetaDataAttribute : Attribute
    {
        
    }
}