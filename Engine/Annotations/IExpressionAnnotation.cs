namespace OpenTap
{
    /// <summary>  Annotation added when a setting has an expression. </summary>
    public interface IExpressionAnnotation : IAnnotation
    {
        /// <summary>  The current expression value. </summary>
        public string Expression { get; set; }
        
        /// <summary> Gets the current expression error. </summary>
        public string Error { get; }
    }

}
