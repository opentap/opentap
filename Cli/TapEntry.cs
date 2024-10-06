//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace OpenTap.Cli
{
    /// <summary>
    /// Serves as an entrypoint for tap.exe. Runs OpenTap.Cli.CliActionExecutor.Execute
    /// possibly in "isolated" mode to allow self-updating depending on command line args
    /// </summary>
    internal class TapEntry
    {

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

        private static bool IsColor()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Contains("--color") || arguments.Contains("-c"))
                return true;
            var envvar = Environment.GetEnvironmentVariable("OPENTAP_COLOR");
            if (envvar == null)
                return false;
            if (envvar == "always")
                return true;
            else if (envvar == "auto")
                return !(Console.IsErrorRedirected || Console.IsOutputRedirected) || ExecutorClient.IsExecutorMode;
            else if (envvar != "never")
                Console.WriteLine("Unknown value of variable OPENTAP_COLOR, valid values are always, auto or never.");
            return false;
        }

        private static void goIsolated()
        {
            if (IsColor())
            {
                try
                {
                    if (OperatingSystem.Current == OperatingSystem.Windows)
                    {
                        if (AnsiColorCodeFix.TryEnableForWin10())
                            Environment.SetEnvironmentVariable("OPENTAP_ANSI_COLORS", "1");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while enabling ANSI colors: {ex.Message}");
                }
            }

            string arguments = new CommandLineSplit(Environment.CommandLine).Args;
            string message = null;

            string tapCommand = OperatingSystem.Current == OperatingSystem.Windows ? "tap.exe" : "tap";
            using (ExecutorSubProcess subproc = ExecutorSubProcess.Create(Path.Combine(ExecutorClient.ExeDir, tapCommand), arguments))
            {
                subproc.MessageReceived += (s, msg) =>
                {
                    if (string.IsNullOrEmpty(message))
                    {
                        message = msg;
                    }
                };
                subproc.Start();
                Environment.ExitCode = subproc.WaitForExit();
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
                            Environment.ExitCode = subproc.WaitForExit();
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
            var start = DateTime.Now;
            void loadCommandLine()
            {
                var args = Environment.GetCommandLineArgs();
                bool isVerbose = args.Contains("--verbose") || args.Contains("-v");
                bool isQuiet = args.Contains("--quiet") || args.Contains("-q"); ;
                ConsoleTraceListener.SetStartupTime(start);
                var cliTraceListener = new ConsoleTraceListener(isVerbose, isQuiet, IsColor());
                Log.AddListener(cliTraceListener);
                cliTraceListener.TraceEvents(TapInitializer.InitTraceListener.Instance.AllEvents.ToArray());
                TapInitializer.InitTraceListener.Instance.AllEvents.Clear();
                AppDomain.CurrentDomain.ProcessExit += (s, e) => cliTraceListener.Flush();
            }

            TapInitializer.Initialize(); // This will dynamically load OpenTap.dll
            // loadCommandLine has to be called after Initialize 
            // to ensure that we are able to load OpenTap.dll
            loadCommandLine();
            wrapGoInProcess();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void wrapGoInProcess()
        {
            DebuggerAttacher.TryAttach();
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
            var installCommand = args.Contains("install");
            var uninstallCommand = args.Contains("uninstall");
            var upgrade = args.Contains("upgrade");
            var packageManagerCommand = args.Contains("packagemanager");
            // "--no-isolation" can be useful for debugging package install related issues,
            // e.g when deploying an image with "tap image install ..."
            bool noIsolation = args.Contains("--no-isolation");

            if ((installCommand || uninstallCommand || packageManagerCommand || upgrade) && !noIsolation)
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
