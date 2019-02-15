//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OpenTap.Package.SetAsmInfo
{
    internal class IL
    {
        private static OpenTap.TraceSource log = Log.CreateSource("IlAsm");

        private static IEnumerable<string> FindPathForWindowsSdk()
        {
            string[] windowsSdkPaths = new[]
            {
                @"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\",
                @"Microsoft SDKs\Windows\v10.0A\bin\",
                @"Microsoft SDKs\Windows\v8.0A\bin\NETFX 4.0 Tools\",
                @"Microsoft SDKs\Windows\v8.0A\bin\",
                @"Microsoft SDKs\Windows\v8.0\bin\NETFX 4.0 Tools\",
                @"Microsoft SDKs\Windows\v8.0\bin\",
                @"Microsoft SDKs\Windows\v7.1A\bin\NETFX 4.0 Tools\",
                @"Microsoft SDKs\Windows\v7.1A\bin\",
                @"Microsoft SDKs\Windows\v7.0A\bin\NETFX 4.0 Tools\",
                @"Microsoft SDKs\Windows\v7.0A\bin\",
                @"Microsoft SDKs\Windows\v6.1A\bin\",
                @"Microsoft SDKs\Windows\v6.0A\bin\",
                @"Microsoft SDKs\Windows\v6.0\bin\",
                @"Microsoft.NET\FrameworkSDK\bin"
            };

            foreach (var possiblePath in windowsSdkPaths)
            {
                string fullPath = string.Empty;

                // Check alternate program file paths as well as 64-bit versions.
                if (Environment.Is64BitProcess)
                {
                    fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), possiblePath, "x64");
                    if (Directory.Exists(fullPath))
                    {
                        yield return fullPath;
                    }

                    fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), possiblePath, "x64");
                    if (Directory.Exists(fullPath))
                    {
                        yield return fullPath;
                    }
                }

                fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), possiblePath);
                if (Directory.Exists(fullPath))
                {
                    yield return fullPath;
                }

                fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), possiblePath);
                if (Directory.Exists(fullPath))
                {
                    yield return fullPath;
                }
            }
        }

        // ILAsm.exe will be somewhere in here
        private static IEnumerable<string> FindPathForDotNetFramework()
        {
            string[] frameworkPaths = new[]
            {
                @"Microsoft.NET\Framework\v4.0.30319",
                @"Microsoft.NET\Framework\v2.0.50727"
            };

            foreach (var possiblePath in frameworkPaths)
            {
                if (Environment.Is64BitProcess)
                {
                    var fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), possiblePath.Replace(@"\Framework\", @"\Framework64\"));

                    if (Directory.Exists(fullPath))
                        yield return fullPath;
                }

                {
                    var fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), possiblePath);
                    if (Directory.Exists(fullPath))
                        yield return fullPath;
                }
            }
        }

        private static bool TestPath(string v1, string v2, out string path)
        {
            path = "";
            if (string.IsNullOrEmpty(v1)) return false;

            path = Path.Combine(v1, v2);
            return File.Exists(path);
        }

        private static string LocateILDAsm(string toolName)
        {
            string path;

            foreach(var p in FindPathForDotNetFramework())
                if (TestPath(p, toolName, out path))
                    return path;
            foreach(var p in FindPathForWindowsSdk())
                if (TestPath(p, toolName, out path))
                    return path;

            return null;
        }

        static string ildasm = LocateILDAsm("ildasm.exe");
        static string ilasm = LocateILDAsm("ilasm.exe");

        public static bool Assemble(string filename, string target, bool isDll, params string[] inputResources)
        {
            var ec = RunProgram(ilasm, string.Format("{3} \"{1}\" {2} /quiet /nologo /ssver=6.0 /output=\"{0}\"", target, filename, string.Join(" ", inputResources.Where(File.Exists).Select(x => "/RESOURCE=\"" + x + "\"")), isDll ? "/DLL" : "/EXE"));
            return ec == 0;
        }

        public static bool Disassemble(string filename, string target)
        {
            var ec = RunProgram(ildasm, string.Format("/TEXT /NOBAR /RAWEH /QUOTEALLNAMES /UTF8 /FORWARD /OUT=\"{0}\" /UTF8 \"{1}\"", target, filename));
            return ec == 0;
        }

        internal static int RunProgram(string program, string arguments)
        {
            //log.Debug("Running '{0}' with '{1}'", program, arguments);

            ProcessStartInfo pi = new ProcessStartInfo(program, arguments);
            pi.CreateNoWindow = true;
            pi.UseShellExecute = false;

            //pi.RedirectStandardError = true;
            //pi.RedirectStandardOutput = true;

            var p = Process.Start(pi);

            //p.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log.Error(e.Data); };
            //p.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log.Info(e.Data); };

            //p.BeginErrorReadLine();
            //p.BeginOutputReadLine();

            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                log.Debug("Running '{0}' with '{1}'", program, arguments);
                log.Debug("Exitcode: {0}", p.ExitCode);
            }
            try
            {
                return p.ExitCode;
            }
            finally
            {
                p.Close();
            }
        }
    }
}
