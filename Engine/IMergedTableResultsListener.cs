namespace OpenTap
{
    /// <summary> This interface adds support for optimized/merged result tables. This can provide a performance boost in situations where many rows of the same kinds of results are published. </summary>
    public interface IMergedTableResultListener : IResultListener
    {
        /// <summary>
        /// Gets if the result table supports merged result tables. If false or if this interface is not implemented result tables will not be merged.
        /// </summary>
        public bool SupportsMergedResults { get; }
    
    }
}
