namespace OpenTap.Expressions
{
    /// <summary> An object node can contain a symbol, string or number. </summary>
    class ObjectNode : AstNode
    {
        public readonly string Content;
        
        public readonly bool IsLiteralString;
        public ObjectNode(string content, bool isLiteralString = false) => (Content, IsLiteralString) = (content, isLiteralString);

        public override string ToString() => $"{Content}";
    }
}
