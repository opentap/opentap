using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
namespace OpenTap.Expressions
{
    class ExpressionCodeBuilder
    {
        static Result<Expression> Error(string error) =>  Result.Error<Expression>(error);
        
        readonly ImmutableArray<IExpressionFunctionProvider> providers;
        
        public  ImmutableArray<string> KnownSymbols { get; }
        
        readonly NumberFormatter nf;
        
        public Type TargetType { get;  }

        public bool ThrowException { get; }


        public ExpressionCodeBuilder()
        {
            providers = TypeData.GetDerivedTypes<IExpressionFunctionProvider>()
                .Where(x => x.CanCreateInstance)
                .Select(x => x.CreateInstanceSafe())
                .OfType<IExpressionFunctionProvider>()
                .ToImmutableArray();
        }

        ExpressionCodeBuilder(ExpressionCodeBuilder builder)
        {
            providers = builder.providers;
            nf = builder.nf;
            KnownSymbols = builder.KnownSymbols;
            ThrowException = builder.ThrowException;
            TargetType = builder.TargetType;
        }
        
        ExpressionCodeBuilder(ExpressionCodeBuilder builder, Type type): this(builder) => TargetType = type;
        
        ExpressionCodeBuilder(ExpressionCodeBuilder builder, bool throwException): this(builder) => ThrowException = throwException;
        ExpressionCodeBuilder(ExpressionCodeBuilder builder, ImmutableArray<string> knownSymbols): this(builder) => KnownSymbols = knownSymbols;
        ExpressionCodeBuilder(ExpressionCodeBuilder builder, NumberFormatter numberFormatter): this(builder) => nf = numberFormatter;

        public ExpressionCodeBuilder WithTargetType(Type type)
        {
            return new ExpressionCodeBuilder(this, type);
        }
        
        public MethodInfo GetMethod(string name, Type[] types) => providers.FirstNonDefault(p => p.GetMethod(name, types));

        public PropertyInfo GetProperty(string name) => providers.FirstNonDefault(p => p.GetProperty(name));
        
        public ExpressionCodeBuilder WithNumberFormatter(NumberFormatter numberFormatter) => new ExpressionCodeBuilder(this, numberFormatter);
        public ExpressionCodeBuilder WithThrowException(bool throws) => new ExpressionCodeBuilder(this, throws);
       
        public void UpdateParameterMembers(object obj, ref ImmutableArray<IMemberData> members, out bool updated)
        {
            updated = ParameterData.GetMembers(obj, ref members);
        }

        

