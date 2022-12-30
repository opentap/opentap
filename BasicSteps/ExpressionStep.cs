using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OpenTap.Plugins.BasicSteps
{
    using Expr = System.Linq.Expressions.Expression;

    [Display("Expression", "Invokes a simple expression based on it's variable.")]
    [AllowAnyChild]
    public class ExpressionStep : TestStep
    {
        public ExpressionStep()
        {
            Name = "{Mode} {Expression}";
        }
        [DefaultValue("")]
        public string Expression { get; set; } = "";

        public enum ModeType
        {
            [Display("Evaluate",  "The expression gets evaluated.")]
            Evaluate,
            [Display("If",  "If the expression evaluates to 'true', then child steps are executed.")]
            If,
            [Display("Check",  "If the expression evaluates to 'true', then child steps are executed.")]
            Check
        }
        
        public enum IfBehaviorType
        {
            RunChildSteps,
            BreakLoop
        }

        [DefaultValue(ModeType.Evaluate)]
        public ModeType Mode { get; set; } = ModeType.Evaluate;

        [DefaultValue(IfBehaviorType.RunChildSteps)]
        [EnabledIf(nameof(Mode), ModeType.If, HideIfDisabled = true)]
        public IfBehaviorType IfBehavior { get; set; } = IfBehaviorType.RunChildSteps;
        
        public override void Run()
        {
            var members = TypeData.GetTypeData(this).GetMembers()
                .OfType<UserDefinedDynamicMember>()
                .ToArray();
            
            var parameters = members
                .Select(x => Expr.Parameter(x.TypeDescriptor.AsTypeData().Type, x.Name))
                .ToArray();
            
            // parse the expression and get an Abstract Syntax Tree.
            var ast = Parse(Expression.ToArray());
            
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
                    if (IfBehavior == IfBehaviorType.RunChildSteps)
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
        
        Expression GenerateCode(AstNode ast, ParameterExpression[] parameterExpressions)
        {
            switch (ast)
            {
                case BinaryExpression b:
                {
                    var left = GenerateCode(b.Left, parameterExpressions);
                    var right = GenerateCode(b.Right, parameterExpressions);
                    if (left.Type != right.Type)
                    {
                        if (left.Type.IsNumeric() && right.Type.IsNumeric())
                        {
                            // assume left is always right
                            right = Expr.Convert(right, left.Type);
                        }
                    }
                    var op = b.Operator;
                    if (op == AdditionOp)
                        return System.Linq.Expressions.Expression.Add(left, right);
                    if (op == MultiplyOp)
                        return System.Linq.Expressions.Expression.Multiply(left, right);
                    if (op == DivideOp)
                        return System.Linq.Expressions.Expression.Divide(left, right);
                    if(op == SubtractOp)
                        return System.Linq.Expressions.Expression.Subtract(left, right);
                    if (op == AssignmentOp)
                        return System.Linq.Expressions.Expression.Assign(left, right);
                    if (op == StatementOperator)
                        return System.Linq.Expressions.Expression.Block(left, right);
                    if (op == LessOperator)
                        return System.Linq.Expressions.Expression.LessThan(left, right);
                    if (op == GreaterOperator)
                        return System.Linq.Expressions.Expression.GreaterThan(left, right);
                    if (op == LessOrEqualOperator)
                        return System.Linq.Expressions.Expression.LessThanOrEqual(left, right);
                    if (op == GreaterOperator)
                        return System.Linq.Expressions.Expression.GreaterThan(left, right);
                    if (op == GreaterOrEqualOperator)
                        return System.Linq.Expressions.Expression.GreaterThanOrEqual(left, right);
                    if (op == EqualOperator)
                        return System.Linq.Expressions.Expression.Equal(left, right);
                    break;
                }
                case IdentifierNode i:
                {
                    var parameterExpression = parameterExpressions
                        .FirstOrDefault(x => x.Name == i.Identifier);
                    if (parameterExpression != null) return parameterExpression;
                    
                    if (int.TryParse(i.Identifier, out var i1))
                        return System.Linq.Expressions.Expression.Constant(i1);
                    
                    if (double.TryParse(i.Identifier, out var i2))
                        return System.Linq.Expressions.Expression.Constant(i2);
                    
                    if (long.TryParse(i.Identifier, out var i3))
                        return System.Linq.Expressions.Expression.Constant(i3);
                    
                    break;
                }
                default: break;

            }

            throw new Exception("Unable to parse expression");
        }
        
        AstNode Parse(ReadOnlySpan<char> str)
        {
            var symbolStack = new Stack<AstNode>();
            while (str.Length > 0)
            {
                next:
                SkipWhitespace(ref str);
                if (str.Length == 0) return null;
                var ident = ParseIdentifier(ref str, x =>  char.IsLetterOrDigit(x) || x == '.');
                if (ident != null)
                {
                    symbolStack.Push(ident);
                    continue;
                }

                foreach (var op in operators)
                {
                    if (str[0] == op.Operator[0])
                    {
                        for (int i = 1; i < op.Operator.Length; i++)
                        {
                            if (str.Length <= i || str[i] != op.Operator[i])
                                goto nextOperator;
                        }
                        symbolStack.Push(op);
                        str = str.Slice(1);
                        goto next;
                    }
                    nextOperator: ;
                }
            }

            var stack2 = symbolStack.Reverse().ToList();

            // x + y * z / w
            while (stack2.Count > 1)
            {
                int index = stack2.IndexOf(stack2.FindMax(x => x is OperatorNode op ? op.Precedence : -1));
                if (!(stack2[index] is OperatorNode))
                    break;
                var left = stack2.PopAt(index - 1);
                var op0 = stack2.PopAt(index - 1);
                var right = stack2.PopAt(index - 1);
                stack2.Insert(index - 1, new BinaryExpression(){Left = left, Operator = op0, Right = right});
                
            }
            return stack2.PopAt(0);
        }

        IdentifierNode ParseIdentifier(ref ReadOnlySpan<char> str, Func<char, bool> filter)
        {
            var str2 = str;
            SkipWhitespace(ref str2);
            while (str2.Length > 0 && str2[0] != ' ' && filter(str2[0]))
            {
                str2 = str2.Slice(1);
            }

            if (str2 == str)
                return null;
            var identifier = new IdentifierNode(new string(str.Slice(0, str.Length - str2.Length).ToArray()));
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

        class IdentifierNode : AstNode
        {
            public readonly string Identifier;
            public IdentifierNode(string identifier) => Identifier = identifier;
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
            public AstNode Operator;
        }

        static OperatorNode MultiplyOp = new OperatorNode("*", 5);
        static OperatorNode AdditionOp = new OperatorNode("+", 3);
        static OperatorNode DivideOp = new OperatorNode("/", 4);
        static OperatorNode SubtractOp = new OperatorNode("-", 2);
        static OperatorNode AssignmentOp = new OperatorNode("=", 1.5);
        static OperatorNode StatementOperator = new OperatorNode(";", 1);
        static OperatorNode LessOperator = new OperatorNode("<", 1.8);
        static OperatorNode GreaterOperator = new OperatorNode(">", 1.8);
        static OperatorNode LessOrEqualOperator = new OperatorNode("<=", 1.8);
        static OperatorNode GreaterOrEqualOperator = new OperatorNode(">=", 1.8);
        static OperatorNode EqualOperator = new OperatorNode("==", 1.5);
        
        private static OperatorNode[] _operators = null;
        private static OperatorNode[] operators
        {
            get
            {
                return _operators ??= typeof(ExpressionStep).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                    .Select(x => x.GetValue(null)).OfType<OperatorNode>().ToArray();
            }
        }
    }
}