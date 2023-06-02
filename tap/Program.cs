//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using OpenTap;
using OpenTap.Cli;

namespace tap;

class Program
{

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void ExecuteAndUnload(string assemblyPath, out WeakReference alcWeakRef)
    {
        var alc = new TestAssemblyLoadContext();
        Assembly a = alc.LoadFromAssemblyPath(assemblyPath);
        alc.InitializeTapResolver();
        alcWeakRef = new WeakReference(alc, trackResurrection: true);

        var type = a.GetType("OpenTap.Cli.TapEntry");
        var entryPoint = type!.GetMethod("Go", BindingFlags.Static | BindingFlags.Public)!;
        entryPoint.Invoke(null, Array.Empty<object>());

        alc.Unload();
    }

    static void OldMain()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

        // OPENTAP_INIT_DIRECTORY: Executing assembly is null when running with 'dotnet tap.dll' hence the following environment variable can be used.
        Environment.SetEnvironmentVariable("OPENTAP_INIT_DIRECTORY",
            Path.GetDirectoryName(typeof(Program).Assembly.Location));
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

    static void Main(string[] args)
    {
        string appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location);
        var asmPath = Path.Combine(appDir, "OpenTap.Cli.dll");
        if (Environment.GetEnvironmentVariable("Sign") != null)
        {
            OldMain();
        }

        ExecuteAndUnload(asmPath, out var test);

        for (int i = 0; i < 100 && test.IsAlive; i++)
        {
            Console.Write($"Waiting for context to be collected: {i}.\r");
            Console.Out.Flush();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(10);
        }
        Console.WriteLine();

        if (test.IsAlive)
        {
            Console.WriteLine($"Failed to collect context.");
            Console.ReadKey();
        }
        else
            Console.WriteLine($"Context was collected.");

    }
}