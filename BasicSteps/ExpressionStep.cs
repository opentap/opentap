using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OpenTap.Plugins.BasicSteps
{
    using Expr = System.Linq.Expressions.Expression;

    /// <summary> This expression step defines an expression over user defined variables. The expression step defines a
    /// little mini-language for evaluating calculator-like expressions. </summary>
    [Display("Expression", "Invokes a simple expression based on it's variable.", Group: "Basic Steps")]
    [AllowAnyChild]
    public class ExpressionStep : TestStep
    {
        public ExpressionStep()
        {
            Name = "{Mode} {Expression}";
        }
        [DefaultValue("")]
        public string Expression { get; set; } = "";

        /// <summary>
        /// The various modes available for the expression step.
        /// </summary>
        public enum ModeType
        {
            /// <summary> The expression gets evaluated. </summary>
            [Display("Evaluate",  "The expression gets evaluated.")]
            Evaluate,
            /// <summary> Conditional expression. </summary>
            [Display("If",  "If the expression evaluates to 'true', then child steps are executed.")]
            If,
            /// <summary> Set the verdict based on the result of the expression. </summary>
            [Display("Check",  "If the expression evaluates to 'true', then child steps are executed.")]
            Check
        }
        
        /// <summary> When ModeType.If is used, what should happen? </summary>
        public enum IfBehaviorType
        {
            /// <summary> Run the child steps. </summary>
            [Display("Run Child Test Steps", "Run the child test steps.")]
            RunChildTestSteps,
            /// <summary> Break out of the current loop (parent).</summary>
            [Display("Break Loop", "Break the currently executed loop parent step.")]
            BreakLoop
        }

        /// <summary> Gets or sets the current mode for the expression step. </summary>
        [DefaultValue(ModeType.Evaluate)]
        public ModeType Mode { get; set; } = ModeType.Evaluate;

        /// <summary> Gets or sets the behavior in the 'if' mode. </summary>
        [DefaultValue(IfBehaviorType.RunChildTestSteps)]
        [EnabledIf(nameof(Mode), ModeType.If, HideIfDisabled = true)]
        public IfBehaviorType IfBehavior { get; set; } = IfBehaviorType.RunChildTestSteps;
        
        public override void Run()
        {
            var members = TypeData.GetTypeData(this).GetMembers()
                .OfType<UserDefinedDynamicMember>()
                .ToArray();
            
            // parameters for the expression are the member variables.
            var parameters = members
                .Select(x => Expr.Parameter(x.TypeDescriptor.AsTypeData().Type, x.Name))
                .ToArray();
            
            // parse the expression and get an Abstract Syntax Tree.
            ReadOnlySpan<char> toParse = Expression.ToArray();
            var ast = Parse(ref toParse);
            
            // generate the expression tree.
            var expr = GenerateCode(ast, parameters);
            
            // if the mode is Evaluate, then generate the code for returning the value of
            // all the parameters (as an object[]), so if needed, they can be updated.
            if(Mode == ModeType.Evaluate)
                expr = Expr.Block(expr, Expr.NewArrayInit(typeof(object), 
                    parameters.Select(x => Expr.Convert(x, typeof(object)))));
            
            // compile the expression tree as a lambda expression.
            var lmb = Expr.Lambda(expr, false, parameters);
            var d= lmb.Compile();
            
            // invoke it dynamically.
            var result = d.DynamicInvoke(members.Select(x => x.GetValue(this))
                .ToArray());

            if (Mode == ModeType.Evaluate)
            {
                // if it is Evaluation mode, set the values according to what is evaluated.
                // e.g A = X
                var newValues = (object[]) result;
                foreach (var set in newValues.Pairwise(members))
                {
                    set.Item2.SetValue(this, set.Item1);
                }
            }
            else if (Mode == ModeType.If)
            {
                // for If mode, we expect something like X > 3.
                if (Equals(result, true))
                {
                    if (IfBehavior == IfBehaviorType.RunChildTestSteps)
                        RunChildSteps();
                    else if (IfBehavior == IfBehaviorType.BreakLoop)
                        GetParent<LoopTestStep>()?.BreakLoop();
                }else if (!Equals(result, false))
                {
                    throw new Exception("Result of expression is not true/false.");
                }
            }
            else if (Mode == ModeType.Check)
            {
                // this is the same as If, except setting the verdict based on the result.
                if (Equals(result, true))
                    UpgradeVerdict(Verdict.Pass);
                else if(Equals(result, false))
                    UpgradeVerdict(Verdict.Fail);
                else
                    throw new Exception("Result of expression is not true/false.");
            }
        }
        
        /// <summary> Compiles the AST into a tree of concrete expressions.
        /// This will throw an exception if the types does not match up. e.g "X" + 3 (undefined operation) </summary>
        Expression GenerateCode(AstNode ast, ParameterExpression[] parameterExpressions)
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
                            right = Expr.Convert(right, left.Type);
                        }
                        // otherwise just hope for the best and let .NET throw an exception if necessary.
                    }
                    
                    var op = b.Operator;
                    if (op == AdditionOp)
                        return Expr.Add(left, right);
                    if (op == MultiplyOp)
                        return Expr.Multiply(left, right);
                    if (op == DivideOp)
                        return Expr.Divide(left, right);
                    if(op == SubtractOp)
                        return Expr.Subtract(left, right);
                    if (op == AssignmentOp)
                        return Expr.Assign(left, right);
                    if (op == StatementOperator)
                        return Expr.Block(left, right);
                    if (op == LessOperator)
                        return Expr.LessThan(left, right);
                    if (op == GreaterOperator)
                        return Expr.GreaterThan(left, right);
                    if (op == LessOrEqualOperator)
                        return Expr.LessThanOrEqual(left, right);
                    if (op == GreaterOperator)
                        return Expr.GreaterThan(left, right);
                    if (op == GreaterOrEqualOperator)
                        return Expr.GreaterThanOrEqual(left, right);
                    if (op == EqualOperator)
                        return Expr.Equal(left, right);
                    if (op == NotEqualOperator)
                        return Expr.NotEqual(left, right);
                    if (op == AndOperator)
                        return Expr.AndAlso(left, right);
                    if (op == OrOperator)
                        return Expr.OrElse(left, right);
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
                        return Expr.Constant(i1);
                    
                    if (double.TryParse(i.data, out var i2))
                        return Expr.Constant(i2);
                    
                    if (long.TryParse(i.data, out var i3))
                        return Expr.Constant(i3);

                    throw new Exception($"Unable to understand: \"{i.data}\".");
                }
            }

            throw new Exception("Unable to parse expression");
        }
        
        AstNode Parse(ref ReadOnlySpan<char> str)
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
        
        class AstNode
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
                return operators ??= typeof(ExpressionStep)
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