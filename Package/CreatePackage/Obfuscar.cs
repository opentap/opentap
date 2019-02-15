//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenTap.Package
{
    [Display("Built-in Obfuscar runner")]
    public class Obfuscar : IPackageObfuscator
    {
        private const string ExeFile = @"Obfuscar.Console.exe";
        private const bool UseWorkaround = true; // Set to true to fix an interface member renaming bug.

        private string ExePath = ExeFile;

        public bool CanTransform(PackageDef package)
        {
            string envVar = Environment.GetEnvironmentVariable("OBFUSCAR_PATH");
            if (!File.Exists(ExePath) && envVar != null) ExePath = Path.Combine(envVar, ExeFile);

            return File.Exists(ExePath);
        }

        public bool Transform(string tempDir, PackageDef package)
        {
            if (!CanTransform(package))
                return false;

            var extraPaths = new List<string>() { Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) };

            foreach (PackageFile file in package.Files)
            {
                if (file.DoObfuscate || !string.IsNullOrEmpty(file.LicenseRequired))
                {
                    string fullPath = Path.GetFullPath(file.FileName);
                    string obfuscated = Obfuscate(tempDir, fullPath, extraPaths);
                    file.FileName = obfuscated;
                }
            }

            return true;
        }
        
        private string Obfuscate(string tempDir, string fullPath, List<string> extraPaths)
        {
            var obfuscatedOutput = Path.Combine(tempDir, "Obfuscated");
            Directory.CreateDirectory(obfuscatedOutput);
            var res =
$@"<?xml version='1.0'?>
<Obfuscator>
  <Var name=""OutPath"" value=""{obfuscatedOutput}"" />
  {string.Format("<Module file=\"{0}\" />", fullPath)}
  {string.Join(Environment.NewLine, extraPaths.Distinct().Select(f => string.Format("<AssemblySearchPath path=\"{0}\" />", f)))}
  <Var name=""KeepPublicApi"" value=""true"" />
  <Var name=""HidePrivateApi"" value=""true"" />
</Obfuscator>";

            var scriptFile = Path.Combine(tempDir, Path.GetFileName(fullPath) + "-SCRIPTFILE");
            Directory.CreateDirectory(Path.GetDirectoryName(scriptFile));
            File.WriteAllText(scriptFile, res);

            var ec = Utils.RunProgram(ExePath, scriptFile + " -s"); // "-s" opts out of Obfuscar telemetry

            if (ec == 0)
            {
                var dotfuscatedFiles = Directory.EnumerateFiles(obfuscatedOutput).ToList();
                return dotfuscatedFiles.Where(s => Path.GetFileName(s) == Path.GetFileName(fullPath)).FirstOrDefault();
            }
            else
                throw new Exception("Failed to obfuscate files. Obfuscar exitcode: " + ec.ToString());

        }

        public double GetOrder(PackageDef package)
        {
            return 20;
        }
    }
    internal static class Utils
    {
        private static OpenTap.TraceSource log = Log.CreateSource("Program");

        internal static int RunProgram(string program, string arguments)
        {
            log.Debug("Running '{0}' with '{1}'", program, arguments);

            ProcessStartInfo pi = new ProcessStartInfo(program, arguments);
            pi.CreateNoWindow = true;
            pi.UseShellExecute = false;

            pi.RedirectStandardError = true;
            pi.RedirectStandardOutput = true;

            var p = Process.Start(pi);

            p.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log.Error(e.Data); };
            p.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log.Debug(e.Data); };

            p.BeginErrorReadLine();
            p.BeginOutputReadLine();

            p.WaitForExit();

            try
            {
                return p.ExitCode;
            }
            finally
            {
                p.Close();
            }
        }

        internal static void FileCopy(string source, string destination)
        {
            const int RetryTimeoutMS = 10;

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    File.Copy(source, destination, true);
                    break;
                }
                catch
                {
                    System.Threading.Thread.Sleep(RetryTimeoutMS);
                    if (i == 9)
                        throw;
                }
            }

        }
    }

}
