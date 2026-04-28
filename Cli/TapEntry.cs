using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenTap.Cli;

internal class TapEntry
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
    
    public static void Go()
    {
        var args = Environment.GetCommandLineArgs();

        bool installCommand = args.Contains("install");
        bool uninstallCommand = args.Contains("uninstall");
        bool packageManagerCommand = args.Contains("packagemanager");

        // "--no-isolation" can be useful for debugging package install related issues,
        // e.g when deploying an image with "tap image install ..."
        bool noIsolation = args.Contains("--no-isolation");

        if ((installCommand || uninstallCommand || packageManagerCommand) && !noIsolation) 
        {
            // "trick" applications into thinking we are running isolated. This is
            // for compatibility reasons. OpenTAP no longer starts isolated child
            // processes since it is no longer necessary, but older versions of
            // e.g. PackageManager will not work correctly when this is not set.
            Environment.SetEnvironmentVariable(ExecutorSubProcess.EnvVarNames.ParentProcessExeDir, Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
        }

        ConsoleTraceListener.SetStartupTime(DateTime.Now);

        bool isVerbose = args.Contains("--verbose") || args.Contains("-v");
        bool isQuiet = args.Contains("--quiet") || args.Contains("-q"); ;
        bool isColor = IsColor();
        var cliTraceListener = new ConsoleTraceListener(isVerbose, isQuiet, isColor);
        Log.AddListener(cliTraceListener);
        AppDomain.CurrentDomain.ProcessExit += (s, e) => cliTraceListener.Flush();

        PluginManager.Search();
        SessionLogs.SetLogPathFromEngineSettings();
        DebuggerAttacher.TryAttach();
        CliActionExecutor.Execute();
    }
}
