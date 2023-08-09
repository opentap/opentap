using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
namespace OpenTap.Expressions
{
    /// <summary> This class both parses and compiles expressions. It generates an abstract syntax tree and can then compile them using Expression trees. </summary>
    class ExpressionCodeBuilder
    {
        static Result<Expression> Error(string error) => Result.Fail<Expression>(error);

        /// <summary> Imported methods. These are imported with IExpressionFunctionProviders.</summary>
        readonly ImmutableArray<MethodInfo> importedMethods;
        /// <summary> Imported variables. These are imported with IExpressionFunctionProviders.</summary>
        readonly ImmutableArray<PropertyInfo> importedProperties;

        /// <summary> For parsing objects with units. E.g "1.0 s". This may be null. </summary>
        readonly NumberFormatter nf;
        
        /// <summary> If this number changed it means new plugins has been installed and we need to update the cached expression function providers.</summary>
        readonly int pluginsChangedId;

        /// <summary> If an exception should be thrown on errors. </summary>
        public bool ThrowException { get; }

        public ExpressionCodeBuilder()
        {
            // load imports.
            var providers = TypeData.GetDerivedTypes<IExpressionFunctionProvider>()
                .Where(x => x.CanCreateInstance)
                .Select(x => x.CreateInstanceSafe())
                .OfType<IExpressionFunctionProvider>()
                .SelectMany(x => x.GetMembers())
                .ToArray();
            importedMethods = providers.OfType<MethodInfo>().ToImmutableArray();
            importedProperties = providers.OfType<PropertyInfo>().ToImmutableArray();
            pluginsChangedId = PluginManager.ChangeID;
            
        }

        /// <summary> returns 'this' unless new plugins has been installed. </summary>
        public ExpressionCodeBuilder Update()
        {
            if (pluginsChangedId == PluginManager.ChangeID) 
                return this;
            
            return new ExpressionCodeBuilder().WithNumberFormatter(nf).WithThrowException(ThrowException);
        }

        ExpressionCodeBuilder(ExpressionCodeBuilder builder)
        {
            nf = builder.nf;
            ThrowException = builder.ThrowException;
            importedProperties = builder.importedProperties;
            importedMethods = builder.importedMethods;
        }

        ExpressionCodeBuilder(ExpressionCodeBuilder builder, bool throwException) : this(builder) => ThrowException = throwException;
        ExpressionCodeBuilder(ExpressionCodeBuilder builder, NumberFormatter numberFormatter) : this(builder) => nf = numberFormatter;

        public MethodInfo GetMethod(string name, Type[] types) => importedMethods.FirstOrDefault(p =>
        {
            var parameters = p.GetParameters();
            return p.Name == name 
                   && parameters.Length == types.Length
                   && types.Pairwise(parameters).All(x => x.Item1.DescendsTo(x.Item2.ParameterType));
        });
        public PropertyInfo GetProperty(string name) => importedProperties.FirstOrDefault(p => p.Name == name);
        public ExpressionCodeBuilder WithNumberFormatter(NumberFormatter numberFormatter) => new ExpressionCodeBuilder(this, numberFormatter);
        public ExpressionCodeBuilder WithThrowException(bool throws) => new ExpressionCodeBuilder(this, throws);

        public void UpdateParameterMembers(object obj, ref ImmutableArray<IMemberData> members, out bool updated)
        {
            updated = ParameterData.UpdateParameterMembers(obj, ref members);
        }

        /// <summary> Generates an lambda expression with the given parameters and target type. </summary>
        /// <returns> A result containing the delegate if everything went well. </returns>
        public Result<Delegate> GenerateLambda(AstNode ast, ParameterData parameters, Type targetType)
        {
            return GenerateExpression(ast, parameters, targetType)
                .Then(expr =>
                {

                    if (expr.Type != targetType)
                    {
                        if (targetType == typeof(string))
                        {
                            expr = Expression.Call(expr, typeof(object).GetMethod(nameof(ToString)));
                        }
                        else if (targetType.IsNumeric() && expr.Type == typeof(string))
                        {
                            expr = Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ChangeType), new []
                            {
                                typeof(object), typeof(Type)
                            }), expr, Expression.Constant(targetType));
                            expr = Expression.Convert(expr, targetType);
                        }
                        else
                        {
                            if (targetType == typeof(object))
                            {
                                expr = Expression.Convert(expr, targetType);
                            }
                            else
                            {
                                if (expr.Type.IsNumeric() && targetType.IsNumeric())
                                    expr = Expression.Convert(expr, targetType);
                                else
                                    return Result.Fail<Delegate>($"Cannot convert result {expr.Type.Name} to {targetType.Name}.");
                            }
                        }
                    }
                    var lmb = Expression.Lambda(expr, false, parameters.Parameters);
                    var d = lmb.Compile();
                    