        public Result<Delegate> GenerateLambda(AstNode ast, ParameterData parameters, Type targetType)
        {
            return GenerateExpression(ast, parameters, targetType)
                .IfOK(expr =>
                {

                    if (expr.Type != targetType)
                    {
                        if (targetType == typeof(string))
                        {
                            expr = Expression.Call(expr, typeof(object).GetMethod("ToString"));
                        }
                        else if (targetType.IsNumeric() && expr.Type == typeof(string))
                        {
                            expr = Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ChangeType), new Type[]
                            {
                                typeof(object), typeof(Type)
                            }), expr, Expression.Constant(targetType));
                            expr = Expression.Convert(expr, targetType);
                        }
                        else
                        {
                            expr = Expression.Convert(expr, targetType);
                        }
                    }
                    var lmb = Expression.Lambda(expr, false, parameters.Parameters);
                    var d = lmb.Compile();
                    if (d == null)
                        return Result<Delegate>.Error("Error compiling delegate");
                    return d;
                });
        }
        
        public Result<Delegate> GenerateLambdaCompact(AstNode ast, ref ImmutableArray<IMemberData> members, Type targetType)
        {
            var lookup = new Dictionary<string, ParameterExpression>();
            var parameterList = new List<ParameterExpression>();
            foreach (var member in members)
            {
                var expr2 = Expression.Parameter(member.TypeDescriptor.AsTypeData().Type, member.Name);
                lookup[member.Name] = expr2;
                parameterList.Add(expr2);
                if (member.GetAttribute<DisplayAttribute>() is DisplayAttribute attr)
                {
                    lookup[attr.Name] = expr2;
                    lookup[attr.GetFullName()] = expr2;
                }
            }
            
            var parameters = new ParameterData(lookup.ToImmutableDictionary(), parameterList.ToImmutableArray());
            var usedParameters = parameters.GetUsedParameters(ast);
            var exprr = GenerateExpression(ast, parameters);

            if (exprr.Ok())
                members = members.RemoveAll(p => usedParameters.Contains(p.Name) == false);
            
            return exprr.IfThen(expr =>
            {
                var parameters2 = parameters.Parameters.Where(p => usedParameters.Contains(p.Name)).ToArray();
                if (expr.Type != targetType)
                {
                    if (targetType == typeof(string))
                    {
                        expr = Expression.Call(expr, typeof(object).GetMethod("ToString"));
                    }
                    else
                    {
                        expr = Expression.Convert(expr, targetType);
                    }
                }
                var lmb = Expression.Lambda(expr, false, parameters2);
                var d = lmb.Compile();
                
                return d;
            });
        }

        public bool IsNumberExpression(AstNode ast)
        {
            var sub = WithThrowException(false);
            if (ast is ObjectNode objectNode)
            {
                if (sub.GenerateExpression(objectNode, ParameterData.Empty, null).Ok())
                    return true;
            }

            return false;
        }
        
        
        /// <summary> Compiles the AST into a tree of concrete expressions.
        /// This will throw an exception if the types does not match up. e.g "X" + 3 (undefined operation) </summary>
        public Result<Expression> GenerateExpression(AstNode ast, ParameterData parameterExpressions, Type targetType = null)
        {
            
            switch (ast)
            {
                case BinaryExpressionNode b:
                {
                    var op = b.Operator;

                    if (op == Operators.CallOperator)
                    {
                        // call was invoked.
                        // left side is the name of the method to call.
                        // right side is a comma separated (comma operator) list of values.
                        
                        List<Expression> expressions = new List<Expression>();
                        var right2 = b.Right;
                        while (right2 is BinaryExpressionNode b2 && b2.Operator == Operators.CommaOp)
                        {
                            expressions.Add(GenerateExpression(b2.Left, parameterExpressions).Unwrap());
                            right2 = b2.Right;
                        }
                        if (right2 != null)
                        {
                            expressions.Add(GenerateExpression(right2, parameterExpressions).Unwrap());
                            right2 = null;
                        }
                        
                        var funcName = ((ObjectNode)b.Left).Data;
                        
                        MethodInfo method = GetMethod(funcName, expressions.Select(x => x.Type).ToArray());
                        if (method == null)
                        {
                            MethodInfo method2 = GetMethod(funcName, new []{typeof(ITestStepParent)}.Concat(expressions.Select(x => x.Type)).ToArray());
                            if(method2 == null)
                                return Error($"No such method: {funcName}");
                            var thisArg = parameterExpressions.Parameters.FirstOrDefault(x => x.Name == "__this__");
                            Debug.Assert(thisArg != null);
                            return Expression.Call(method2, new Expression[]
                            {
                                thisArg
                            }.Concat(expressions).ToArray());
                        }

                        return Expression.Call(method, expressions.ToArray());
                    }
                    
                    var leftr = GenerateExpression(b.Left, parameterExpressions);
                    var rightr = GenerateExpression(b.Right, parameterExpressions);
                    if ((leftr.Error() ?? rightr.Error()) is string err)
                        return Error(err);
                    var left = leftr.Unwrap();
                    var right = rightr.Unwrap();

                    // If the types of the two sides of the expression is not the same.
                    if (left.Type != right.Type)
                    {
                        // if it is numeric we can try to convert it
                        if (left.Type.IsNumeric() && right.Type.IsNumeric())
                        {
                            if (right.Type == typeof(double))
                            {
                                // in this case, always use double.
                                left = Expression.Convert(left, typeof(double));
                            }
                            else
                            {
                                // otherwise assume left is always right
                                right = Expression.Convert(right, left.Type);    
                            }
                        }
                        // if one side is numeric, lets try converting the other.
                        // maybe strings should be checked too
                        else if (right.Type.IsNumeric() && left.Type == typeof(object))
                        {
                            
                            left = Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ChangeType), new []{typeof(object), typeof(Type)}), left,  Expression.Constant(right.Type));
                            left = Expression.Convert(left, right.Type);
                        }else if (left.Type.IsNumeric() && left.Type == typeof(object))
                        {
                            right = Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ChangeType), new []{typeof(object), typeof(Type)}), right,  Expression.Constant(left.Type));
                            right = Expression.Convert(right, left.Type);
                        }
                    }

                    if (op == Operators.AdditionOp)
                        return Expression.Add(left, right);
                    if (op == Operators.MultiplyOp)
                        return Expression.Multiply(left, right);
                    if (op == Operators.DivideOp)
                        return Expression.Divide(left, right);
                    if (op == Operators.SubtractOp)
                        return Expression.Subtract(left, right);
                    if (op == Operators.AssignmentOp)
                        return Expression.Assign(left, right);
                    if (op == Operators.StatementOperator)
                        return Expression.Block(left, right);
                    if (op == Operators.LessOperator)
                        return Expression.LessThan(left, right);
                    if (op == Operators.GreaterOperator)
                        return Expression.GreaterThan(left, right);
                    if (op == Operators.LessOrEqualOperator)
                        return Expression.LessThanOrEqual(left, right);
                    if (op == Operators.GreaterOperator)
                        return Expression.GreaterThan(left, right);
                    if (op == Operators.GreaterOrEqualOperator)
                        return Expression.GreaterThanOrEqual(left, right);
                    if (op == Operators.EqualOperator)
                        return Expression.Equal(left, right);
                    if (op == Operators.NotEqualOperator)
                        return Expression.NotEqual(left, right);
                    if (op == Operators.AndOperator)
                        return Expression.AndAlso(left, right);
                    if (op == Operators.OrOperator)
                        return Expression.OrElse(left, right);
                    if (op == Operators.StrCombineOperator)
                    {
                        if (left.Type != typeof(string))
                        {
                            left = Expression.Call(left, typeof(object).GetMethod("ToString"));
                        }
                        if (right.Type != typeof(string))
                        {
                            right = Expression.Call(right, typeof(object).GetMethod("ToString"));
                        }
                        
                        return Expression.Call(typeof(String).GetMethod("Concat", new Type[]
                        {
                            typeof(string), typeof(string)
                        }), left, right);
                    }
                    break;
                }
                case ObjectNode i:
                {
                    if (i.IsString)
                    {
                        return Expression.Constant(i.Data);
                    }
                    // if it is an object node, it can either be a parameter (variables) or a constant. 

                    // is there a matching parameter?
                    if(parameterExpressions.Lookup.TryGetValue(i.Data, out var expression))
                        return expression;

                    var prop = GetProperty(i.Data);
                    if (prop != null)
                        return Expression.Property(null, prop);

                    if (nf != null)
                    {
                        var value = nf.ParseNumber(i.Data, targetType != null ? targetType : typeof(double));
                        return Expression.Constant(value);
                    }
                    
                    // otherwise, is it a constant?
                    if (int.TryParse(i.Data, out var i1))
                        return Expression.Constant(i1);

                    if (double.TryParse(i.Data, out var i2))
                        return Expression.Constant(i2);

                    if (long.TryParse(i.Data, out var i3))
                        return Expression.Constant(i3);

                    return Error($"Unable to understand: \"{i.Data}\".");
                }
            }

            return Error($"Unable to parse expression.");
        }
        

        public AstNode ParseStringInterpolation(string str, bool isSubString = false)
        {
            ReadOnlySpan<char> str2 = str.ToArray();
            return ParseStringInterpolation(ref str2, isSubString);
        }

        /// <summary>
        /// Builds an AstNode for string interpolation. e.g "This is the result: {FrequencyMeasurement} Hz".
        /// </summary>
        /// <param name="str"></param>
        /// <param name="isSubString">This should end on a quote</param>
        /// <returns></returns>
        public AstNode ParseStringInterpolation(ref ReadOnlySpan<char> str, bool isSubString = false)
        {
            // This node will be continously built upon.
            AstNode returnNode = null;
            
            // For building a string of chars.
            List<char> read = new List<char>();
            while (str.Length > 0)
            {
                
                if (str[0] == '}')
                {
                    // handle escaped '}}'.
                    if (str.Length > 1 && str[1] == '}')
                    {
                        str = str.Slice(2);
                        read.Add('}');
                        continue;
                    }
                    break;
                }

                if (str[0] == '"')
                {
                    if (str.Length > 1 && str[1] == '"')
                    {
                        str = str.Slice(2);
                        read.Add('"');
                        continue;
                    }
                    if (!isSubString)
                        throw new FormatException("Unexpected \" character");
                    str = str.Slice(1);
                    
                    break; // probably end of string.
                }
                
                if (str[0] == '{')
                {
                    // handle escaped {{.
                    if (str.Length > 1 && str[1] == '{')
                    {
                        str = str.Slice(2);
                        read.Add('{');
                        continue;
                    }
                    // this '{' denotes the start of an expression.
                    // now read until the end of the expression and parse the
                    // inner stuff as one whole expression.

                    // but first, add what came before as a string.

                    var newNode = new ObjectNode(new String(read.ToArray()))
                    {
                        IsString = true
                    };
                    if (returnNode == null) returnNode = newNode;
                    else
                    {
                        returnNode = new BinaryExpressionNode
                        {
                            Left = returnNode,
                            Operator = Operators.StrCombineOperator,
                            Right = new ObjectNode(new String(read.ToArray()))
                            {
                                IsString = true
                            }
                        };
                    }
                    
                    read.Clear();
                    str = str.Slice(1);
                    
                    var node = Parse(ref str);
                    // the next should be '}'.
                    SkipWhitespace(ref str);
                    if (str.Length == 0 || str[0] != '}')
                    {
                        throw new FormatException("Invalid formed expression");
                    }
                    str = str.Slice(1);

                    
                    if (returnNode == null)
                    {
                        returnNode = node.Unwrap();
                    }
                    else
                    {
                        returnNode = new BinaryExpressionNode
                        {
                            Left = returnNode,
                            Operator = Operators.StrCombineOperator,
                            Right = node.Unwrap()
                        };
                    }
                    continue;
                }
                read.Add(str[0]);
                str = str.Slice(1);
            }
            if (read.Count > 0)
            {
                var newNode = new ObjectNode(new String(read.ToArray()))
                {
                    IsString = true
                };
                if (returnNode == null) returnNode = newNode;
                else
                {
                    returnNode = new BinaryExpressionNode
                    {
                        Left = returnNode,
                        Operator = Operators.StrCombineOperator,
                        Right = new ObjectNode(new String(read.ToArray()))
                        {
                            IsString = true
                        }
                    };
                }
            }
            return returnNode ?? new ObjectNode(""){IsString = true};
        }

        
        AstNode ParseString(ref ReadOnlySpan<char> str)
        {
            List<char> stringContent = new List<char>();
            var str2 = str.Slice(1);
            
            while (str2.Length > 0)
            {
                if (str2[0] == '"')
                {
                    // escaped quote.
                    if (str2.Length > 1 && str2[1] == '"')
                    {
                        stringContent.Add('"');
                        str2 = str2.Slice(2);
                        continue;
                    }
                    break;
                }
                stringContent.Add(str2[0]);
                str2 = str2.Slice(1);
            }
            str2 = str2.Slice(1);
            str = str2;
            return new ObjectNode(new String(stringContent.ToArray()))
            {
                IsString = true
            };
        }

        public Result<AstNode> Parse(string str)
        {
            
            ReadOnlySpan<char> str2 = str.ToArray();
            return Parse(ref str2);
            
        }
        public Result<AstNode> Parse(ref ReadOnlySpan<char> str, bool subExpression = false)
        {
            var expressionList = new List<AstNode>();
            
            // Run through the span, parsing elements and adding them to the list.
            while (str.Length > 0)
            {
                next:
                // skip past the whitespace
                SkipWhitespace(ref str);
                
                // maybe we've read the last whitespace.
                if (str.Length == 0) 
                    break;
                if (str[0] == ',')
                {
                    str = str.Slice(1);
                    var subVal = Parse(ref str, subExpression);
                    if (subVal.Ok())
                    {
                        expressionList.Add(Operators.CommaOp);
                        expressionList.Add(subVal.Unwrap());
                    }
                    else
                        return subVal;
                    break;
                }
                // start parsing a sub-expression? (recursively).
                if (str[0] == '(')
                {
                    var prevNode = expressionList.LastOrDefault();
                    
                    if (prevNode is ObjectNode objectNode)
                    { 
                        // if this is the case it means a symbol is right next to parenthesis.
                        // e.g symbolname(parameters).
                        // parameters are separated by the comma operator, but comma is not used for anything else.
                        
                        str = str.Slice(1);
                        var noder = Parse(ref str, true);
                        if (noder.Ok() == false)
                            return noder;
                        var node = noder.Unwrap();
                        var expr = new BinaryExpressionNode {Left = objectNode, Operator = Operators.CallOperator, Right = node};
                        if(!(node is BinaryExpressionNode b &&  b.Operator == Operators.CommaOp))
                        {
                            expr.Right = new BinaryExpressionNode
                            {
                                Left = node,
                                Operator = Operators.CommaOp,
                                Right = null
                            };
                        }
                        expressionList[expressionList.Count - 1] = expr;
                        continue;
                    }
                    else
                    {
                        str = str.Slice(1);
                        var node = Parse(ref str, true);
                        if (node.Ok())
                            expressionList.Add(node.Unwrap());
                        else
                            return node;
                        continue;
                    }
                }

                // end of sub expression?
                if (str[0] == ')')
                {
                    if (!subExpression)
                        throw new FormatException("Unexpected symbol ')'");
                        
                    // done with parsing a sub-expression.
                    str = str.Slice(1);
                    break;
                }

                
                // interpolated string?
                if (str[0] == '$')
                {
                    if (str.Length <= 2 || str[1] != '"')
                        return Result.Error<AstNode>("Invalid format");

                    str = str.Slice(2);
                    var ast = ParseStringInterpolation(ref str, true);
                    expressionList.Add(ast);
                    continue;
                }
                
                // normal string?
                if (str[0] == '\"')
                {
                    var ast = ParseString(ref str);
                    expressionList.Add(ast);
                    continue;
                }

                // End of an enclosing expression.
                if (str[0] == '}')
                {
                    break;
                }

                // The content can either be an identifier or an operator.
                // numbers and other constants are also identifiers.
                var strBackup = str;
                var identr = ParseObject(ref str, x =>  char.IsLetterOrDigit(x) || x == '.' || x == '-');
                
                if (identr.Ok())
                {
                    var ident = identr.Unwrap();
                    if (ident.Data == "-")
                    {
                        str = strBackup;
                        ident = null;
                    }
                    else
                    {
                        if (expressionList.Count > 0)
                        {
                            var lst = expressionList.Last();
                            if (lst is ObjectNode on)
                            {
                                on.Data = on.Data + " " + ident.Data;
                                continue;
                            }
                        }
                        // if it is an identifier
                        expressionList.Add(ident);
                        continue;
                    }
                }

                // operators are sorted by-length to avoid that '==' gets mistaken for '='.
                foreach (var op in Operators.GetOperators())
                {
                    if (str[0] == op.Operator[0])
                    {
                        for (int i = 1; i < op.Operator.Length; i++)
                        {
                            if (str.Length <= i || str[i] != op.Operator[i])
                                goto nextOperator;
                        }
                        expressionList.Add(op);
                        str = str.Slice(op.Operator.Length);
                        goto next;
                    }
                    nextOperator: ;
                }

                return Result.Error<AstNode>("Unable to parse code");
            }
            
            // now the expression has turned into a list of identifiers and operators. 
            // e.g: [x, +, y, *, z, /, w]
            // build the abstract syntax tree by finding the operators and combine with the left and right side
            // in order of precedence (see operator precedence where operators are defined.
            while (expressionList.Count > 1)
            {
                // The index of the highest precedence operator.
                int index = expressionList.IndexOf(expressionList.FindMax(x => x is OperatorNode op ? op.Precedence : -1));
                if (index == 0 || index == expressionList.Count - 1)
                    // it cannot start or end with e.g '*'.
                    return Result.Error<AstNode>("Unable to parse sub-expression");
                
                // take out the group of things. e.g [1,*,2]
                // left and right might each be a group of statements.
                // operator should always be an operator.
                var left = expressionList.PopAt(index - 1);
                var @operator = expressionList.PopAt(index - 1);
                var right = expressionList.PopAt(index - 1);

                // Verify that the right syntax is used.
                if (!(@operator is OperatorNode) || left is OperatorNode || right is OperatorNode)
                    return Result.Error<AstNode>("Unable to parse sub-expression");
                
                // insert it back in to the list as a combined group.
                expressionList.Insert(index - 1, new BinaryExpressionNode {Left = left, Operator = (OperatorNode) @operator, Right = right});
                
            }
            if (expressionList.Count == 0)
                return null;
            // now there should only be one element left. Return it.
            if (expressionList.Count != 1)
                return Result.Error<AstNode>("Invalid expression");
            return expressionList[0];
        }

        Result<ObjectNode> ParseObject(ref ReadOnlySpan<char> str, Func<char, bool> filter)
        {
            var str2 = str;
            SkipWhitespace(ref str2);
            
            while (str2.Length > 0 && str2[0] != ' ' && filter(str2[0]))
            {
                str2 = str2.Slice(1);
            }

            if (str2 == str)
                return Result.Error<ObjectNode>("unable to parse object");
            var identifier = new ObjectNode(new string(str.Slice(0, str.Length - str2.Length).ToArray()));
            str = str2;
            return identifier;
        }

        void SkipWhitespace(ref ReadOnlySpan<char> str)
        {
            while (str.Length > 0 && str[0] == ' ')
            {
                str = str.Slice(1);
            }
        }
    }

}
