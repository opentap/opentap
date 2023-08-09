namespace OpenTap.Expressions
{
    /// <summary> A node that contains an operator. See the Operators class for instances. </summary>
    sealed class OperatorNode : AstNode
    {
        ///<summary> The operator itself, e.g '*' </summary> 
        public string Operator { get; }
        
        /// <summary> The precedence of the operator in relation to other operators. </summary>
        public double Precedence{ get; }
        
        public OperatorNode(string op, double precedence)
        {
            Operator = op;
            Precedence = precedence;
        }

        public override string ToString() => $"{Operator}";
    }
}
