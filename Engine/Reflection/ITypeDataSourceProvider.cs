namespace OpenTap
{
    /// <summary> Used for a ITypeDataSearcher to identify a type as something that comes from it. </summary>
    public interface ITypeDataSourceProvider : ITypeDataSearcher
    {
        /// <summary>
        /// Gets the ITypeDataSource corresponding to a type data if this ITypeDataSourceProvider supports it. Otherwise it returns null.
        /// </summary>
        /// <param name="typeData"></param>
        /// <returns></returns>
        ITypeDataSource GetSource(ITypeData typeData);
    }
}