namespace OpenTap
{
    /// <summary>  An ITypeData that has a source property. </summary>
    public interface ITypeDataWithSource : ITypeData
    {
        /// <summary> The source of the type data. Specifying where it came from. This may be null. </summary>
        ITypeDataSource Source { get; }
    }
}