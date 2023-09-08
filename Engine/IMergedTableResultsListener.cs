namespace OpenTap
{
    /// <summary> This interface adds support for optimized/merged result tables. This can provide a performance boost in situations where many rows of the same kinds of results are published. </summary>
    public interface IMergedTableResultListener : IResultListener
    {
        
    }
}
