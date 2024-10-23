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

        /// <summary> Creates an object by calling thet target member. </summary>
        /// <param name="ownerObj"></param>
        /// <param name="factoryAttribute"></param>
        /// <returns></returns>
        internal static object Create(object ownerObj, IFactoryAttribute factoryAttribute)
        {
            var type = ownerObj.GetType();
            return type.GetMethod(factoryAttribute.FactoryMethodName, BindingFlags.Instance |BindingFlags.Public | BindingFlags.NonPublic).Invoke(ownerObj, Array.Empty<object>());
        }
    }

    /// <summary> Specifies that another member method can be used to create an element value for a given list property.</summary>
    public class ElementFactoryAttribute : Attribute, IFactoryAttribute
    {
        /// <summary> The name of the method that can be used to create a value. This can be a private method.</summary>
        public string FactoryMethodName { get; }

        /// <summary>
        /// Creates an instance of ElementFactoryAttribute
        /// </summary>
        /// <param name="factoryMethodName"></param>
        public ElementFactoryAttribute(string factoryMethodName)
        {
            FactoryMethodName = factoryMethodName;
        }
    }
}
