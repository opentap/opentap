using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace OpenTap.Expressions
{
    class MathExpressionFunctionProvider : IExpressionFunctionProvider
    {
        class Extra
        {
            public static bool empty(string str) => string.IsNullOrEmpty(str);
            public static double sin(double v) => Math.Sin(v);
            public static double cos(double v) => Math.Cos(v);
            public static double tan(double v) => Math.Tan(v);
            public static double atan(double v) => Math.Atan(v);
            public static double abs(double v) => Math.Abs(v);
            public static double floor(double v) => Math.Floor(v);
            public static double ceiling(double v) => Math.Ceiling(v);
            public static double round(double v) => Math.Round(v);
            public static double round(double v, int decimals) => Math.Round(v, decimals);
            public static double Pi => π;
            public static double π => Math.PI;
            public static bool @false => false;
            public static bool @true => true;
        }
        
        static readonly MethodInfo[] extraMethods;
        static readonly PropertyInfo[] extraProperties;
        
        static MathExpressionFunctionProvider()
        {
            extraMethods = typeof(Extra).GetMethods(BindingFlags.Static | BindingFlags.Public);
            extraProperties = typeof(Extra).GetProperties(BindingFlags.Static | BindingFlags.Public);
        }

        public IEnumerable<MemberInfo> GetMembers() => extraMethods.Concat<MemberInfo>(extraProperties);
    }
}
