//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Tap.Shared
{
    internal class PathUtils
    {
        public class PathComparer : IEqualityComparer<string>
        {
            public bool Equals(string x, string y)
            {
                return NormalizePath(x) == NormalizePath(y);
            }

            public int GetHashCode(string obj)
            {
                return NormalizePath(obj).GetHashCode();
            }
        }

        public static string NormalizePath(string path)
        {
            var newPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                    return newPath.ToUpperInvariant();
            }

            return newPath;
        }

        public static bool AreEqual(string path1, string path2)
        {
            return NormalizePath(path1) == NormalizePath(path2);
        }
    }
}
