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
            public static bool isempty(string str) => string.IsNullOrEmpty(str);
            public static Verdict fail(bool check) => check ? Verdict.Fail : Verdict.NotSet;
            public static Verdict inconclusive(bool check) => check ? Verdict.Inconclusive : Verdict.NotSet;
            public static Verdict pass(bool check) => check ? Verdict.Pass : Verdict.NotSet;
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

            public static object Input(ITestStepParent step, string stepName, string memberName)
            {
                ITestStepParent targetStep = null;
                while (targetStep == null && step is ITestStep step2)
                {
                    var end = step2.Parent.ChildTestSteps.IndexOf(step2) ;
                    for (int i = end - 1; i >= 0 ; i--)
                    {
                        if (step2.Parent.ChildTestSteps[i].Name == stepName)
                        {
                            targetStep = step2.Parent.ChildTestSteps[i];
                            break;
                        }
                    }
                    step = step2.Parent;
                }

                if (targetStep == null)
                    throw new Exception("Unable to find target step: " + stepName);

                var member = TypeData.GetTypeData(targetStep).GetMember(memberName);
                if (member == null)
                {
                    member = TypeData.GetTypeData(targetStep).GetMembers().FirstOrDefault(member => member.GetAttribute<DisplayAttribute>()?.Name == memberName);
                    if(member == null)
                        throw new Exception("Unable to find member on target step: " + memberName);
                }
                return member.GetValue(targetStep);
            } 
            

            public static double Pi => π;
            public static double π => Math.PI;

            public bool @false => false;
            public bool @true => true;
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
