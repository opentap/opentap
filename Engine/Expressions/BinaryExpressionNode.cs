namespace OpenTap.Expressions
{
    class BinaryExpressionNode : AstNode
    {
        public AstNode Left;
        public AstNode Right;
        public OperatorNode Operator;

        public override string ToString()
        {
            if (Operator == Operators.Call)
                return $"{Left}({Right})";
            return $"{Left} {Operator} {Right}";
        }
    }
}
