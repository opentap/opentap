using System.Linq;
using System.Reflection;
namespace OpenTap.Expressions
{
    class Operators
    {
        // operators are defined by a string an precedence.
        
        public static readonly OperatorNode Addition = new OperatorNode("+", 3);
        public static readonly OperatorNode Divide = new OperatorNode("/", 4);
        public static readonly OperatorNode Multiply = new OperatorNode("*", 4);
        public static readonly OperatorNode Subtract = new OperatorNode("-", 3);
        
        public static readonly OperatorNode Less = new OperatorNode("<", 1.8);
        public static readonly OperatorNode Greater = new OperatorNode(">", 1.8);
        public static readonly OperatorNode LessOrEqual = new OperatorNode("<=", 1.8);
        public static readonly OperatorNode GreaterOrEqual = new OperatorNode(">=", 1.8);
        public static readonly OperatorNode Equal = new OperatorNode("==", 1.8);
        public static readonly OperatorNode NotEqual = new OperatorNode("!=", 1.8);
        public static readonly OperatorNode And = new OperatorNode("&&", 1.7);
        public static readonly OperatorNode Or = new OperatorNode("||", 1.7);
        
        // comma is used for combining arguments for use by call.
        public static readonly OperatorNode Comma = new OperatorNode(",", 1.6);
        
        // Some special operators are newer used directly, but figures in the AST:
        
        // This operator is for concatenating strings. normally not used, but it is used for interpolations.
        public static readonly OperatorNode StrCombine = new OperatorNode("[strcombine]", 1.7);
        // This operator is for calling functions. AST-wise, it looks like this:(funcname [call] (arg1 , (arg2 , ...)))
        public static readonly OperatorNode Call = new OperatorNode("[call]", 1.7);
        
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
