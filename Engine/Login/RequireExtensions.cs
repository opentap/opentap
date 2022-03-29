using System;
using OpenTap.Cli;

namespace OpenTap.Authentication
{
    static class RequireExtensions
    {
        public static void MustBeDefined(this ICliAction obj, string propertyName)
        {
            if (null == TypeData.GetTypeData(obj).GetMember(propertyName).GetValue(obj))
                throw new ArgumentException(propertyName + " must be defined.");
        }
    }
}