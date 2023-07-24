using System;
using System.Reflection;
namespace OpenTap.Expressions
{
    /// <summary>
    /// Provides expression functions.
    /// </summary>
    public interface IExpressionFunctionProvider
    {
        /// <summary>
        /// Returns a method based on name and argument types.
        /// </summary>
        MethodInfo GetMethod(string name, Type[] argumentTypes);
        
        /// <summary> Gets a property based on a name. </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        PropertyInfo GetProperty(string name);
        
        /// <summary>
        /// Returns a list of completions based 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        string[] GetCompletions(string name);
        
    }

}
