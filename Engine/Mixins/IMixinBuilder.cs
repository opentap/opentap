namespace OpenTap
{ 
    /// <summary>  A mixin builder defines how to create and apply the instance of a specific mixin. </summary>
    public interface IMixinBuilder
    {
        /// <summary> Initializes the mixin, providing the type of object to provide a mixin for. </summary>
        void Initialize(ITypeData targetType);
        /// <summary> Creates the member for the mixin. </summary>
        /// <param name="targetType"></param>
        /// <returns></returns>
        MixinMemberData ToDynamicMember(ITypeData targetType);

        /// <summary> Clones an instance of mixin builder. </summary>
        IMixinBuilder Clone();
    }
}