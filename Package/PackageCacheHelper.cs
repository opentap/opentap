//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.IO;

namespace OpenTap.Package
{
    internal static class PackageCacheHelper
    {
        public static string PackageCacheDirectory { get; private set; } = Path.Combine(ExecutorClient.ExeDir, "PackageCache");
        readonly static TraceSource log =  OpenTap.Log.CreateSource("PackageCache");

        static PackageCacheHelper()
        {
            Directory.CreateDirectory(PackageCacheDirectory);
        }
        internal static bool PackageIsFromCache(PackageDef package)
        {
            return (package.PackageSource as IRepositoryPackageDefSource)?.RepositoryUrl.StartsWith(PackageCacheDirectory) == true;
        }
        
        internal static void CachePackage(string filename)
        {
            if (!File.Exists(filename))
                throw new FileNotFoundException(filename);

            string newFilename = Path.Combine(PackageCacheDirectory, Path.GetFileName(filename));

            if (filename == newFilename)
                return;

            File.Copy(filename, newFilename, true);
            log.Debug("Package cached in {0}.", newFilename);
        }

        internal static void ClearCache()
        {
            Directory.Delete(PackageCacheDirectory, true);
        }
    }
}
