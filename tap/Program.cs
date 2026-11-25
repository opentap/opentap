//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Globalization;
using System.IO;
using OpenTap;

namespace tap;

class Program
{
    static void Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        Environment.SetEnvironmentVariable("OPENTAP_INIT_DIRECTORY", Path.GetDirectoryName(typeof(Program).Assembly.Location));

        var directory = Path.GetDirectoryName(typeof(Program).Assembly.Location);
        var runtimePath = Path.Combine(directory, "OpenTap.Runtime.dll");
        if (!File.Exists(runtimePath))
            runtimePath = Path.Combine(directory, "Packages", "OpenTAP", "OpenTap.Runtime.dll");
        System.Reflection.Assembly.LoadFrom(runtimePath);
        Run(args);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void Run(string[] args)
    {
        // Main only performs the handoff. No engine services or application logic
        // should be initialized in the process/default copy of these assemblies.
        var directory = Path.GetDirectoryName(typeof(Program).Assembly.Location);
        // MSBuild outputs keep the freshly built CLI beside the launcher;
        // packaged installations keep it under Packages/OpenTAP.
        var cliPath = Path.Combine(directory, "OpenTap.Cli.dll");
        if (!File.Exists(cliPath))
            cliPath = Path.Combine(directory, "Packages", "OpenTAP", "OpenTap.Cli.dll");
        using (var context = OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder()
            .WithEntryPoint(cliPath, "OpenTap.Cli.TapEntry", "Go")
            .WithArgument(args)
            .Build()))
        {
        }
    }
}
