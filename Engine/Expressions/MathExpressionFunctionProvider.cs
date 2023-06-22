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
            public static double sin(double phase) => Math.Sin(phase);
            public static double cos(double phase) => Math.Cos(phase);
            public static double tan(double phase) => Math.Tan(phase);
            public static double atan(double phase) => Math.Atan(phase);
            
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
                        return match.Groups[0].Value;
                    }
                }
                return "";
            }

            public static object Input(ITestStepParent step, string guid, string name)
            {
                var step2 = step.ChildTestSteps.GetStep(Guid.Parse(guid));
                var member = TypeData.GetTypeData(step2).GetMember(name);
                return member.GetValue(step2);
            } 
            

            public static double Pi => π;
            public static double π => Math.PI;
        }
        
        static MethodInfo[] methods;
        static readonly MethodInfo[] extraMethods;
        static readonly PropertyInfo[] extraProperties;
        
        static MathExpressionFunctionProvider()
        {
            methods = typeof(Math).GetMethods(BindingFlags.Static | BindingFlags.Public);
            extraMethods = typeof(Extra).GetMethods(BindingFlags.Static | BindingFlags.Public);
            extraProperties = typeof(Extra).GetProperties(BindingFlags.Static | BindingFlags.Public);
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
        public PropertyInfo GetProperty(string name)
        {
            return extraProperties.FirstOrDefault(prop => prop.Name == name);
        }
        public string[] GetCompletions(string name)
        {
            return methods.Select(x => x.Name)
                .Where(name2 => name2.StartsWith(name))
                .ToArray();
        }
    }
}
