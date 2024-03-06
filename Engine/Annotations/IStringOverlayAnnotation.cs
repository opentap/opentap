namespace OpenTap
{
    /// <summary> Annotation added when a setting has a string overlay (Expression). </summary>
    public interface IStringOverlayAnnotation : IAnnotation
    {
        /// <summary>  The current overlay value. </summary>
        public string OverlayString { get; set; }
        
        /// <summary> Gets the current overlay error. </summary>
        public string Error { get; }
    }
}
