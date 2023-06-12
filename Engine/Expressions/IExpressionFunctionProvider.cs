using System;
using System.Reflection;
namespace OpenTap.Expressions
{
    public interface IExpressionFunctionProvider
    {
        MethodInfo GetMethod(string name, Type[] argumentTypes);
        string[] GetCompletions(string name);
    }

}
