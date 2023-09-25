namespace OpenTap
{
    /// <summary>
    /// Used for notifying the serializer if a type is used which should cause a package dependency.
    /// </summary>
    public interface ISerializeNotifyAdditionalTypesUsed
    {
        /// <summary>
        /// The additional types used.
        /// </summary>
        public ITypeData[] AdditionalTypes { get; }
    }
}
