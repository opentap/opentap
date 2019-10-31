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

        /// <summary>
        /// Similar to Directory.EnumerateFiles but will ignore any UnauthorizedAccessException or PathTooLongException that occur while walking the directory tree.
        /// </summary>
        public static IEnumerable<string> IterateDirectories(string rootPath, string patternMatch, SearchOption searchOption)
        {
            if (searchOption == SearchOption.AllDirectories)
            {
                IEnumerable<string> subDirs = Array.Empty<string>();
                try
                {
                    subDirs = Directory.EnumerateDirectories(rootPath);
                }
                catch (UnauthorizedAccessException) { }
                catch (PathTooLongException) { }

                foreach (var dir in subDirs)
                {
                    foreach (var f in IterateDirectories(dir, patternMatch, searchOption))
                        yield return f;
                }
            }

            IEnumerable<string> files = Array.Empty<string>();
            try
            {
                files = Directory.EnumerateFiles(rootPath, patternMatch);
            }
            catch (UnauthorizedAccessException) { }

            foreach (var file in files)
                yield return file;
        }

        static bool compareFileStreams(FileStream f1, FileStream f2)
        {
            if (f1.Length != f2.Length) return false;
            const int bufferSize = 4096;
            const int u64len1 = bufferSize / 8;
            byte[] buffer1 = new byte[bufferSize];
            byte[] buffer2 = new byte[bufferSize];
            while (true)
            {
                int count = f1.Read(buffer1, 0, bufferSize);
                if (count == 0) return true;
                f2.Read(buffer2, 0, bufferSize);

                int u64len = u64len1;
                if (count < bufferSize)
                    u64len = (count / 8 + 1);

                for (int i = 0; i < u64len; i++)
                {
                    if (BitConverter.ToInt64(buffer1, i * 8) != BitConverter.ToInt64(buffer2, i * 8))
                        return false;
                }
            }
        }

        public static bool CompareFiles(string file1, string file2)
        {
            if (PathUtils.AreEqual(file1, file2))
                return true;
            try
            {
                using (var f1 = File.Open(file1, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var f2 = File.Open(file2, FileMode.Open, FileAccess.Read, FileShare.Read))
                    return compareFileStreams(f1, f2);
            }
            catch
            {
                return false;
            }
        }
    }
}
