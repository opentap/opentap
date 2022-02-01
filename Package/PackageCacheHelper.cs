//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.IO;

namespace OpenTap.Package
{
    static class PackageCacheHelper
    {
        // ApplicationDataLocal is meant for storing data for the local(non-roaming) user. E.g, if the user logs
        // into another machine that data should not be copied over. It needs to be a location that can be written to
        // by the current user without any additional rights - hence it cannot be truly system-wide.
        // Resolves to:
        // - Linux: /home/<USER>/.local/share/OpenTAP/PackageCache
        // - Windows: C:\Users\<USER>\AppData\Local\OpenTAP\PackageCache
        public static string PackageCacheDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), "OpenTap", "PackageCache");
        readonly static TraceSource log =  Log.CreateSource("PackageCache");

        static PackageCacheHelper()
        {
            Directory.CreateDirectory(PackageCacheDirectory);
        }

        internal static string GetCacheFilePath(PackageDef package)
        {
            var packageName = PackageActionHelpers.GetQualifiedFileName(package).Replace('/', '.'); // TODO: use the hash in this name, to be able to handle cross repo name clashes.
            return Path.Combine(PackageCacheHelper.PackageCacheDirectory, packageName);
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
            Directory.CreateDirectory(PackageCacheDirectory);
        }
    }
}
