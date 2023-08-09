namespace OpenTap.Expressions
{
    /// <summary> Binary expressions AST node. This describes the application of a binary operator for example '*' in 5 * 4.</summary>
    sealed class BinaryExpressionNode : AstNode
    {
        /// <summary> Left side of the operation. </summary>
        public AstNode Left;
        /// <summary> Right side of the operation </summary>
        public AstNode Right;
        
        /// <summary> The operator used. See the Operators class. </summary>
        public OperatorNode Operator;

        public override string ToString()
        {
            if (Operator == Operators.Call)
                return $"{Left}({Right})";
            return $"{Left} {Operator} {Right}";
        }
    }
}
