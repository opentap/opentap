namespace OpenTap.Expressions
{
    /// <summary> A node that contains an operator. See the Operators class for instances. </summary>
    class OperatorNode : AstNode
    {
        ///<summary> The operator itself, e.g '*' </summary> 
        public readonly string Operator;
        
        /// <summary> The precedence of the operator in relation to other operators. </summary>
        public readonly double Precedence;
        
        public OperatorNode(string op, double precedence)
        {
            Operator = op;
            Precedence = precedence;
        }

        public override string ToString() => $"{Operator}";
    }
}
