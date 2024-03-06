using System.Collections.Generic;
namespace OpenTap
{
    /// <summary> Provides a list of dynamic member types. Use DynamicMember.DeclaringType to control which types the member applies to e.g ITestStep. </summary>
    public interface IDynamicMemberProvider
    {
        /// <summary> Gets the list of dynamic members provided. </summary>
        IEnumerable<DynamicMember> GetDynamicMembers();
    }
}
