using System;
using System.Collections.Generic;
using System.Text;

namespace OpenTap
{
    /// <summary> Specialization of <see cref="IIconAnnotation"/> that represents a reference to a setting on another TestStep.</summary>
    public interface ISettingReferenceIconAnnotation : IIconAnnotation
    {
        /// <summary>
        /// The TestStep that holds the setting beeing referenced
        /// </summary>
        Guid TestStepReference { get; }
        /// <summary>
        /// The name of the setting being referenced
        /// </summary>
        string MemberName { get; }
    }
}
