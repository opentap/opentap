//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace OpenTap.Cli
{
    /// <summary>
    /// Serves as an entrypoint for tap.exe. Runs Keysight.Tap.Package.Cli.Program.Main
    /// possibly in "isolated" mode to allow self-updating depending on command line args
    /// </summary>
    internal class TapEntry
    {
        internal static class TapInitializer
        {
            /// <summary>
            /// We need this SimpleTapAssemblyResolver to resolve netstandard.dll. We need to resolve netstandard.dll to be able to load OpenTAP, which is a .netstandard project
            /// After we load OpenTAP, we can safely remove this simple resolver and let TapAssemblyResolver in OpenTAP resolve dependencies.
            /// </summary>
            internal class SimpleTapAssemblyResolver
            {
                private List<string> assemblies { get; set; }

                public SimpleTapAssemblyResolver()
                {
                    string curAssemblyFolder = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
                    string currentDir = Path.GetDirectoryName(curAssemblyFolder);
                    assemblies = Directory.EnumerateFiles(currentDir, "*.*", SearchOption.AllDirectories)
                        .Where(s => s.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) || s.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase))
                        .ToList();
                }

                internal Assembly Resolve(object sender, ResolveEventArgs args)
                {
                    // Ignore missing resources
                    if (args.Name.Contains(".resources"))
                        return null;

                    string filename = args.Name.Split(',')[0].ToLower();
                    string assembly = assemblies.FirstOrDefault(s => Path.GetFileNameWithoutExtension(s).ToLower() == filename);

                    // check for assemblies already loaded
                    Assembly loadedAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
                    if (loadedAssembly != null)
                        return loadedAssembly;

                    if (!string.IsNullOrWhiteSpace(assembly))
                        return Assembly.LoadFrom(assembly);

                    Console.Error.WriteLine($"Asked to resolve {filename}, but couldn't");
                    return null;
                }
            }

            private static readonly SimpleTapAssemblyResolver tapAssemblyResolver = new SimpleTapAssemblyResolver();
            internal static void Initialize()
            {
                AppDomain.CurrentDomain.AssemblyResolve += tapAssemblyResolver.Resolve;
                ContinueInitialization();
            }

            internal static void ContinueInitialization()
            {
                // We only needed the resolver to get into this method (requires OpenTAP, which requires netstandard)
                // Remove so we avoid race condition with OpenTap AssemblyResolver.
                AppDomain.CurrentDomain.AssemblyResolve -= tapAssemblyResolver.Resolve;
                PluginManager.SearchAsync();
            }
        }

        private class CommandLineSplit
        {
            public string App { get; private set; }
            public string Args { get; private set; }

            public CommandLineSplit(string commandLine)
            {
                string argstr = commandLine;
                bool escaped = false;
                int i;
                for (i = 0; i < argstr.Length; i++)
                {
                    if (argstr[i] == '"')
                    {
                        escaped = !escaped;
                        continue;
                    }
                    if (argstr[i] == ' ' && !escaped)
                    {
                        break;
                    }
                }
                App = argstr.Substring(0, i).Trim('"');
                Args = argstr.Substring(i).TrimStart(' ');
            }
        }

        private static void goIsolated()
        {
            string arguments = new CommandLineSplit(Environment.CommandLine).Args;
            string message = null;
            using (ExecutorSubProcess subproc = ExecutorSubProcess.Create("tap.exe", arguments))
            {
                subproc.MessageReceived += (s, msg) =>
                {
                    if (string.IsNullOrEmpty(message))
                    {
                        message = msg;
                    }
                };
                subproc.Start();
                subproc.WaitForExit();
            }
            List<string> tmpDirs = new List<string>();
            try
            {
                while (string.IsNullOrEmpty(message) == false)
                {

                    if (message.StartsWith("delete "))
                    {
                        string dir = message.Substring("delete ".Length);
                        for (int i = 0; i < 4; i++)
                        {
                            try // probably OK if it cannot be deleted.
                            {
                                if (Directory.Exists(dir))
                                {
                                    Directory.Delete(dir, true);
                                }

                                break;
                            }
                            catch
                            {
                                Thread.Sleep(50);
                            }
                        }
                        message = null;
                    }
                    else if (message.StartsWith("run "))
                    {
                        string loc = message.Substring("run ".Length);
                        CommandLineSplit command = new CommandLineSplit(loc);
                        message = null;
                        using (ExecutorSubProcess subproc = ExecutorSubProcess.Create(command.App, $"{arguments} {command.Args}", true))
                        {
                            subproc.Start();
                            subproc.MessageReceived += (s, msg) =>
                            {
                                if (string.IsNullOrEmpty(message))
                                {
                                    message = msg;
                                }
                            };
                            subproc.WaitForExit();
                        }
                        tmpDirs.Add(Path.GetDirectoryName(command.App));
                    }
                    else
                    {
                        message = null;
                    }
                }
            }
            finally
            {

                foreach (string dir in tmpDirs)
                {
                    try // probably OK if it cannot be deleted.
                    {
                        if (Directory.Exists(dir))
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                    catch { }
                }
            }

        }

        private static void goInProcess()
        {
            CliActionExecutor.Execute();
        }

        public static void Go()
        {
            TapInitializer.Initialize();

            if (ExecutorClient.IsExecutorMode)
            {
                goInProcess();
                return;
            }

            string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            bool installCommand;
            bool uninstallCommand;
            bool packageManagerCommand;
            installCommand = args.Contains("install");
            uninstallCommand = args.Contains("uninstall");
            packageManagerCommand = args.Contains("packagemanager");

            if (installCommand || uninstallCommand || packageManagerCommand)
            {
                goIsolated();
            }
            else
            {
                goInProcess();
            }
        }
    }
}
