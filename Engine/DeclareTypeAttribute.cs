using System;

namespace OpenTap
{
    
    /// <summary>
    /// Declares a type. If it has not been otherwise declared, it is registered so that data of that type can be serialized.
    /// This can be convenient when working with down-casted types, which are private, generic or nested.
    /// Note, in most instances, it is _not_ necessary to declare a type!
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class DeclareTypeAttribute : Attribute
    {
        /// <summary> The declared type. </summary>
        public readonly Type DeclaredType;
        
        /// <summary> Creates an instance of DeclaredTypeAttribute. </summary>
        public DeclareTypeAttribute(Type declaredType) => DeclaredType = declaredType;
    }
}