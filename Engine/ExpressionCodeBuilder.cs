using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
namespace OpenTap
{
    public class ExpressionCodeBuilder
    {
        
        public ParameterExpression[] GetParameters(object obj)
        {
            var members = TypeData.GetTypeData(obj).GetMembers()
                .ToArray();
            
            // parameters for the expression are the member variables.
            var parameters = members
                .Select(x => Expression.Parameter(x.TypeDescriptor.AsTypeData().Type, x.Name))
                .ToArray();
            return parameters;
        }

        public Delegate GenerateLambda(AstNode ast, ParameterExpression[] parameters)
        {
            var expr = GenerateCode(ast, parameters);
            var lmb = Expression.Lambda(expr, false, parameters);
            var d= lmb.Compile();
            return d;
        }
        
        /// <summary> Compiles the AST into a tree of concrete expressions.
        /// This will throw an exception if the types does not match up. e.g "X" + 3 (undefined operation) </summary>
        public Expression GenerateCode(AstNode ast, ParameterExpression[] parameterExpressions)
        {
            switch (ast)
            {
                case BinaryExpression b:
                {
                    var left = GenerateCode(b.Left, parameterExpressions);
                    var right = GenerateCode(b.Right, parameterExpressions);
                    
                    // If the types of the two sides of the expression is not the same.
                    if (left.Type != right.Type)
                    {
                        // if it is numeric we can try to convert it
                        if (left.Type.IsNumeric() && right.Type.IsNumeric())
                        {
                            // assume left is always right
                            right = Expression.Convert(right, left.Type);
                        }
                        // otherwise just hope for the best and let .NET throw an exception if necessary.
                    }
                    
                    var op = b.Operator;
                    if (op == AdditionOp)
                        return Expression.Add(left, right);
                    if (op == MultiplyOp)
                        return Expression.Multiply(left, right);
                    if (op == DivideOp)
                        return Expression.Divide(left, right);
                    if(op == SubtractOp)
                        return Expression.Subtract(left, right);
                    if (op == AssignmentOp)
                        return Expression.Assign(left, right);
                    if (op == StatementOperator)
                        return Expression.Block(left, right);
                    if (op == LessOperator)
                        return Expression.LessThan(left, right);
                    if (op == GreaterOperator)
                        return Expression.GreaterThan(left, right);
                    if (op == LessOrEqualOperator)
                        return Expression.LessThanOrEqual(left, right);
                    if (op == GreaterOperator)
                        return Expression.GreaterThan(left, right);
                    if (op == GreaterOrEqualOperator)
                        return Expression.GreaterThanOrEqual(left, right);
                    if (op == EqualOperator)
                        return Expression.Equal(left, right);
                    if (op == NotEqualOperator)
                        return Expression.NotEqual(left, right);
                    if (op == AndOperator)
                        return Expression.AndAlso(left, right);
                    if (op == OrOperator)
                        return Expression.OrElse(left, right);
                    break;
                }
                case ObjectNode i:
                {
                    // if it is an object node, it can either be a parameter (variables) or a constant. 
                    
                    // is there a matching parameter?
                    var parameterExpression = parameterExpressions
                        .FirstOrDefault(x => x.Name == i.data);
                    if (parameterExpression != null) 
                        return parameterExpression;
                    
                    // otherwise, is it a constant?
                    if (int.TryParse(i.data, out var i1))
                        return Expression.Constant(i1);
                    
                    if (double.TryParse(i.data, out var i2))
                        return Expression.Constant(i2);
                    
                    if (long.TryParse(i.data, out var i3))
                        return Expression.Constant(i3);

                    throw new Exception($"Unable to understand: \"{i.data}\".");
                }
            }

            throw new Exception("Unable to parse expression");
        }
        
        public AstNode Parse(ref ReadOnlySpan<char> str)
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
                
                // start parsing a sub-expression? (recursively).
                if (str[0] == '(')
                {
                    str = str.Slice(1);
                    var node = Parse(ref str);
                    expressionList.Add(node);
                }

