using System;

namespace OpenTap.Package
{
    interface IPackageDownloadProgress
    {
        Action<string, long, long> OnProgressUpdate { get; set; }
    }
}