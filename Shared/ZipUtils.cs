using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace OpenTap
{
    static class ZipUtils
    {
        private static PropertyInfo _externalAttributes = null;
        public static void FixUnixPermissions(this ZipArchiveEntry entry)
        {
            // This API is only available on .NET 6 and above -- access it via reflection if possible
            // entry.ExternalAttributes = entry.ExternalAttributes | (Convert.ToInt32("664", 8) << 16);

            if (OperatingSystem.Current != OperatingSystem.Windows)
            {
                _externalAttributes ??= typeof(ZipArchiveEntry).GetProperty("ExternalAttributes");
                // user: read/write
                // group: read
                // other: read
                var flags = Convert.ToInt32("644", 8) << 16;
                var current = (int)_externalAttributes.GetValue(entry);
                _externalAttributes.SetValue(entry, current | flags);
            }
        }
    }
}
