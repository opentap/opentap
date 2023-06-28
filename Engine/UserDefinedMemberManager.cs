using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace OpenTap
{
    class UserDefinedMemberManager
    {

        public static Attribute[] CompileAttributes(string attributeCode)
        {
            if (string.IsNullOrWhiteSpace(attributeCode))
                return Array.Empty<Attribute>();
            var attributes = new List<Attribute>();
            { 
                var ast = CSharpSyntaxTree.ParseText(attributeCode);
                var root = ast.GetRoot();
                var childNodes = root.ChildNodes()
                    .OfType<IncompleteMemberSyntax>()
                    .SelectMany(x => x.AttributeLists)
                    .SelectMany(x => x.Attributes)
                    .ToArray();
                foreach (var node in childNodes)
                {
                    var name = node.Name.ToString();
                    var openTapName = $"OpenTap.{name}Attribute";
                    var attributeType = TypeData.GetTypeData(openTapName);
                    if (attributeType == null)
                        throw new Exception("Unable to find attribute " + openTapName);
                    var ctors = attributeType.AsTypeData().Type.GetConstructors();
                    foreach (var ctor in ctors)
                    {
                        var parameters = ctor.GetParameters();
                        var normalArgs = node.ArgumentList?.Arguments.ToArray() ?? Array.Empty<AttributeArgumentSyntax>();
                        var exprs = normalArgs.Select(x =>
                            {
                                if (x.Expression is ExpressionSyntax e)
                                {
                                    if (e is LiteralExpressionSyntax le)
                                    {
                                        return le.Token.Value;
                                    }
                                }
                                
                                return null;
                            }
                        ).ToArray();
                        if (parameters.Count(x => false == (x.IsOptional|| x.CustomAttributes.OfType<ParamArrayAttribute>().Any())) <= exprs.Length)
                        {
                            var args = new object[parameters.Count()];
                            for (int i = 0; i < parameters.Count(); i++)
                            {
                                if (exprs.Length > i)
                                    args[i] = exprs[i];
                                else
                                    args[i] = parameters[i].DefaultValue;
                            }
                            var attr = (Attribute)ctor.Invoke(args);
                            attributes.Add(attr);
                            break;
                        }


                    }
                }
                
            }
            return attributes.ToArray();
        }
        
    }
}