                    if (d == null)
                        return Result.Fail<Delegate>("Error compiling delegate");
                    return d;
                });
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

                    if (op == Operators.Call)
                    {
                        // call was invoked.
                        // left side is the name of the method to call.
                        // right side is a comma separated (comma operator) list of values.

                        List<Expression> expressions = new List<Expression>();
                        var right2 = b.Right;
                        while (right2 is BinaryExpressionNode b2 && b2.Operator == Operators.Comma)
                        {
                            var expr = GenerateExpression(b2.Left, parameterExpressions);
                            if (!expr.Ok)
                                return expr;
                            expressions.Add(expr.Unwrap());
                            right2 = b2.Right;
                        }
                        if (right2 != null)
                        {
                            switch (GenerateExpression(right2, parameterExpressions))
                            {
                                case { Ok: true, Value: var expr }:
                                    expressions.Add(expr);
                                    break;
                                case { Ok: false } r:
                                    return r;
                            }
                        }

                        var funcName = ((ObjectNode)b.Left).Content;

                        MethodInfo method = GetMethod(funcName, expressions.Select(x => x.Type).ToArray());
                        if (method == null)
                        {
                            
                            // if the method takes the test step as the first argument
                            // include it as an implicit 'this' argument.
                            // this is not used in the first iteration of expressions.
                            MethodInfo methodWithThis = GetMethod(funcName, new[]
                            {
                                typeof(ITestStepParent)
                            }.Concat(expressions.Select(x => x.Type)).ToArray());

                            if (methodWithThis == null)
                            {
                                
                                // if the method was not found generate an error.
                                
                                var methods = importedMethods.Where(x => x.Name == funcName).ToArray();
                                foreach (var method2 in methods)
                                {
                                    var p = method2.GetParameters();
                                    if (p.Length != expressions.Count)
                                        continue;
                                    List<Expression> expr2 = new List<Expression>();

                                    for (int i = 0; i < p.Length; i++)
                                    {
                                        var a = p[i].ParameterType;
                                        var b2 = expressions[i].Type;
                                        if (a == typeof(double) && b2 == typeof(int))
                                            expr2.Add(Expression.Convert(expressions[i], typeof(double)));
                                        else goto nextit;
                                    }
                                    expressions = expr2;
                                    method = method2;
                                    goto useMethod;
                                    nextit: ;
                                }
                                if (methods.Length > 0)
                                {
                                    bool wrongCount = methods.All(x => x.GetParameters().Count() != expressions.Count);
                                    if (wrongCount)
                                        return Error($"Invalid number of arguments for '{funcName}'.");
                                    return Error($"Invalid argument types for '{funcName}'.");
                                }

                                if (importedProperties.Any(x => x.Name == funcName))
                                {
                                    return Error($"'{funcName}' cannot be used as a function.");    
                                }

                                return Error($"'{funcName}' function not found.");
                            }
                            var thisArg = parameterExpressions.Parameters.FirstOrDefault(x => x.Name == "__this__");
                            Debug.Assert(thisArg != null);
                            return Expression.Call(methodWithThis, new Expression[]
                            {
                                thisArg
                            }.Concat(expressions).ToArray());
                        }
                        useMethod: ;

                        try
                        {
                            return Expression.Call(method, expressions.ToArray());
                        }
                        catch (Exception e)
                        {
                            return Error(e.Message);
                        }
                    }

                    Expression left, right;
                    switch (GenerateExpression(b.Left, parameterExpressions))
                    {
                        case { Ok: false } r:
                            return r;
                        case {Ok: true, Value: var expr}:
                            left = expr;
                            break;
                    }

                    switch (GenerateExpression(b.Right, parameterExpressions))
                    {
                        case { Ok: false } r:
                            return r;
                        case {Ok: true, Value: var expr}:
                            right = expr;
                            break;
                    }
                    
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

                            left = Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ChangeType), new[]
                            {
                                typeof(object), typeof(Type)
                            }), left, Expression.Constant(right.Type));
                            left = Expression.Convert(left, right.Type);
                        }
                        else if (left.Type.IsNumeric() && left.Type == typeof(object))
                        {
                            right = Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ChangeType), new[]
                            {
                                typeof(object), typeof(Type)
                            }), right, Expression.Constant(left.Type));
                            right = Expression.Convert(right, left.Type);
                        }
                    }

                    if (op == Operators.Addition)
                        return Expression.Add(left, right);
                    if (op == Operators.Multiply)
                        return Expression.Multiply(left, right);
                    if (op == Operators.Power)
                    {
                        if(left.Type != typeof(double))
                            left = Expression.Convert(left, typeof(double));
                        if(right.Type != typeof(double))
                            right = Expression.Convert(right, typeof(double));
                        
                        return Expression.Power(left, right);
                    }
                    if (op == Operators.Divide)
                        return Expression.Divide(left, right);
                    if (op == Operators.Subtract)
                        return Expression.Subtract(left, right);
                    if (op == Operators.Less)
                        return Expression.LessThan(left, right);
                    if (op == Operators.Greater)
                        return Expression.GreaterThan(left, right);
                    if (op == Operators.LessOrEqual)
                        return Expression.LessThanOrEqual(left, right);
                    if (op == Operators.Greater)
                        return Expression.GreaterThan(left, right);
                    if (op == Operators.GreaterOrEqual)
                        return Expression.GreaterThanOrEqual(left, right);
                    if (op == Operators.Equal)
                        return Expression.Equal(left, right);
                    if (op == Operators.NotEqual)
                        return Expression.NotEqual(left, right);
                    if (op == Operators.And)
                        return Expression.AndAlso(left, right);
                    if (op == Operators.Or)
                        return Expression.OrElse(left, right);
                    if (op == Operators.StrCombine)
                    {
                        Expression toStringExpression(Expression expr)
                        {
                            if (expr.Type == typeof(float) || expr.Type == typeof(double))
                            {
                                if (expr.Type == typeof(float))
                                    expr = Expression.Convert(expr, typeof(double));
                                // round to the nearest value with 15 0's of precision.
                                // This is to avoid simple expressions like 6.0 / 2.0 turning into e.g 3.00000000000002
                                expr = Expression.Call(null, typeof(Math).GetMethod(nameof(Math.Round), new []
                                {
                                    typeof(double), typeof(int),
                                }), expr, Expression.Constant(15));
                            }

                            return Expression.Call(expr, typeof(object).GetMethod("ToString"));
                        }
                        
                        if (left.Type != typeof(string))
                            left = toStringExpression(left);
                        if (right.Type != typeof(string))
                            right = toStringExpression(right);

                        return Expression.Call(typeof(String).GetMethod("Concat", new []
                        {
                            typeof(string), typeof(string)
                        }), left, right);
                    }
                    break;
                }
                case ObjectNode i:
                {
                    if (i.IsLiteralString)
                    {
                        return Expression.Constant(i.Content);
                    }
                    // if it is an object node, it can either be a parameter (variables) or a constant. 

                    // is there a matching parameter?
                    if (parameterExpressions.Lookup.TryGetValue(i.Content, out var expression))
                        return expression;

                    var prop = GetProperty(i.Content);
                    if (prop != null)
                        return Expression.Property(null, prop);

                    if (nf != null)
                    {
                        try
                        {
                            var value = nf.ParseNumber(i.Content, targetType ?? typeof(double));
                            return Expression.Constant(value);
                        }
                        catch
                        {
                            // this is ok. We'll try something else.
                        }
                    }

                    // otherwise, is it a constant?
                    if (int.TryParse(i.Content, out var i1))
                        return Expression.Constant(i1);

                    if (double.TryParse(i.Content, out var i2))
                        return Expression.Constant(i2);

                    if (long.TryParse(i.Content, out var i3))
                        return Expression.Constant(i3);

                    return Error($"'{i.Content}' symbol not found.");
                }
            }

            return Error($"{ast} is an invalid expression.");
        }

        /// <summary>  Builds an AstNode for string interpolation. e.g "This is the result: {FrequencyMeasurement} Hz". </summary>
        public Result<AstNode> ParseStringInterpolation(string str)
        {
            return ParseStringInterpolation(str, false);
        }
        
        /// <summary> Builds an AstNode for string interpolation. e.g "This is the result: {FrequencyMeasurement} Hz". The str will be iterated to the end of the parsed code. </summary>
        public Result<AstNode> ParseStringInterpolation(ref ReadOnlySpan<char> str)
        {
            return ParseStringInterpolation(ref str, false);
        }
        
        Result<AstNode> ParseStringInterpolation(string str, bool isSubString)
        {
            ReadOnlySpan<char> str2 = str.ToArray();
            return ParseStringInterpolation(ref str2, isSubString);
        }

        
        Result<AstNode> ParseStringInterpolation(ref ReadOnlySpan<char> str, bool isSubString)
        {
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
                        return Result.Fail<AstNode>("Unexpected \" character");
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

                    var newNode = new ObjectNode(new String(read.ToArray()), true);
                    if (returnNode == null) returnNode = newNode;
                    else
                    {
                        returnNode = new BinaryExpressionNode(returnNode, Operators.StrCombine, new ObjectNode(new String(read.ToArray()), true));
                    }

                    read.Clear();
                    str = str.Slice(1);

                    var nodeBase = Parse(ref str, false, stringSubExpression: true);

                    if (nodeBase.Ok == false)
                        return nodeBase;
                    var node = nodeBase.Unwrap();
                    // the next should be '}'.
                    SkipWhitespace(ref str);
                    if (str.Length == 0 || str[0] != '}')
                    {
                        return Result.Fail<AstNode>("Invalid formed expression");
                    }
                    str = str.Slice(1);

                    returnNode = new BinaryExpressionNode(returnNode, Operators.StrCombine, node);
                    continue;
                }
                read.Add(str[0]);
                str = str.Slice(1);
            }
            if (read.Count > 0)
            {
                var newNode = new ObjectNode(new String(read.ToArray()), true);
                if (returnNode == null) returnNode = newNode;
                else
                {
                    returnNode = new BinaryExpressionNode(returnNode, Operators.StrCombine, new ObjectNode(new String(read.ToArray()), true));
                }
            }
            return returnNode ?? new ObjectNode("", true);
        }


        Result<AstNode> ParseString(ref ReadOnlySpan<char> str)
        {
            List<char> stringContent = new List<char>();
            var str2 = str.Slice(1);

            while (str2.Length > 0)
            {
                if (str2.Length == 0)
                    return Result.Fail<AstNode>("Invalid format for string.");
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
            return new ObjectNode(new String(stringContent.ToArray()), true);
        }

        public Result<AstNode> Parse(string str)
        {

            ReadOnlySpan<char> str2 = str.ToArray();
            return Parse(ref str2);

        }
        
        public Result<AstNode> Parse(ref ReadOnlySpan<char> str)
        {
            return Parse(ref str, false, false);
        }
        
        Result<AstNode> Parse(ref ReadOnlySpan<char> str, bool subExpression, bool stringSubExpression)
        {
            var expressionList = new List<AstNode>();
            bool gotEnd = false;

            // Run through the span, parsing elements and adding them to the list.
            while (true)
            {
                next:
                // skip past the whitespace
                SkipWhitespace(ref str);

                // maybe we've read the last whitespace.
                if (str.Length == 0)
                {
                    if (subExpression)
                        return Result.Fail<AstNode>("Reached end of text but expected ')'.");
                    if (stringSubExpression)
                        return Result.Fail<AstNode>("Reached end of text but expected '}'.");
                    break;
                }
                if (str[0] == ',')
                {
                    str = str.Slice(1);
                    var subVal = Parse(ref str, subExpression, stringSubExpression);
                    if (subVal.Ok)
                    {
                        expressionList.Add(Operators.Comma);
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
                        // e.g symbol-name(parameters).
                        // parameters are separated by the comma operator, but comma is not used for anything else.

                        str = str.Slice(1);
                        AstNode node;
                        switch (Parse(ref str, true, false))
                        {
                            case {Ok: true, Value: var n}:
                                node = n;
                                break;
                            case var r:
                                return r;
                        }

                        var expr = new BinaryExpressionNode(objectNode, Operators.Call, node);
                        if (!(node is BinaryExpressionNode b && b.Operator == Operators.Comma))
                        {
                            expr = expr.WithRight(new BinaryExpressionNode(node, Operators.Comma, null));
                        }
                        expressionList[expressionList.Count - 1] = expr;
                        continue;
                    }
                    else
                    {
                        str = str.Slice(1);
                        switch(Parse(ref str, true, false))
                        {
                            case {Ok: true, Value: var node}:
                                expressionList.Add(node);
                                break;
                            case var r:
                                return r;
                        }
                        continue;
                    }
                }

                // end of sub expression?
                if (str[0] == ')')
                {
                    if (!subExpression)
                        return Result.Fail<AstNode>("Unexpected symbol ')'.");
                    
                    gotEnd = true;
                    // done with parsing a sub-expression.
                    str = str.Slice(1);
                    break;
                }


                // interpolated string?
                if (str[0] == '$')
                {
                    if (str.Length <= 2 || str[1] != '"')
                        return Result.Fail<AstNode>("Invalid format");

                    str = str.Slice(2);
                    var parseResult = ParseStringInterpolation(ref str, true);
                    if (!parseResult.Ok)
                        return parseResult;
                    expressionList.Add(parseResult.Unwrap());
                    continue;
                }

                // normal string?
                if (str[0] == '\"')
                {
                    var ast = ParseString(ref str);
                    if (ast.Ok == false)
                        return ast;
                    expressionList.Add(ast.Unwrap());
                    continue;
                }

                // End of an enclosing expression.
                if (str[0] == '}')
                {
                    if (!stringSubExpression)
                        return Result.Fail<AstNode>("Unexpected symbol '}'.");
                    break;
                }

                // The content can either be an identifier or an operator.
                // numbers and other constants are also identifiers.
                var strBackup = str;
                var identResult = ParseObjectNode(ref str, x => char.IsLetterOrDigit(x) || x == '.' || x == '-');

                if (identResult.Ok)
                {
                    var ident = identResult.Value;
                    if (ident.Content == "-")
                    {
                        str = strBackup;
                    }
                    else
                    {
                        if (expressionList.Count > 0)
                        {
                            var lst = expressionList.Last();
                            if (lst is ObjectNode on)
                            {
                                expressionList[expressionList.Count - 1] = new ObjectNode(on.Content + " " + ident.Content, on.IsLiteralString);
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

                return Result.Fail<AstNode>("Unable to parse code.");
            }
            if (subExpression && gotEnd == false)
            {
                //return Result.Fail<AstNode>("Expected ).");
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
                    return Result.Fail<AstNode>("Unable to parse sub-expression");

                // take out the group of things. e.g [1,*,2]
                // left and right might each be a group of statements.
                // operator should always be an operator.
                var left = expressionList.PopAt(index - 1);
                var @operator = expressionList.PopAt(index - 1);
                var right = expressionList.PopAt(index - 1);

                // Verify that the right syntax is used.
                if (!(@operator is OperatorNode) || left is OperatorNode || right is OperatorNode)
                    return Result.Fail<AstNode>("Unable to parse sub-expression");

                // insert it back in to the list as a combined group.
                expressionList.Insert(index - 1, new BinaryExpressionNode(left, (OperatorNode)@operator, right));
            }
            
            
            
            if (expressionList.Count == 0)
                return null;
            // now there should only be one element left. Return it.
            if (expressionList.Count != 1)
                return Result.Fail<AstNode>("Invalid expression");
            return expressionList[0];
        }

        /// <summary> Reads characters until the readCondition is no longer true. </summary>
        /// <param name="str"></param>
        /// <param name="readCondition"></param>
        /// <returns>The parsed object or an error. </returns>
        Result<ObjectNode> ParseObjectNode(ref ReadOnlySpan<char> str, Func<char, bool> readCondition)
        {
            var str2 = str;
            SkipWhitespace(ref str2);

            while (str2.Length > 0 && str2[0] != ' ' && readCondition(str2[0]))
            {
                str2 = str2.Slice(1);
            }

            if (str2 == str)
                return Result.Fail<ObjectNode>("Unable to parse object");
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
