namespace OpenTap.Expressions
{
    class ObjectNode : AstNode
    {
        public string Data;
        public bool IsString;
        public ObjectNode(string data) => Data = data;
    }
}
