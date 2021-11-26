namespace OpenTap
{
    /// <summary>
    ///  for reverse caching of parameters. (source/member => parameter).f
    /// </summary>
    interface IParameterizedMembersCache
    {
        void RegisterParameterizedMember(IMemberData mem, IParameterMemberData memberData);
        void UnregisterParameterizedMember(IMemberData mem, IParameterMemberData memberData);
        IParameterMemberData GetParameterFor(IMemberData mem);
    }
}