namespace OpenTap
{
    /// <summary>
    /// For interactive icons that should be advertised on annotation collections.
    /// </summary>
    public interface IInteractiveIconAnnotation : IIconAnnotation{
        // this inner annotation should be fetched from the MenuAnnotation property associated with IconName in a lazy fashion.
        /// <summary> Reads the sub annotation representing the interactive icon action. It should contain an IMethodAnnotation. </summary>
        AnnotationCollection Action { get; } 
    }
}