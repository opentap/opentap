using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
namespace OpenTap.Expressions
{
    
    /// <summary> Immutable class that contains information about parameters. </summary>
    class ParameterData
    {
        
        public ImmutableDictionary<string, ParameterExpression> Lookup { get; }
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
            var members = GetParameterMembers(obj);
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
        
        internal static ImmutableArray<IMemberData> GetParameterMembers(object obj)
        {
            var members = ImmutableArray<IMemberData>.Empty;
            UpdateParameterMembers(obj, ref members);
            return members;
        }
        
        /// <summary>
        /// Updates array and returns true if the list compared to the referenced array.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="array"></param>
        /// <returns>True if the list has been updated. </returns>
        internal static bool UpdateParameterMembers(object obj, ref ImmutableArray<IMemberData> array)
        {
            var newArray = TypeData.GetTypeData(obj).GetMembers()
                .Where(mem => mem.Readable && mem.IsBrowsable() && (mem.HasAttribute<SettingsIgnoreAttribute>() == false) && (mem.HasAttribute<AnnotationIgnoreAttribute>() == false))
                .ToImmutableArray();
            if (array != null && newArray.SequenceEqual(array))
                return false;
            array = newArray;
            return true;
        }
        
        /// <summary> Finds all the used symbol of an abstract syntax tree. </summary>
        public ImmutableHashSet<string> GetUsedParameters(AstNode ast)
        {
            switch (ast)
            {
                case ObjectNode obj:
                    if (!obj.IsLiteralString && Lookup.TryGetValue(obj.Content, out var expr))
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
