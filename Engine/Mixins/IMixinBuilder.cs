namespace OpenTap
{ 
    /// <summary>  A mixin builder defines how to create and apply the instance of a specific mixin. </summary>
    public interface IMixinBuilder : ITapPlugin
    {
        /// <summary> Initializes the mixin, providing the type of object to provide a mixin for. </summary>
        void Initialize(ITypeData targetType);
        
        /// <summary> Creates the member for the mixin. </summary>
        MixinMemberData ToDynamicMember(ITypeData targetType);
    }
}