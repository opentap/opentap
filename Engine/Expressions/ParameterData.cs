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
        
        internal static IMemberData[] GetMembers(object obj)
        {
            IMemberData[] members = new IMemberData[10];
            GetMembers(obj, ref members);

            return members;
        }
        public static bool GetMembers(object obj, ref IMemberData[] array)
        {
            if (array == null)
            {
                array = GetMembers(obj).ToArray();
                return true;
            }
            int i = 0;
            bool changed = false;
            foreach (var mem in TypeData.GetTypeData(obj).GetMembers())
            {
                if (mem.Readable && mem.IsBrowsable() && (mem.HasAttribute<SettingsIgnoreAttribute>() == false))
                {
                    if (array.Length <= i)
                    {
                        Array.Resize(ref array, i + 1);
                    }
                    if (array[i] != mem)
                    {
                        array[i] = mem;
                        changed = true;
                    }
                    i++;
                }
            }
            if (array.Length > i)
            {
                Array.Resize(ref array, i);
                changed = true;
            }
            return changed;
        }

    }
}
