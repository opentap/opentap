using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
namespace OpenTap.Expressions
{
    class MathExpressionFunctionProvider : IExpressionFunctionProvider
    {
        class Extra
        {
            public static string Match(string pattern, string group, string value)
            {
                var regex = new Regex(pattern);
                var names = regex.GetGroupNames();
                var matches = regex.Matches(value);
                foreach (Match match in matches)
                {
                    if (match.Success)
                    {
                        for (int i = 0; i < names.Length; i++)
                        {
                            if (names[i] == group)
                            {
                                return match.Groups[i].Value;
                            }
                        }
                    }
                }
                return "";
            }
            public static string Match(string pattern, string value)
            {
                var regex = new Regex(pattern);
                var names = regex.GetGroupNames();
                var matches = regex.Matches(value);
                foreach (Match match in matches)
                {
                    if (match.Success)
                    {
                        for (int i = 0; i < names.Length; i++)
                        {
                            return match.Groups[i].Value;
                        }
                    }
                }
                return "";
            }
        }
        
        static MethodInfo[] methods;
        static readonly MethodInfo[] extraMethods;
        static MathExpressionFunctionProvider()
        {
            methods = typeof(Math).GetMethods(BindingFlags.Static | BindingFlags.Public);
            extraMethods = typeof(Extra).GetMethods(BindingFlags.Static | BindingFlags.Public);
        }



        public MethodInfo GetMethod(string name, Type[] argumentTypes)
        {
            var ex = extraMethods.FirstOrDefault(m => m.Name == name && m.GetParameters().Length == argumentTypes.Length);
            if (ex != null) return ex;
            if (name.StartsWith("Math.") == false) return null;
            name = name.Substring("Math.".Length);
            var candidates = methods
                .Where(x => x.ReturnParameter?.ParameterType != typeof(decimal))
                .Where(x => x.Name == name && x.GetParameters().Length == argumentTypes.Length);
            

            return candidates.FirstOrDefault() ;
        }
        public string[] GetCompletions(string name)
        {
            return methods.Select(x => x.Name)
                .Where(name2 => name2.StartsWith(name))
                .ToArray();
        }
    }
}
