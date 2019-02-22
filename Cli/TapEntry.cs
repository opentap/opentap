//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace OpenTap.Cli
{
    /// <summary>
    /// Serves as an entrypoint for tap.exe. Runs Keysight.Tap.Package.Cli.Program.Main
    /// possibly in "isolated" mode to allow self-updating depending on command line args
    /// </summary>
    internal class TapEntry
    {
        static TapEntry()
        {
            TapInitializer.Initialize();
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

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && (installCommand || uninstallCommand || packageManagerCommand))
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
