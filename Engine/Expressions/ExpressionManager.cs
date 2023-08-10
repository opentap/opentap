﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
namespace OpenTap.Expressions
{
    /// <summary>  Manages expressions for settings.  </summary>
    public static class ExpressionManager
    {
        internal class ExpressionObject
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
        
        /// <summary>
        /// A list of expressions that is associated with an object (usually a test step).
        /// </summary>
        internal class ExpressionList : List<ExpressionObject>
        {
            /// <summary> Keeps track of the members of the object to which these expressions applies. </summary>
            public ImmutableArray<IMemberData> Members;
            
            /// <summary> This is used to store the arguments for when invoking the delegates. They always take the same arguments, so this is very efficient.</summary>
            public object[] ArgumentBuffer;

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
        
        static ExpressionCodeBuilder builderCache = new ExpressionCodeBuilder();
        static ExpressionCodeBuilder builder => (builderCache = builderCache.Update());
        
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
            if (expression == null)
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
            builder.UpdateParameterMembers(step, ref expressions.Members, out bool updated);
            if (updated)
            {
                foreach (var item in expressions)
                {
                    item.Lambda = null;
                }
            }
        }

        internal static ExpressionList GetExpressionList(object step, bool create)
        {
            var expressions = ExpressionsMember.GetValue(step) as ExpressionList;
            if(create && expressions == null)
            {
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
                expressions.ArgumentBuffer = new object[expressions.Members.Length + 1];
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

                    var result = ast.Then(node =>
                    {
                        expression.UsedParameters = parameters.GetUsedParameters(node);
                        return builder2.GenerateLambda(node, parameters, mem.TypeDescriptor.AsTypeData().Type);
                    });
                    if (result.Ok)
                    {
                        expression.Lambda = result.Value;
                    }
                    else
                    {
                        // this error is already shown in the GUI through other ways, so it can be ignored.
                    }
                     
                    expression.ParameterIndex = parameters.Parameters.IndexWhen(x => x.Name == expression.Member);
                    Debug.Assert(expression.ParameterIndex != -1);
                }
                
                // Sort the expressions so that expressions can depend on the result of other members.
                expressions.Sort((x,y) => x.UsedParameters.Contains(y.Member) ? 1 : -1);
            }
            expressions.ArgumentBuffer[0] = step;
            for (int i = 0; i < expressions.Members.Length; i++)
            {
                expressions.ArgumentBuffer[i + 1] = expressions.Members[i].GetValue(step);
            }

            foreach (var expression in expressions)
            {
                try
                {
                    if (member != null && expression.MemberData != member) 
                        continue;
                    
                    if (expression.Lambda == null) 
                        continue; // this may occur if the code was invalid.
                    
                    var result = expression.Lambda.DynamicInvoke(expressions.ArgumentBuffer);
                    expression.MemberData.SetValue(step, result);
                    if (expression.ParameterIndex != -1)
                    {
                        expressions.ArgumentBuffer[expression.ParameterIndex] = result;
                    }
                }
                catch (Exception)
                {
                    // consider handling this exception.
                }
            }
        }

        internal static string ExpressionError(string expression, object targetObject, ITypeData targetType)
        {
            try
            {
                var parameters = ParameterData.GetParameters(targetObject) ?? ParameterData.Empty;
                Result<Expression> result; 
                if (targetType.DescendsTo(typeof(string)))
                {
                    result = builder.ParseStringInterpolation(expression).Then(expr => builder.GenerateExpression(expr, parameters, targetType.AsTypeData().Type));
                }
                else
                {
                    result = builder.Parse(expression).Then(expr => builder.GenerateExpression(expr, parameters, targetType.AsTypeData().Type));
                }
                if (result.Ok)
                    return null;
                
                return result.Error;
            }
            catch(Exception e)
            {
                return e.Message;
            }
        }
        internal static bool HasExpression(ITestStepParent step)
        {
            var expressions = GetExpressionList(step, false);
            if (expressions == null || expressions.Count == 0) return false;
            return true;
        }
    }
}