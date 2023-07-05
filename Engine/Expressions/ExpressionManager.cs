using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
namespace OpenTap.Expressions
{
    /// <summary>  Manages expressions for settings.  </summary>
    public static class ExpressionManager
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
            public ImmutableHashSet<string> UsedParameters = ImmutableHashSet<string>.Empty;
            public int ParameterIndex;
        }
        class ExpressionList : List<ExpressionObject>
        {
            public ImmutableArray<IMemberData> members;
            public object[] buffer;

            public readonly Dictionary<string, (ImmutableArray<IMemberData>, Delegate)> EnabledIfLookup = new Dictionary<string, (ImmutableArray<IMemberData>, Delegate)>();
            public (Delegate, Verdict)[] UpdateVerdicts
            {
                get;
                set;
            }

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
            DeclaringType = TypeData.FromType(typeof(ExpressionManager)),
            Readable = true,
            Writable = true,
            Attributes = new object[]
            {
                new DefaultValueAttribute(null),
                new BrowsableAttribute(false)
            }
        };
          
        /// <summary>
        /// Gets the current expression assigned to a member.
        /// </summary>
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
        
        /// <summary> Sets an expression on a member. if set to null or white space, the expression is removed. </summary>
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

        static void UpdateExpressions(object step, ExpressionList expressions)
        {
            builder.UpdateParameterMembers(step, ref expressions.members, out bool updated);
            if (updated)
            {
                expressions.UpdateVerdicts = null;
                expressions.EnabledIfLookup.Clear();
                foreach (var item in expressions)
                {
                    item.Lambda = null;
                }
            }
        }

        internal static bool MemberHasExpression(ITypeData type, IMemberData member)
        {
            var exprMember = type.GetMember(ExpressionsMember.Name);
            if (exprMember != null)
                return true;
            return false;
        }

        static ExpressionList GetExpressionList(object step, bool Create)
        {
            var expressions = ExpressionsMember.GetValue(step) as ExpressionList;
            if(Create && expressions == null)
            {
                if (expressions == null)
                    expressions = new ExpressionList();
                UpdateExpressions(step, expressions);    
            }
            else if(expressions != null)
            {
                UpdateExpressions(step, expressions);
            }
            return expressions;
        }

        /// <summary> Updates all expressions on an object. </summary>
        public static void Update(object step) => Update(step, null);
        
        internal static void Update(object step, IMemberData member)
        {
            var expressions = GetExpressionList(step, false);
            if (expressions == null || expressions.Count == 0) return;
            
            UpdateExpressions(step, expressions);
            if (expressions.Any(x => x.Lambda == null))
            {
                var td = TypeData.GetTypeData(step);
                var parameters = ParameterData.GetParameters(step)
                    .AddThis();
                expressions.buffer = new object[expressions.members.Length + 1];
                expressions.RemoveIf(expr => td.GetMember(expr.Member) == null);
                foreach (var expression in expressions)
                {
                    var mem = td.GetMember(expression.Member);
                    var unit2 = mem.GetAttribute<UnitAttribute>();
                    var builder2 = builder;
                    if(unit2 != null)
                        builder2 = builder2.WithNumberFormatter(new NumberFormatter(CultureInfo.CurrentCulture, unit2));

                    Debug.Assert(mem != null);
                    expression.MemberData = mem;
                    ReadOnlySpan<char> code = expression.Expression.ToArray();

                    Result<AstNode> ast;
                    
                    if (mem.TypeDescriptor.DescendsTo(typeof(string)))
                    {
                        ast = builder2.ParseStringInterpolation(ref code);
                    }
                    else
                    {
                        ast = builder2.Parse(ref code);
                    }
                    
                    ast.IfOK(node =>
                        {
                            expression.UsedParameters = parameters.GetUsedParameters(node);
                            return builder2.GenerateLambda(node, parameters, mem.TypeDescriptor.AsTypeData().Type);
                        }).IfOK(lambda => expression.Lambda = lambda);
                     
                    expression.ParameterIndex = parameters.Parameters.IndexWhen(x => x.Name == expression.Member);
                    Debug.Assert(expression.ParameterIndex != -1);
                }
                
                // Sort the expressions so that expressions can depend on the result of other members.
                expressions.Sort((x,y) => x.UsedParameters.Contains(y.Member) ? 1 : -1);
            }
            expressions.buffer[0] = step;
            for (int i = 0; i < expressions.members.Length; i++)
            {
                expressions.buffer[i + 1] = expressions.members[i].GetValue(step);
            }

            foreach (var expression in expressions)
            {
                try
                {
                    if (member != null && expression.MemberData != member) 
                        continue;
                    
                    if (expression.Lambda == null) 
                        continue; // this may occur if the code was invalid.
                    var result = expression.Lambda.DynamicInvoke(expressions.buffer);
                    expression.MemberData.SetValue(step, result);
                    if (expression.ParameterIndex != -1)
                    {
                        expressions.buffer[expression.ParameterIndex] = result;
                    }
                }
                catch (Exception)
                {
                    // consider handling this exception.
                    //throw new Exception($"Unable to update member {expression.Member} of {step}", e);
                }
            }
        }

        internal static void UpdateVerdicts(ITestStep step, IList<IMemberData> verdictMembers)
        {
            var expressions = GetExpressionList(step, true);
            if (expressions.UpdateVerdicts == null)
            {
                var parameters = ParameterData.GetParameters(step);
                
                var updateActions = new List<(Delegate, Verdict)>();
                foreach (var verdictMember in verdictMembers)
                {
                    
                    foreach (var attr in verdictMember.GetAttributes<VerdictAttribute>())
                    {
                        var expr = attr.Expression;
                        var ast = builder.Parse(expr);
                        var lambda = ast.IfOK(ast => builder.GenerateLambda(ast, parameters, typeof(object)));
                        if(lambda.Ok())
                            updateActions.Add((lambda.Unwrap(), attr.Verdict));
                    }
                }
                expressions.UpdateVerdicts = updateActions.ToArray();
            }
            if (expressions.UpdateVerdicts.Length == 0) return;
            var parameterValues = expressions.members.Select(x => x.GetValue(step)).ToArray();

            foreach (var item in expressions.UpdateVerdicts)
            {
                object result = item.Item1.DynamicInvoke(parameterValues);
                if (result is bool t)
                {
                    if(t)
                        step.UpgradeVerdict(item.Item2);
                }
                else if (result is Verdict v)
                {
                    step.UpgradeVerdict(v);
                }
                else
                {
                    throw new Exception("Unsupported return type from verdict attribute");
                }
                
            }
        }

        internal static ValidationRule[] GetValidationRules(object step)
        {
            // this can be slow, because the information is cached in the validation rule object.
            List<ValidationRule> rules = null;
            
            var members = TypeData.GetTypeData(step)
                .GetMembers()
                .Where(x => x.Readable && x.IsBrowsable() && x.HasAttribute<SettingsIgnoreAttribute>() == false)
                .ToImmutableArray();
            var codeBuilder = new ExpressionCodeBuilder();
            foreach (var member in members)
            {
                foreach (var attr in member.GetAttributes<ValidationAttribute>())
                {
                    rules ??= new List<ValidationRule>();
                    
                    var expression = attr.Expression;
                    var ast = builder.Parse(expression);
                    if (!ast.Ok())
                        continue;
                    
                    var membersLocal = members;
                    var lambda = codeBuilder.GenerateLambdaCompact(ast.Unwrap(), ref membersLocal, typeof(bool));

                    CustomErrorDelegateDefinition messageFcn = null;
                    if (!string.IsNullOrWhiteSpace(attr.Message))
                    {
                        var membersLocal2 = members;
                        var messageAst = builder.ParseStringInterpolation(attr.Message);
                        if (messageAst != null)
                        {
                            var fcnr = codeBuilder.GenerateLambdaCompact(messageAst, ref membersLocal2, typeof(string));
                            
                            if (fcnr.Ok())
                            {
                                var fcn = fcnr.Unwrap();
                                if (membersLocal.Equals(membersLocal2))
                                    membersLocal2 = membersLocal;
                                messageFcn = () =>
                                {
                                    var buffer2 = membersLocal2.Select(mem => mem.GetValue(step)).ToArray();
                                    return (string)fcn.DynamicInvoke(buffer2);
                                };
                            }
                            else
                            {
                                lambda = new Func<bool>(() => false);
                            }
                        }
                    }
                    if (messageFcn == null)
                    {
                        var str = string.IsNullOrEmpty(attr.Message) ? attr.Expression : attr.Message;
                        messageFcn = () => str;
                    }
                    if (lambda.Ok() == false)
                    {
                        var err = lambda.Error();
                        lambda = new Func<bool>(() => false);
                         messageFcn = () => err;
                         membersLocal = membersLocal.Clear();
                    }
                    var lambda2 = lambda.Unwrap();

                    rules.Add(new DelegateValidationRule(() =>
                    {
                        var buffer2 = membersLocal.Select(mem => mem.GetValue(step)).ToArray();
                        return (bool)lambda2.DynamicInvoke(buffer2);
                    }, member.Name, messageFcn));
                }
            }

            return rules?.ToArray() ?? Array.Empty<ValidationRule>();
        }

        internal static bool? EvaluateEnabledIf(object step, string expression)
        {
            var expressions = GetExpressionList(step, true);
            
            var builder = new ExpressionCodeBuilder();
            var ast = builder.Parse(expression);
            
            if (ast.Ok() == false) return null;
            
            if (!expressions.EnabledIfLookup.TryGetValue(expression, out var x))
            {
                var members2 = expressions.members;
                var lambda = builder.GenerateLambdaCompact(ast.Unwrap(), ref members2, typeof(bool));
                if (lambda.Ok())
                    expressions.EnabledIfLookup[expression] = x = (members2, lambda.Unwrap());
                else return null;
            }
            var buffer2 = x.Item1.Select(mem => mem.GetValue(step)).ToArray();
            return (bool)x.Item2.DynamicInvoke(buffer2);
        }

        internal static string ExpressionError(string expression, object targetObject, ITypeData targetType)
        {
            try
            {
                var parameters = ParameterData.GetParameters(targetObject) ?? ParameterData.Empty;
                var ast = builder.Parse(expression);
                if (ast.Ok() == false) return ast.Error();
                var expr = builder.GenerateExpression(ast.Unwrap(), parameters, targetType.AsTypeData().Type);
                if (expr.Ok() == false) return expr.Error();
                return null;
            }
            catch(Exception e)
            {
                return e.Message;
            }
        }
    }
}
