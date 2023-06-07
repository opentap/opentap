namespace OpenTap.Expressions
{
    class BinaryExpression : AstNode
    {
        public AstNode Left;
        public AstNode Right;
        public OperatorNode Operator;
    }
}
