//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTap.Cli
{
    /// <summary>
    /// Serves as an entrypoint for tap.exe. Runs Keysight.Tap.Package.Cli.Program.Main
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
                return !(Console.IsErrorRedirected || Console.IsOutputRedirected);
            else if (envvar != "never")
                Console.WriteLine("Unknown value of variable OPENTAP_COLOR, valid values are always, auto or never.");
            return false;
        }

        private static void goInProcess(bool isolated)
        {
            var start = DateTime.Now;
            void loadCommandLine()
            {
                var args = Environment.GetCommandLineArgs();
                bool isVerbose = args.Contains("--verbose") || args.Contains("-v");
                bool isQuiet = args.Contains("--quiet");
                ConsoleTraceListener.SetStartupTime(start);
                var cliTraceListener = new ConsoleTraceListener(isVerbose, isQuiet, IsColor());
                Log.AddListener(cliTraceListener);
                cliTraceListener.TraceEvents(TapInitializer.InitTraceListener.Instance.AllEvents.ToArray());
                AppDomain.CurrentDomain.ProcessExit += (s, e) => cliTraceListener.Flush();

            }

            TapInitializer.Initialize(isolated); // This will dynamically load OpenTap.dll
            // loadCommandLine has to be called after Initialize 
            // to ensure that we are able to load OpenTap.dll
            loadCommandLine();
            wrapGoInProcess(isolated);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void wrapGoInProcess(bool isolated)
        {
            if (isolated)
            {
                // Enable Isolated mode on the plugin-manager as early as possible to ensure we don't
                // load assemblies incorrectly
                typeof(PluginManager).GetMethod("Isolate", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, Array.Empty<object>());
            }
            DebuggerAttacher.TryAttach();
            CliActionExecutor.Execute();
        }

        public static void Go()
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            var installCommand = args.Contains("install");
            var uninstallCommand = args.Contains("uninstall");
            var packageManagerCommand = args.Contains("packagemanager");

            if (OperatingSystem.Current == OperatingSystem.Windows && (installCommand || uninstallCommand || packageManagerCommand))
            {
                goInProcess(true);
            }
            else
            {
                goInProcess(false);
            }
        }
    }
}
