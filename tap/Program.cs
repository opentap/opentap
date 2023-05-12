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
using System.Runtime.Loader;
using System.Threading;
using complex;
using OpenTap;
using OpenTap.Cli;

namespace tap
{
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
        

        public static void CancelTapThreads(AssemblyLoadContext ctx)
        {
            var mainthread = TapThread.Current;
            while (mainthread.Parent != null)
                mainthread = mainthread.Parent;
            ctx.Unloading += context =>
            {
                try
                {
                    mainthread.Abort("Context unloaded.");
                }
                catch
                {
                    // ignore -- Abort is supposed to throw
                }

                try
                {
                    mainthread.AbortManager();
                }
                catch
                {
                    // this shouldn't throw but let's be safe
                }
            };
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ExecuteAndUnload(string assemblyPath, out WeakReference alcWeakRef)
        {
            string appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var alc = new TestAssemblyLoadContext(appDir);
            Assembly a = alc.LoadFromAssemblyPath(assemblyPath);
            var thisasm = alc.LoadFromAssemblyPath(Assembly.GetExecutingAssembly().Location);
            var program = thisasm.GetType("tap.Program");
            var cancelTapThread = program!.GetMethod("CancelTapThreads", BindingFlags.Static | BindingFlags.Public)!;
            cancelTapThread.Invoke(null, new object[] { alc });
            

            alcWeakRef = new WeakReference(alc, trackResurrection: true);
            
            var type = a.GetType("OpenTap.Cli.TapEntry");
            var entryPoint = type!.GetMethod("Go", BindingFlags.Static | BindingFlags.Public)!;
            entryPoint.Invoke(null, Array.Empty<object>());

            alc.Unload();
        }
        static void Main(string[] args)
        {
            string appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var asmPath = Path.Combine(appDir, "OpenTap.Cli.dll");
            ExecuteAndUnload(asmPath, out var testAlcWeakRef);

            if (args.Contains("test"))
            {
                for (int i = 0; testAlcWeakRef.IsAlive && (i < 10); i++)
                {
                    Console.WriteLine($"Waiting for context to be collected: {i}.");
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                if (testAlcWeakRef.IsAlive)
                    Console.WriteLine($"Failed to collect context.");
                else
                    Console.WriteLine($"Context was collected.");
                Console.ReadKey();
            }
        }
    }

}
