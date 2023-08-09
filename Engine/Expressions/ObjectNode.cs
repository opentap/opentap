namespace OpenTap.Expressions
{
    /// <summary> An object node can contain a symbol, string or number. </summary>
    sealed class ObjectNode : AstNode
    {
        public string Content { get; }
        public bool IsLiteralString { get; }
        public ObjectNode(string content, bool isLiteralString = false) => (Content, IsLiteralString) = (content, isLiteralString);

        public override string ToString() => $"{Content}";
    }
}
