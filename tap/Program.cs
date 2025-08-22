//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Globalization;
using System.IO;
using OpenTap;
using OpenTap.Cli;
using System.Linq;

namespace tap;

class Program
{
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
        
    static void Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            
        // OPENTAP_INIT_DIRECTORY: Executing assembly is null when running with 'dotnet tap.dll' hence the following environment variable can be used.
        Environment.SetEnvironmentVariable("OPENTAP_INIT_DIRECTORY", Path.GetDirectoryName(typeof(Program).Assembly.Location));
            
        var start = DateTime.Now;
        ConsoleTraceListener.SetStartupTime(start);

        bool isVerbose = args.Contains("--verbose") || args.Contains("-v");
        bool isQuiet = args.Contains("--quiet") || args.Contains("-q"); ;
        bool isColor = IsColor();
        var cliTraceListener = new ConsoleTraceListener(isVerbose, isQuiet, isColor);
        Log.AddListener(cliTraceListener);
        AppDomain.CurrentDomain.ProcessExit += (s, e) => cliTraceListener.Flush();

        PluginManager.Search();
        DebuggerAttacher.TryAttach();
        CliActionExecutor.Execute();
    }
}