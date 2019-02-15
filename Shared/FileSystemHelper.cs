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

        static string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern).
                Replace("\\*", ".*").
                Replace("\\?", ".") + "$";
        }

        public static IEnumerable<string> ExpandWildcards(string path)
        {
            if (path == null)
                throw new ArgumentNullException("path");
            var splitted = path.Split('\\');
            if (splitted.Last().Contains('*'))
            {
                string dir = String.Join("\\", splitted.Take(splitted.Length - 1));
                var reg = new Regex(WildcardToRegex(splitted.Last()));
                try
                {
                    var files = Directory.GetFiles(dir, "*")
                        .Where(file =>
                        {
                            var name = Path.GetFileName(file);
                            return reg.Match(name).Success;
                        });
                    return files;
                }
                catch (DirectoryNotFoundException)
                {
                    Console.WriteLine(String.Format("Directory {0} not found", dir));
                }
                return new string[0];

            }
            else if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
            {
                return Directory.GetFiles(path);
            }
            else
            {
                return new string[] { path };
            }
        }

        public static void CheckPathIsRelative(string path)
        {
            if (Path.IsPathRooted(path))
                throw new ArgumentException("Paths must be relative.");
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
            private readonly FileStream io;
            private readonly Encoding encoding;

            private long outPos = 0, outSize = 0;

            private int outBufSize = 0;
            private byte[] outBuffer = new byte[64 * 1024];

            private long inPos = 0;
            private bool inGotEnd = false;

            private int inBufOffset = 0, inBufSize = 0;
            private byte[] inBuffer = new byte[64 * 1024];

            private byte[] lineEnding;
            private int lineEndingSize;

            public void Close()
            {
                // Flush
                if (outBufSize > 0)
                {
                    io.Seek(outPos, SeekOrigin.Begin);
                    io.Write(outBuffer, 0, outBufSize);

                    outPos += outBufSize;
                    outSize += outBufSize;
                }
                
                // Truncate
                io.SetLength(outSize);
                io.Close();
            }

            private void EnsureSpace(long bytes)
            {
                // Compact the input buffer
                if (!inGotEnd && (inBufOffset > 0))
                {
                    Array.Copy(inBuffer, inBufOffset, inBuffer, 0, inBufSize);
                    inBufOffset = 0;
                }

                while (!inGotEnd && ((outPos + bytes >= inPos) || (inBufSize == 0)))
                {
                    if (inBufSize + inBufOffset >= inBuffer.Length)
                        Array.Resize(ref inBuffer, inBuffer.Length * 4 / 3);

                    io.Seek(inPos, SeekOrigin.Begin);
                    var cnt = io.Read(inBuffer, inBufOffset + inBufSize, inBuffer.Length - (inBufOffset + inBufSize));

                    if (cnt == 0)
                    {
                        // We are at the end of file
                        inGotEnd = true;
                        break;
                    }
                    else
                    {
                        inBufSize += cnt;
                        inPos += cnt;
                    }
                }
            }

            private int FindLineEnding()
            {
                int scanOffset = 0;

                // Keep trying 
                while (true)
                {
                    if (inGotEnd && (inBufSize <= 0)) return -1;

                    int idx = -1;

                    // If we got space to check
                    if (inBufSize - scanOffset - lineEndingSize + 1 > 0)
                        idx = Array.IndexOf<byte>(inBuffer, lineEnding[0], inBufOffset + scanOffset, inBufSize - scanOffset - lineEndingSize + 1); // The count calculation ignores the length of the lineending

                    if (idx >= 0)
                    {
                        bool match = true;

                        // Check that the lineending is a full match
                        for (int i = 1; i < lineEndingSize; i++)
                            if (inBuffer[idx + i] != lineEnding[i])
                            {
                                match = false;
                                break;
                            }

                        // If not then we need to skip and look after the first character match
                        if (!match)
                        {
                            scanOffset = (idx - inBufOffset) + 1;
                            continue;
                        }

                        // Otherwise we got it
                        return idx - inBufOffset;
                    }

                    // Grow buffer if needed
                    if (inBufSize + inBufOffset >= inBuffer.Length)
                        Array.Resize(ref inBuffer, inBuffer.Length * 4 / 3);

                    // Read into buffer
                    io.Seek(inPos, SeekOrigin.Begin);
                    var cnt = io.Read(inBuffer, inBufOffset + inBufSize, inBuffer.Length - (inBufOffset + inBufSize));

                    if (cnt == 0)
                    {
                        // We are at the end of file
                        inGotEnd = true;
                        return -1;
                    }
                    else
                    {
                        inBufSize += cnt;
                        inPos += cnt;
                    }
                }
            }

            public string ReadLine()
            {
                var ending = FindLineEnding();

                if (ending < 0)
                {
                    if (inBufSize <= 0)
                        return null;
                    else
                    {
                        var res = encoding.GetString(inBuffer, inBufOffset, inBufSize);
                        inBufSize = 0;
                        return res;
                    }
                }
                else
                {
                    var res = encoding.GetString(inBuffer, inBufOffset, ending);
                    inBufOffset += ending + lineEndingSize;
                    inBufSize -= ending + lineEndingSize;

                    if (inBufSize <= 0)
                        EnsureSpace(1024);

                    return res;
                }
            }

            public void WriteLine(string line)
            {
                var bytesNeeded = encoding.GetByteCount(line) + lineEndingSize;

                // Find out whether we need to flush
                if (outBufSize + bytesNeeded > outBuffer.Length)
                {
                    EnsureSpace(outBufSize);

                    io.Seek(outPos, SeekOrigin.Begin);
                    io.Write(outBuffer, 0, outBufSize);

                    outPos += outBufSize;
                    outSize += outBufSize;

                    outBufSize = 0;
                }

                // Find out if we need a bigger buffer, if yes it's already flushed
                if (bytesNeeded > outBuffer.Length)
                    outBuffer = new byte[bytesNeeded];

                outBufSize += encoding.GetBytes(line, 0, line.Length, outBuffer, outBufSize);

                Array.Copy(lineEnding, 0, outBuffer, outBufSize, lineEndingSize);
                outBufSize += lineEndingSize;
            }

            public LineModifier(string filename, Encoding encoding)
            {
                io = File.Open(filename, FileMode.Open, FileAccess.ReadWrite);
                this.encoding = encoding;

                lineEnding = encoding.GetBytes(Environment.NewLine);
                lineEndingSize = lineEnding.Length;

                // Prime the input buffer
                EnsureSpace(1024);
            }
        }

        /// <summary> 
        /// Modifies each line in a file by using the modify function.
        /// It does so in-place and out of memory, so large files can be modified.
        /// </summary>
        /// <param name="file"> The file that should be modified.</param>
        /// <param name="modify">The function that can modify each line.</param>
        public static void ModifyLines(string file, Func<string, int, string> modify)
        {
            LineModifier lm = new LineModifier(file, Encoding.UTF8);
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
