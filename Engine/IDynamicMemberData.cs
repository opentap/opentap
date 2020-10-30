namespace OpenTap
{
    /// <summary>  A dynamic member data is a member data that can be detached from the owner member. </summary>
    public interface IDynamicMemberData : IMemberData
    {
        /// <summary>  Returns true once the member can be safely ignored. </summary>
        bool IsDisposed { get; }
    }
}