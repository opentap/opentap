using System;
namespace OpenTap
{
    /// <summary> Limits the number of types available when selecting resources. The required type is usually an interface. This makes it possible to specify that a setting has to satisfy multiple interfaces. </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class TypeFilterAttribute : Attribute
    {
        /// <summary> Creates an instance of the type filter attribute </summary>
        public TypeFilterAttribute(Type requiredType)
        {
            RequiredType = requiredType;
        }
        
        /// <summary> Specifies the required type. </summary>
        public Type RequiredType { get; }
    }
}
