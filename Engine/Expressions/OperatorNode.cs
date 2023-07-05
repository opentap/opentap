namespace OpenTap.Expressions
{
    class OperatorNode : AstNode
    {
        public readonly string Operator;
        public OperatorNode(string op, double precedence)
        {
            Operator = op;
            Precedence = precedence;
        }

        public readonly double Precedence;

        public override string ToString() => $"{Operator}";
    }
}
