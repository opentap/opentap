using System;
using System.Collections.Generic;
using System.Reflection;
namespace OpenTap.Expressions
{
    /// <summary>
    /// Provides expression functions.
    /// </summary>
    public interface IExpressionFunctionProvider
    {
        /// <summary> Returns the list of members for this function provider. </summary>
        /// <returns></returns>
        IEnumerable<MemberInfo> GetMembers();
        
    }
}
