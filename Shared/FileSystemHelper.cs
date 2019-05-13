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

        /// <summary>
        /// Creates a directory if it does not already exist.
        /// </summary>
        /// <param name="filePath"></param>
        public static void EnsureDirectory(string filePath)
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
            EnsureDirectory(path);
            return path;
        }

        public static string CreateTempFile()
        {
            return Path.Combine(System.IO.Path.GetTempPath(), Path.GetRandomFileName());
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

        private class LineModifier
        {
            readonly string tempFile;
            readonly Stream outFile;
            readonly Stream infile;
            readonly StreamReader @in;
            readonly StreamWriter @out;

            public void Close()
            {
                @out.Flush();
                
                infile.Flush();
                infile.Seek(0, SeekOrigin.Begin);
                infile.SetLength(outFile.Length);

                outFile.Seek(0, SeekOrigin.Begin);
                outFile.CopyTo(infile, 1024 * 8);
                outFile.Close();
                File.Delete(tempFile);
            }


            public string ReadLine() => @in.ReadLine();
            public void WriteLine(string line) => @out.WriteLine(line);
            
            public LineModifier(Stream file, Encoding encoding)
            {
                tempFile = FileSystemHelper.CreateTempFile();

                outFile = File.Open(tempFile, FileMode.Create);
                infile = file;
                @in = new StreamReader(file, encoding, false, 1024 * 8);
                @out = new StreamWriter(outFile, encoding, 1024 * 8);
            }
        }

        /// <summary> 
        /// Modifies each line in a file by using the modify function.
        /// It does so in-place and out of memory, so large files can be modified.
        /// </summary>
        /// <param name="stream"> The file that should be modified.</param>
        /// <param name="pushBom">Whether a BOM should be prepended to the file.</param>
        /// <param name="modify">The function that can modify each line.</param>
        public static void ModifyLines(Stream stream, bool pushBom, Func<string, int, string> modify)
        {
            var lm = new LineModifier(stream, new UTF8Encoding(pushBom));
            
            try
            {
                int lineCount = 0;

                while (true)
                {
                    var line = lm.ReadLine();
                    if (line == null)
                        break;

                    line = modify(line, lineCount++) ?? line;

                    lm.WriteLine(line);
                }
            }
            finally
            {
                lm.Close();
            }
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
