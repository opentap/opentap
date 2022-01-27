namespace OpenTap
{
    /// <summary>
    ///  for reverse caching of parameters. (source/member => parameter).f
    /// </summary>
    interface IParameterizedMembersCache
    {
        void RegisterParameterizedMember(IMemberData mem, ParameterMemberData memberData);
        void UnregisterParameterizedMember(IMemberData mem, ParameterMemberData memberData);
        ParameterMemberData GetParameterFor(IMemberData mem);
    }

    /// <summary>
    /// For accessing IParameterizedMembersCaches.
    /// </summary>
    static class ParameterizedMembersCache
    {
        public static (ParameterMemberData, ITestStepParent) GetParameterFor(ITestStepParent step, IMemberData member)
        {
            if (step is IParameterizedMembersCache cache && cache.GetParameterFor(member) is ParameterMemberData p) // implemented by TestStep.
                return (p, p.Target as ITestStepParent);
                
            var source =  step;
            while (source != null)
            {
                source = source.Parent;
                if (member.GetParameter(source, step) is ParameterMemberData p2)
                    return (p2, source);
            }

            return (null, null);
        }
    }
    
}