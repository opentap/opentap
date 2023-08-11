namespace OpenTap
{ 
    /// <summary>  A mixin builder defines how to create and apply the instance of a specific mixin. </summary>
    public interface IMixinBuilder
    {
        /// <summary> Creates the member for the mixin. </summary>
        /// <param name="targetType"></param>
        /// <returns></returns>
        MixinMemberData ToDynamicMember(ITypeData targetType);
    }
}