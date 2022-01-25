using System;
using System.Collections.Generic;
using System.Text;

namespace OpenTap
{
    public interface ISettingReferenceIconAnnotation : IIconAnnotation
    {
        Guid TestStepReference { get; }
        string MemberName { get; }
    }
}
