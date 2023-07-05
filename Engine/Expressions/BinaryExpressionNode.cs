namespace OpenTap.Expressions
{
    class BinaryExpressionNode : AstNode
    {
        public AstNode Left;
        public AstNode Right;
        public OperatorNode Operator;
    }
}