                // end of sub expression?
                if (str[0] == ')')
                {
                    // done with parsing a sub-expression.
                    str = str.Slice(1);
                    break;
                }

                // The content can either be an identifier or an operator.
                // numbers and other constants are also identifiers.
                var ident = ParseObject(ref str, x =>  char.IsLetterOrDigit(x) || x == '.');
                if (ident != null)
                {
                    // if it is an identifier
                    expressionList.Add(ident);
                    continue;
                }

                // operators are sorted by-length to avoid that '==' gets mistaken for '='.
                foreach (var op in Operators)
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
                    throw new Exception("Invalid sub-expression");
                
                // take out the group of things. e.g [1,*,2]
                // left and right might each be a group of statements.
                // operator should always be an operator.
                var left = expressionList.PopAt(index - 1);
                var @operator = expressionList.PopAt(index - 1);
                var right = expressionList.PopAt(index - 1);

                // Verify that the right syntax is used.
                if (!(@operator is OperatorNode) || left is OperatorNode || right is OperatorNode)
                    throw new Exception("Invalid sub-expression");
                
                // insert it back in to the list as a combined group.
                expressionList.Insert(index - 1, new BinaryExpression {Left = left, Operator = (OperatorNode) @operator, Right = right});
                
            }

            // now there should only be one element left. Return it.
            if (expressionList.Count != 1)
                throw new Exception("Invalid expression");
            return expressionList[0];
        }

        ObjectNode ParseObject(ref ReadOnlySpan<char> str, Func<char, bool> filter)
        {
            var str2 = str;
            SkipWhitespace(ref str2);
            while (str2.Length > 0 && str2[0] != ' ' && filter(str2[0]))
            {
                str2 = str2.Slice(1);
            }

            if (str2 == str)
                return null;
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
        
        public class AstNode
        {
            
        }

        class ObjectNode : AstNode
        {
            public readonly string data;
            public ObjectNode(string data) => this.data = data;
        }
        
        class OperatorNode : AstNode
        {
            public readonly string Operator;
            public OperatorNode(string op, double precedence)
            {
                Operator = op;
                Precedence = precedence;
            }

            public readonly double Precedence;
        }

        class BinaryExpression : AstNode
        {
            public AstNode Left;
            public AstNode Right;
            public OperatorNode Operator;
        }
        
        #region Operators
        
        // operators are defined by a string an precedence.
        static readonly OperatorNode MultiplyOp = new OperatorNode("*", 5);
        static readonly OperatorNode AdditionOp = new OperatorNode("+", 3);
        static readonly OperatorNode DivideOp = new OperatorNode("/", 4);
        static readonly OperatorNode SubtractOp = new OperatorNode("-", 2);
        static readonly OperatorNode AssignmentOp = new OperatorNode("=", 1.5);
        static readonly OperatorNode StatementOperator = new OperatorNode(";", 1);
        static readonly OperatorNode LessOperator = new OperatorNode("<", 1.8);
        static readonly OperatorNode GreaterOperator = new OperatorNode(">", 1.8);
        static readonly OperatorNode LessOrEqualOperator = new OperatorNode("<=", 1.8);
        static readonly OperatorNode GreaterOrEqualOperator = new OperatorNode(">=", 1.8);
        static readonly OperatorNode EqualOperator = new OperatorNode("==", 1.8);
        static readonly OperatorNode NotEqualOperator = new OperatorNode("!=", 1.8);
        static readonly OperatorNode AndOperator = new OperatorNode("&&", 1.7);
        static readonly OperatorNode OrOperator = new OperatorNode("||", 1.7);
        
        static OperatorNode[] operators;
        static OperatorNode[] Operators
        {
            get
            {
                // Use reflection to fetch all the defined operators.
                return operators ??= typeof(ExpressionCodeBuilder)
                    .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                    .Select(x => x.GetValue(null))
                    .OfType<OperatorNode>()
                    // longest operators are first, so that '=' is not used instead of '=='.
                    .OrderByDescending(o => o.Operator.Length)
                    .ToArray();
            }
        }
        #endregion
    }

}
