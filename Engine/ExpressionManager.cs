using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
namespace OpenTap
{
    class ExpressionManager
    {
        class Expression
        {
            public string ExpressionString;

            [XmlIgnore]
            public Delegate Lambda;

            

        }
        static ExpressionCodeBuilder builder = new ExpressionCodeBuilder();
        static UserDefinedDynamicMember expressionsMember = new UserDefinedDynamicMember()
        {
            Name = "OpenTap.Expressions", 
            TypeDescriptor = TypeData.FromType(typeof(Dictionary<string, Expression>)),
            Readable = true,
            Writable = true
        };
          
        public static string GetExpression(ITestStepParent step, IMemberData member)
        {
            var lookup = expressionsMember.GetValue(step) as Dictionary<string, Expression>;
            if (lookup != null)
            {
                if (lookup.TryGetValue(member.Name, out var expr))
                {
                    return expr.ExpressionString;
                }
            }
            return null;
        }
        public static void SetExpression(ITestStepParent step, IMemberData member, string expression)
        {
            var lookup = expressionsMember.GetValue(step) as Dictionary<string, Expression>;
            if (lookup == null)
            {
                lookup = new Dictionary<string, Expression>();
                expressionsMember.SetValue(step, lookup);
            }

            if (!lookup.TryGetValue(member.Name, out var expr))
            {
                expr = new Expression()
                {
                    ExpressionString = ""
                };
                lookup[member.Name] = expr;
            }
            expr.ExpressionString = expression;


        }

        public static void Eval(ITestStepParent step, IMemberData member)
        {
            
        }

        public static void Update(ITestStepParent step)
        {
            var lookup = expressionsMember.GetValue(step) as Dictionary<string, Expression>;
            if (lookup == null) return;
            var td = TypeData.GetTypeData(step);
            var parameters = builder.GetParameters(step);
            foreach (var kw in lookup)
            {
                var mem = td.GetMember(kw.Key);
                Debug.Assert(mem != null);
                ReadOnlySpan<char> code = kw.Value.ExpressionString.ToArray();
                var ast = builder.Parse(ref code);
                
                var lambda = builder.GenerateLambda(ast, parameters);

                kw.Value.Lambda = lambda;


            }
        }
        
    }
}
