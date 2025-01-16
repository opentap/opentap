using System;
using System.Reflection;
namespace OpenTap
{
    /// <summary>
    /// Marks a factory attribute, for now this can be internal.
    /// </summary>
    interface IFactoryAttribute
    {
        public string FactoryMethodName { get; }
    }

    /// <summary> Specifies that another member method can be used to create a value for a given property.</summary>
    public class FactoryAttribute : Attribute, IFactoryAttribute
    {
        /// <summary> The name of the method that can be used to create a value. This can be a private method.</summary>
        public string FactoryMethodName { get; }
        
        /// <summary> Creates an instance of FactoryAttribute. </summary>
        /// <param name="factoryMethodName"></param>
        public FactoryAttribute(string factoryMethodName)
        {
            FactoryMethodName = factoryMethodName;
        }

        /// <summary> Creates an object by calling that target member. </summary>
        internal static object Create(object ownerObj, IFactoryAttribute factoryAttribute)
        {
            var type = ownerObj.GetType();
            var fac = type.GetMethod(factoryAttribute.FactoryMethodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            // This is a bug, but showing this message in the log is much nicer than a null reference exception.
            if (fac == null)
                throw new Exception(
                    $"Factory method '{factoryAttribute.FactoryMethodName}' not found on object of type '{type.FullName}'");
            return fac.Invoke(ownerObj, []);
        }

        // Recursively search for the source of the parameter to try constructing this object
        internal static bool TryCreateFromMember(IParameterMemberData member, IFactoryAttribute factoryAttribute, out object obj)
        {
            obj = null;
            if (member is IParameterMemberData pmd)
            {
                foreach (var p in pmd.ParameterizedMembers)
                {
                    // If this is a concrete member, we should be able to use the source object as a factory source.
                    if (p.Member is MemberData)
                    {
                        obj = Create(p.Source, factoryAttribute);
                        return true;
                    }

                    // Otherwise, try recursing further
                    if (p.Member is IParameterMemberData pmd2)
                    {
                        if (TryCreateFromMember(pmd2, factoryAttribute, out obj))
                            return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary> Specifies that another member method can be used to create an element value for a given list property.</summary>
    public class ElementFactoryAttribute : Attribute, IFactoryAttribute
    {
        /// <summary> The name of the method that can be used to create a value. This can be a private method.</summary>
        public string FactoryMethodName { get; }

        /// <summary> Creates an instance of ElementFactoryAttribute. </summary>
        public ElementFactoryAttribute(string factoryMethodName)
        {
            FactoryMethodName = factoryMethodName;
        }
    }
}
