using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
namespace OpenTap.Expressions
{
    class ParameterData
    {
        public ImmutableDictionary<string,ParameterExpression> Lookup { get; }
        public ImmutableArray<ParameterExpression> Parameters { get; }
        public ParameterData(ImmutableDictionary<string,ParameterExpression> lookup, ImmutableArray<ParameterExpression> parameters)
        {
            Lookup = lookup;
            Parameters = parameters;
        }

        public ParameterData Add(ParameterExpression expr, int at = -1) => new ParameterData(Lookup.Add(expr.Name, expr), Parameters.Insert(at == -1 ? Parameters.Length : at, expr));
        public ParameterData AddThis() => Add(Expression.Parameter(typeof(ITestStep), "__this__"), 0);
            
        public static ParameterData Empty { get; } = new ParameterData(ImmutableDictionary<string,ParameterExpression>.Empty, ImmutableArray<ParameterExpression>.Empty);
            
        public static ParameterData GetParameters(object obj)
        {
            var members = GetMembers(obj);
            var parameters = new List<ParameterExpression>();
            var lookup = new Dictionary<string, ParameterExpression>();
            foreach (var member in members)
            {
                var expr = Expression.Parameter(member.TypeDescriptor.AsTypeData().Type, member.Name);
                parameters.Add(expr);
                var display = member.GetAttribute<DisplayAttribute>();
                lookup[member.Name] = expr;
                if (display != null)
                {
                    lookup[display.Name] = expr;
                    lookup[display.GetFullName()] = expr;
                }
            }
            
            return new ParameterData(lookup.ToImmutableDictionary(), parameters.ToImmutableArray());
        }            
        
        internal static ImmutableArray<IMemberData> GetMembers(object obj)
        {
            var members = ImmutableArray<IMemberData>.Empty;
            GetMembers(obj, ref members);
            return members;
        }
        public static bool GetMembers(object obj, ref ImmutableArray<IMemberData> array)
        {
            var newArray = TypeData.GetTypeData(obj).GetMembers()
                .Where(mem => mem.Readable && mem.IsBrowsable() && (mem.HasAttribute<SettingsIgnoreAttribute>() == false))
                .ToImmutableArray();
            if (newArray.Equals(array))
                return false;
            array = newArray;
            return true;
        }
        
        public ImmutableHashSet<string> GetUsedParameters(AstNode ast)
        {
            switch (ast)
            {
                case ObjectNode obj:
                    if (Lookup.TryGetValue(obj.Data, out var expr))
                        return ImmutableHashSet<string>.Empty.Add(expr.Name);
                    return ImmutableHashSet<string>.Empty;
                case OperatorNode _:
                    return ImmutableHashSet<string>.Empty;
                case BinaryExpressionNode bin:
                    return GetUsedParameters(bin.Left).Union(GetUsedParameters(bin.Right));
                case null:
                    return ImmutableHashSet<string>.Empty;
                default:
                    throw new InvalidOperationException();
            }
            
            
        }

    }
}
