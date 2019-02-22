//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace tap
{
    class Program
    {
        static void Main(string[] args)
        {
            // in case TPM needs to update Tap.Cli.dll, we load it from memory to not keep the file in use
            Assembly asm = null;
            string entrypoint = "OpenTap.Cli.TapEntry";
            try
            {
                string appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                if (File.Exists(Path.Combine(appDir, "Packages/OpenTAP/OpenTap.Cli.dll")))
                    asm = Assembly.Load(File.ReadAllBytes(Path.Combine(appDir, "Packages/OpenTAP/OpenTap.Cli.dll")));
                if (File.Exists(Path.Combine(appDir, "OpenTap.Cli.dll")))
                    asm = Assembly.Load(File.ReadAllBytes(Path.Combine(appDir, "OpenTap.Cli.dll")));
                else if (File.Exists(Path.Combine(appDir, "OpenTap.Cli.exe")))
                    asm = Assembly.Load(File.ReadAllBytes(Path.Combine(appDir, "OpenTap.Cli.exe")));
                else if (File.Exists(Path.Combine(appDir, ".tapentry")))
                {
                    string[] lines = File.ReadAllLines(Path.Combine(appDir, ".tapentry"));
                    asm = Assembly.Load(File.ReadAllBytes(Path.Combine(appDir, lines[0])));
                    if (lines.Length > 1)
                        entrypoint = lines[1];
                }
            }
            catch
            {
                Console.WriteLine("Error finding TAP CLI. Please try reinstalling TAP.");
                Environment.ExitCode = 7;
                return;
            }
            if (asm == null)
            {
                Console.WriteLine("Missing TAP CLI. Please try reinstalling TAP.");
                Environment.ExitCode = 8;
                return;
            }

#if DEBUG && !NETCOREAPP
            if (args.Contains("-v"))
            {
                Console.WriteLine("Attaching Debugger.");
                VisualStudioHelper.AttemptDebugAttach();
            }
#endif

            var type = asm.GetType(entrypoint);
            var method = type.GetMethod("Go", BindingFlags.Static | BindingFlags.Public);
            method.Invoke(null, Array.Empty<object>());
        }

    }
}
