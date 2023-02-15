namespace OpenTap
{
    /// <summary>
    /// Allows a serializer plugin to specify if it is needed for deserialization.
    /// This can affect the package dependencies of test plans as the serializers used by the test plan
    /// are otherwise always dependencies of the test plan itself.
    /// </summary>
    public interface ITapSerializerPluginDependencyMarker : ITapSerializerPlugin
    {
        /// <summary> Gets if the serializer is needed to deserialize.
        /// If set to false, the serializer will not mark it as having been used. </summary>
        bool NeededForDeserialization { get; }
    }
}
