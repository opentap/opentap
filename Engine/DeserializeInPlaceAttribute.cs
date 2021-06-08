using System;

namespace OpenTap
{
    /// <summary>
    /// This attribute marks that an object should not be overwritten during serialization, but instead the
    /// value existing in the property should be used.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DeserializeInPlaceAttribute : Attribute
    {
        
    }
}