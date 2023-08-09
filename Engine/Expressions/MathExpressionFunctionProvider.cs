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
            public static double sign(double v) => Math.Sign(v);
            public static double max(double a, double b) => Math.Max(a,b);
            public static double max(double a, double b, double c) => Math.Max(Math.Max(a,b), c);
            public static double max(double a, double b, double c, double d) => Math.Max(Math.Max(Math.Max(a,b), c), d);
            public static double min(double a, double b) => Math.Min(a,b);
            public static double min(double a, double b, double c) => Math.Min(Math.Min(a,b), c);
            public static double min(double a, double b, double c, double d) => Math.Min(Math.Min(Math.Min(a,b), c), d);

            public static double log2(double x) => Math.Log(x, 2.0);
            public static double log10(double x) => Math.Log10(x);
            public static double log(double x, double @base) => Math.Log(x, @base);
            public static double exp(double x) => Math.Exp(x);
            
            
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
