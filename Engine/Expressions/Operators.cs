using System.Linq;
using System.Reflection;
namespace OpenTap.Expressions
{
    class Operators
    {
        // operators are defined by a string an precedence.
        public static readonly OperatorNode CommaOp = new OperatorNode(",", 1.6);
        public static readonly OperatorNode AdditionOp = new OperatorNode("+", 3);
        public static readonly OperatorNode DivideOp = new OperatorNode("/", 4);
        public static readonly OperatorNode MultiplyOp = new OperatorNode("*", 4);
        public static readonly OperatorNode SubtractOp = new OperatorNode("-", 3);
        public static readonly OperatorNode AssignmentOp = new OperatorNode("=", 1.5);
        public static readonly OperatorNode StatementOperator = new OperatorNode(";", 1);
        public static readonly OperatorNode LessOperator = new OperatorNode("<", 1.8);
        public static readonly OperatorNode GreaterOperator = new OperatorNode(">", 1.8);
        public static readonly OperatorNode LessOrEqualOperator = new OperatorNode("<=", 1.8);
        public static readonly OperatorNode GreaterOrEqualOperator = new OperatorNode(">=", 1.8);
        public static readonly OperatorNode EqualOperator = new OperatorNode("==", 1.8);
        public static readonly OperatorNode NotEqualOperator = new OperatorNode("!=", 1.8);
        public static readonly OperatorNode AndOperator = new OperatorNode("&&", 1.7);
        public static readonly OperatorNode OrOperator = new OperatorNode("||", 1.7);
        public static readonly OperatorNode StrCombineOperator = new OperatorNode("..", 1.7);
        
        static OperatorNode[] operators;
        public static OperatorNode[] GetOperators()
        {
            // Use reflection to fetch all the defined operators.
            return operators ??= typeof(Operators)
                .GetFields(BindingFlags.Static | BindingFlags.Public)
                .Select(x => x.GetValue(null))
                .OfType<OperatorNode>()
                // longest operators are first, so that '=' is not used instead of '=='.
                .OrderByDescending(o => o.Operator.Length)
                .ToArray();   
        }
    }
}
