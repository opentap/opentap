using System;
namespace OpenTap
{
    /// <summary>
    /// Ordering constraint for plugin types. This should be used 'before' something else.
    /// This is currently only support by implementations of IStringConvertProvider.
    /// If more than one of these attributes are used, it will try to find a type before all of them.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class BeforeAttribute: Attribute
    {
        readonly ITypeData type;
        /// <summary>
        ///  Creates a BeforeAttribute from a type parameter. This is the safest way, but requires a public C# type.
        /// </summary>
        public BeforeAttribute(Type type)
        {
            this.type = TypeData.FromType(type);
        }
        /// <summary>
        ///  Creates a BeforeAttribute from a string type name..
        /// </summary>
        /// <param name="typeName"></param>
        public BeforeAttribute(string typeName)
        {
            this.type = TypeData.GetTypeData(typeName);
        }

        /// <summary>
        /// returns true if the marked type should come before the 'other' type.
        /// This can method can be overridden for more custom 'before' behavior.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public virtual bool Before(ITypeData other)
        {
            if (type != null && Equals(type, other))
                return true;
            return false;
        } 
    }
}
