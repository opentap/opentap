using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
namespace OpenTap.Expressions
{
    class ExpressionManager
    {
        class ExpressionObject
        {
            public string Member { get; set; }
            public string Expression
            {
                get => expression;
                set
                {
                    Lambda = null;
                    expression = value;
                }
            }

            public Delegate Lambda;
            string expression;
            public IMemberData MemberData;
            public string[] UsedParameters;
            public int ParameterIndex;
        }
        class ExpressionList : List<ExpressionObject>
        {
            public IMemberData[] members;
            public object[] buffer;

            public bool TryGetValue(string memberName, out ExpressionObject o)
            {
                o = this.FirstOrDefault(item => item.Member == memberName);
                return o != null;
            }

            public bool Remove(string name)
            {
                var o = this.FirstOrDefault(item => item.Member == name);
                return Remove(o);
            }

            public ExpressionObject this[string index]
            {
                get
                {
                    if (TryGetValue(index, out var o))
                    {
                        return o;
                    }
                    throw new IndexOutOfRangeException();
                }
                set
                {
                    if (value == null)
                        throw new ArgumentNullException();
                    if (TryGetValue(index, out var o))
                    {
                        o.Expression = value.Expression;
                    }
                    else
                    {
                        Add(value);
                    }
                }
            }
        }
        
        static readonly ExpressionCodeBuilder builder = new ExpressionCodeBuilder();
        internal static readonly DynamicMember ExpressionsMember = new DynamicMember
        {
            Name = "OpenTap.Expressions", 
            TypeDescriptor = TypeData.FromType(typeof(ExpressionList)),
            DeclaringType = TypeData.FromType((typeof(ExpressionManager))),
            Readable = true,
            Writable = true,
            Attributes = new object[]
            {
                new DefaultValueAttribute(null),
                new BrowsableAttribute(false)
            }
        };
          
        public static string GetExpression(ITestStepParent step, IMemberData member)
        {
            if (ExpressionsMember.GetValue(step) is ExpressionList lookup)
            {
                if (lookup.TryGetValue(member.Name, out var expr))
                {
                    return expr.Expression;
                }
            }
            return null;
        }
        public static void SetExpression(ITestStepParent step, IMemberData member, string expression)
        {
            if (member == null)
                throw new ArgumentNullException(nameof(member));
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            
            var lookup = ExpressionsMember.GetValue(step) as ExpressionList;
            if (lookup == null)
            {
                lookup = new ExpressionList();
                ExpressionsMember.SetValue(step, lookup);
            }
            if (string.IsNullOrEmpty(expression))
            {
                lookup.Remove(member.Name);
                return;
            }

            if (!lookup.TryGetValue(member.Name, out var expr))
            {
                expr = new ExpressionObject
                {
                    Member = member.Name,
                    Expression = ""
                };
                lookup[member.Name] = expr;
            }
            expr.Expression = expression;
        }

        public static void Update(ITestStepParent step)
        {
            var expressions = ExpressionsMember.GetValue(step) as ExpressionList;
            if (expressions == null || expressions.Count == 0) return;
            var td = TypeData.GetTypeData(step);
            
            builder.UpdateParameterMembers(step, ref expressions.members, out bool updated);
            if (updated || expressions.Any(x => x.Lambda == null))
            {
                ParameterExpression[] parameters = builder.GetParameters(step);;
                expressions.buffer = new object[expressions.members.Length];
                foreach (var expression in expressions)
                {
                    var mem = td.GetMember(expression.Member);
                    Debug.Assert(mem != null);
                    expression.MemberData = mem;
                    ReadOnlySpan<char> code = expression.Expression.ToArray();
                    builder.UsedParameters.Clear();
                    if (mem.TypeDescriptor.DescendsTo(typeof(string)))
                    {
                        var ast = builder.ParseStringInterpolation(ref code);
                        var lambda = builder.GenerateLambda(ast, parameters, mem.TypeDescriptor.AsTypeData().Type);
                        expression.Lambda = lambda;
                    }
                    else
                    {
                        var ast = builder.Parse(ref code);
                        var lambda = builder.GenerateLambda(ast, parameters, mem.TypeDescriptor.AsTypeData().Type);
                        expression.Lambda = lambda;
                    }
                    expression.UsedParameters = builder.UsedParameters.ToArray();
                    expression.ParameterIndex = parameters.IndexWhen(x => x.Name == expression.Member);
                    Debug.Assert(expression.ParameterIndex != -1);
                }
                
                // Sort the expressions so that expressions can depend on the result of other members.
                expressions.Sort((x,y) => x.UsedParameters.Contains(y.Member) ? 1 : -1);
            }
            for (int i = 0; i < expressions.members.Length; i++)
            {
                expressions.buffer[i] = expressions.members[i].GetValue(step);
            }

            foreach (var expression in expressions)
            {
                try
                {
                    var result = expression.Lambda.DynamicInvoke(expressions.buffer);
                    expression.MemberData.SetValue(step, result);
                    if (expression.ParameterIndex != -1)
                    {
                        expressions.buffer[expression.ParameterIndex] = result;
                    }
                }
                catch (Exception e)
                {
                    throw new Exception($"Unable to update member {expression.Member} of {step}", e);
                }
            }
        }

        public static void UpdateVerdicts(ITestStep step, IList<IMemberData> verdictMembers)
        {
            var members = builder.GetMembers(step);
            var parameters = builder.GetParameters(step);
            var parameterValues = members.Select(x => x.GetValue(step)).ToArray();
            foreach (var verdictMember in verdictMembers)
            {
                foreach (var attr in verdictMember.GetAttributes<VerdictAttribute>())
                {
                    var expr = attr.Expression;
                    var ast = builder.Parse(expr);
                    var lambda = builder.GenerateLambda(ast, parameters, typeof(bool));
                    object result = lambda.DynamicInvoke(parameterValues);
                    if (result is bool t)
                    {
                        if(t)
                            step.UpgradeVerdict(attr.Verdict);
                    }
                    else
                    {
                        throw new Exception("Unsupported return type from verdict attribute");
                    }
                }
            }
        }

        public static ValidationRule[] GetValidationRules(object step)
        {
            List<ValidationRule> rules = null;
            
            var members2 = TypeData.GetTypeData(step)
                .GetMembers()
                .Where(x => x.Readable && x.IsBrowsable());

            foreach (var member in members2)
            {
                foreach (var attr in member.GetAttributes<ValidationAttribute>())
                {
                    rules ??= new List<ValidationRule>();
                    
                    var expression = attr.Expression;
                    var ast = builder.Parse(expression);
                    var builder2 = new ExpressionCodeBuilder();
                    var members = members2.ToArray();
                    var lambda = builder2.GenerateLambdaCompact(ast, ref members, typeof(bool));
                    
                    rules.Add(new ValidationRule(() =>
                    {
                        var buffer2 = members.Select(mem => mem.GetValue(step)).ToArray();
                        return (bool)lambda.DynamicInvoke(buffer2);
                    }, string.IsNullOrEmpty(attr.Message) ? attr.Expression : attr.Message, member.Name));
                }
            }

            return rules?.ToArray() ?? Array.Empty<ValidationRule>();
        } 
        
    }
}
