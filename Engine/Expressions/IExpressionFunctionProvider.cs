using System;
using System.Reflection;
namespace OpenTap.Expressions
{
    public interface IExpressionFunctionProvider
    {
        MethodInfo GetMethod(string name, Type[] argumentTypes);
        PropertyInfo GetProperty(string name);
        string[] GetCompletions(string name);
    }

}
