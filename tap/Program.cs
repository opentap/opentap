//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace tap
{
    class Program
    {
        static void Main(string[] args)
        {
            Environment.SetEnvironmentVariable("OPENTAP_INIT_DIRECTORY", typeof(Program).Assembly.Location);
            // in case TPM needs to update Tap.Cli.dll, we load it from memory to not keep the file in use
            Assembly asm = null;
            string entrypoint = "OpenTap.Cli.TapEntry";
            try
            {
                string appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

                Assembly load(string file)
                {
                    file = Path.Combine(appDir, file);
                    if (File.Exists(file) == false)
                        return null;
                    return Assembly.Load(File.ReadAllBytes(file));
                }
                asm = load("Packages/OpenTAP/OpenTap.Cli.dll") ?? load("OpenTap.Cli.dll") ?? load("OpenTap.Cli.exe");
                if (asm == null && File.Exists(Path.Combine(appDir, ".tapentry")))
                {
                    string[] lines = File.ReadAllLines(Path.Combine(appDir, ".tapentry"));
                    asm = Assembly.Load(File.ReadAllBytes(Path.Combine(appDir, lines[0])));
                    if (lines.Length > 1)
                        entrypoint = lines[1];
                }
            }
            catch
            {
                Console.WriteLine("Error finding OpenTAP CLI. Please try reinstalling OpenTAP.");
                Environment.ExitCode = 7;
                return;
            }
            if (asm == null)
            {
                Console.WriteLine("Missing OpenTAP CLI. Please try reinstalling OpenTAP.");
                Environment.ExitCode = 8;
                return;
            }

            var type = asm.GetType(entrypoint);
            var method = type.GetMethod("Go", BindingFlags.Static | BindingFlags.Public);
            method.Invoke(null, Array.Empty<object>());
        }
    }

}
