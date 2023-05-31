//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using OpenTap;
using OpenTap.Cli;

namespace tap;

class Program
{
    private static void EnterAsm(Assembly asm)
    {
        // Setup assembly resolver before calling DeferLoad
        string appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        void DeferLoad()
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            var actionTree = new CliActionTree();
            var selectedcmd = actionTree.GetSubCommand(args);
            if (selectedcmd?.Type != null && selectedcmd?.SubCommands.Any() != true)
            {
                var act = selectedcmd.Type;
                if (act.AsTypeData() is TypeData { TargetFramework: TargetFramework.NetFramework })
                {
                    // execute in dotnet framework
                    var proc = Process.Start(Path.Combine(appDir, "tap-legacy.exe"), string.Join(" ", args));
                    proc.WaitForExit();
                    Environment.Exit(proc.ExitCode);
                }

                var isolated = TypeData.GetTypeData("OpenTap.Package.IsolatedPackageAction");
                if (act.DescendsTo(isolated))
                {
                    // go isolated
                    // this involves unloading the current load context and creating a new, isolated context
                        
                }
                else
                {
                    var type = asm.GetType("OpenTap.Cli.TapEntry");
                    var method = type.GetMethod("Go", BindingFlags.Static | BindingFlags.Public);
                    method.Invoke(null, Array.Empty<object>());
                }
            }
        }
            
        var type = asm.GetType("OpenTap.Cli.TapEntry");
        var method = type.GetMethod("Go", BindingFlags.Static | BindingFlags.Public);
        method.Invoke(null, Array.Empty<object>());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void ExecuteAndUnload(string assemblyPath, out WeakReference alcWeakRef)
    {
        var alc = new TestAssemblyLoadContext();
        Assembly a = alc.LoadFromAssemblyPath(assemblyPath);

        alcWeakRef = new WeakReference(alc, trackResurrection: true);
            
        var type = a.GetType("OpenTap.Cli.TapEntry");
        var entryPoint = type!.GetMethod("Go", BindingFlags.Static | BindingFlags.Public)!;
        entryPoint.Invoke(null, Array.Empty<object>());

        alc.Unload();
    }

    static void Main(string[] args)
    {
        string appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location);
        var asmPath = Path.Combine(appDir, "OpenTap.Cli.dll");
        ExecuteAndUnload(asmPath, out var testAlcWeakRef);

        for (int i = 0; i < 100 && testAlcWeakRef.IsAlive; i++)
        {
            Console.WriteLine($"Waiting for context to be collected: {i}.");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Yield();
        }

        if (testAlcWeakRef.IsAlive)
            Console.WriteLine($"Failed to collect context.");
        else
            Console.WriteLine($"Context was collected.");
        Console.ReadKey();
    }
}