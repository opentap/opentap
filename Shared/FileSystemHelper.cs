//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Text;
using System.Reflection;

namespace OpenTap
{
    /// <summary>
    /// Utility class to help with common file system operations.
    /// </summary>
    class FileSystemHelper
    {
        static void deleteAllFiles(string target_dir)
        {
            foreach (string file in Directory.GetFiles(target_dir))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
        }

        static void deleteAllDirectories(string target_dir)
        {
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }
        }
        /// <summary>
        /// Deletes a directory with files.
        /// </summary>
        /// <param name="target_dir"></param>
        public static void DeleteDirectory(string target_dir)
        {
            if (target_dir == null)
                throw new ArgumentNullException("target_dir");
            try
            {
                deleteAllFiles(target_dir);
                deleteAllDirectories(target_dir);
                Directory.Delete(target_dir, false);
            }
            catch (Exception)
            {

            }
        }

        public static void SafeDelete(string file, int retries, Action<int, Exception> onError)
        {
            for(int i = 0; i < retries; i++)
            {
                try
                {
                    File.Delete(file);
                    break;
                }
                catch(Exception e)
                {
                    if (e is DirectoryNotFoundException)
                    {
                        // this occurs if the directory of the file being deleted does not exist.
                        // But if the directory is not found it also means that the file does not exist.
                        // so we can safely assume it is deleted.
                        break;
                    }
                    onError(i, e);
                }
            }
        }

        /// <summary>
        /// Creates a directory if it does not already exist.
        /// </summary>
        /// <param name="filePath"></param>
        public static void EnsureDirectoryOf(string filePath)
        {
            if (!Directory.Exists(Path.GetDirectoryName(filePath)) && string.IsNullOrWhiteSpace(Path.GetDirectoryName(filePath)) == false)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            }
        }

        /// <summary>
        /// Creates a temporary directory.
        /// </summary>
        /// <returns> Path to the temporary directory.</returns>
        public static string CreateTempDirectory()
        {
            string path = Path.Combine(System.IO.Path.GetTempPath(), Path.GetRandomFileName());
            EnsureDirectoryOf(path);
            return path;
        }

        public static string CreateTempFile(string extension)
        {
            return Path.Combine(System.IO.Path.GetTempPath(), Path.GetRandomFileName()) + extension;
        }

        public static string GetCurrentInstallationDirectory()
        {
            return ExecutorClient.ExeDir;
        }

        /// <summary>
        /// Compares two paths to get the relative between base and end. The string has to be a standard file system string like "C:\Program Files\...".
        /// </summary>
        /// <param name="baseDirectory"></param>
        /// <param name="endDirectory"></param>
        /// <returns></returns>
        internal static string GetRelativePath(string baseDirectory, string endDirectory)
        {
            var baseSplit = baseDirectory.Split('\\');
            var endSplit = endDirectory.Split('\\');
            int idx = getSameIndex(baseSplit, endSplit);
            var dots = String.Join("\\", baseSplit.Skip(idx).Select(v => ".."));
            var afterDots = string.Join("\\", endSplit.Skip(idx));

            string temp = System.IO.Path.Combine(dots, afterDots);

            //If the resulting directory is empty, put in a dot.  This allows the hover help to work
            return temp == string.Empty ? "." : temp;
        }

        static int getSameIndex(string[] a, string[] b)
        {
            int end = Math.Min(a.Length, b.Length);
            for (int i = 0; i < end; i++)
            {
                if (a[i] != b[i])
                {
                    return i;
                }
            }
            return end;
        }

        public static string GetAssemblyVersion(string assemblyPath)
        {
            if (assemblyPath == null)
                throw new ArgumentNullException("assemblyPath");
            assemblyPath = Path.GetFullPath(assemblyPath); // this is important to make sure that we take the version number from the file in the current directory, and not the one in TAP_PATH
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(assemblyPath);
            return info.ProductVersion;
        }

        internal static byte[] ByteOrderMark = new byte[] { 0xEF, 0xBB, 0xBF };
        public static string EscapeBadPathChars(string path)
        {
            try
            {
                new FileInfo(path);
            }
            catch
            {
                var fname = Path.GetFileName(path);
                var dirname = Path.GetDirectoryName(path);
                
                var pathchars = Path.GetInvalidPathChars();
                foreach (var chr in pathchars)
                {
                    dirname = dirname.Replace(chr, '_');
                }

                var filechars = Path.GetInvalidFileNameChars();
                foreach (var chr in filechars)
                {
                    fname = fname.Replace(chr, '_');
                }

                path = Path.Combine(dirname, fname);

                new FileInfo(path);
            }
            return path;
        }

        public static string CreateUniqueFileName(string path)
        {
            if (!File.Exists(path)) return path;

            var dir = Path.GetDirectoryName(path);
            var ext = Path.GetExtension(path);
            var name = Path.GetFileNameWithoutExtension(path);

            int i = 2;

            // Just put some upper bound on this
            while (i < 100000)
            {
                var newPath = Path.Combine(dir, name + " (" + i + ")" + ext);

                if (!File.Exists(newPath)) return newPath;
                i++;
            }

            return path;
        }
    }
}
