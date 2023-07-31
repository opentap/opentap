using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OpenTap
{
    /// <summary> Class describing a user defined member. </summary>
    public class UserDefinedDynamicMember : DynamicMember
    {
        IEnumerable<object> attributes = Array.Empty<object>();
        /// <summary> User defined attributes. </summary>
        [XmlIgnore]
        public override IEnumerable<object> Attributes
        {
            get => attributes;
            set => attributes = value;
        }

        /// <summary> Return user defined members for an object.  </summary>
        public static UserDefinedDynamicMember[] GetUserDefinedMembers(object obj) => 
            TypeData.GetTypeData(obj).GetMembers().OfType<UserDefinedDynamicMember>().ToArray();

        /// <summary> Compare this to another UserDefinedDynamicMember. </summary>
        public override bool Equals(object obj)
        {
            if (obj is UserDefinedDynamicMember other)
                return other.Name == Name 
                       && Equals(other.TypeDescriptor, TypeDescriptor) 
                       && Equals(other.DeclaringType, DeclaringType);
            return false;
        }

        internal string internalAttributesCode;
        /// <summary>
        /// This contains the code for reconstructing the attributes. e.g [Unit("Hz")]
        /// </summary>
        [XmlElement("Attributes")]
        public string AttributesCode
        {
            get => internalAttributesCode;
            set
            {
                attributes = UserDefinedMemberManager.CompileAttributes(value);
                internalAttributesCode = value;
            }

        }

        /// <summary> Calculate a hash code. </summary>
        public override int GetHashCode()
        {
            var a = Name.GetHashCode() * 37219321;
            var b = TypeDescriptor?.GetHashCode() ?? 0 + 7565433;
            var c = DeclaringType?.GetHashCode() ?? 0 + 180374830;
            return ((a * 732013 + b) * 3073212 + c * 32103212);
        }
    }
    
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