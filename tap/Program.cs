//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace tap;

class Program
{
    static void Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        
        // OPENTAP_INIT_DIRECTORY: Executing assembly is null when running with 'dotnet tap.dll' hence the following environment variable can be used.
        Environment.SetEnvironmentVariable("OPENTAP_INIT_DIRECTORY", Path.GetDirectoryName(typeof(Program).Assembly.Location));
        
        try
        {
            string appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

            Assembly load(string file)
            {
                file = Path.Combine(appDir, file);
                if (File.Exists(file) == false)
                    return null;
                return Assembly.LoadFrom(file);
            }

            var asm = load("Packages/OpenTAP/OpenTap.Cli.dll") ?? load("OpenTap.Cli.dll");
            if (asm == null)
            {
                Console.WriteLine("Missing OpenTAP CLI. Please try reinstalling OpenTAP.");
                Environment.ExitCode = 8;
                return;
            }
        }
        catch
        {
            Console.WriteLine("Error finding OpenTAP CLI. Please try reinstalling OpenTAP.");
            Environment.ExitCode = 7;
            return;
        }

        Go();
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Go()
    {
        OpenTap.Cli.TapEntry.Go();
    }
}
