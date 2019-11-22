//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using OpenTap.Package;
using System.Runtime.CompilerServices;
using System.ComponentModel;

namespace OpenTap.Cli
{
    internal class CliActionTree
    {
        public string Name { get; set; }
        public bool IsGroup => Type == null;
        public ITypeData Type { get; set; }
        public List<CliActionTree> SubCommands { get; set; }

        public static CliActionTree Root { get; internal set; }

        static CliActionTree()
        {
            var commands = TypeData.GetDerivedTypes(TypeData.FromType(typeof(ICliAction))).Where(t => t.CanCreateInstance && t.GetDisplayAttribute() != null).ToList();
            Root = new CliActionTree { Name = "tap" };
            foreach (var item in commands)
                ParseCommand(item, item.GetDisplayAttribute().Group, Root);
        }

        private static void ParseCommand(ITypeData type, string[] group, CliActionTree command)
        {
            if (command.SubCommands == null)
                command.SubCommands = new List<CliActionTree>();

            // If group is not empty. Find command with first group name
            if (group.Length > 0)
            {
                var existingCommand = command.SubCommands.FirstOrDefault(c => c.Name == group[0]);

                if (existingCommand == null)
                {
                    existingCommand = new CliActionTree() { Name = group[0] };
                    command.SubCommands.Add(existingCommand);
                }

                ParseCommand(type, group.Skip(1).ToArray(), existingCommand);
            }
            else
            {
                command.SubCommands.Add(new CliActionTree() { Name = type.GetDisplayAttribute().Name, Type = type, SubCommands = new List<CliActionTree>() });
                command.SubCommands = command.SubCommands.OrderBy(c => c.Name).ToList();
            }
        }

        public CliActionTree GetSubCommand(string[] args)
        {
            if (args.Length == 0)
                return null;

            foreach (var item in SubCommands)
            {
                if (item.Name == args[0])
                {
                    if (args.Length == 1 || item.SubCommands.Any() == false)
                       return item;
                    else
                    {
                        return item.GetSubCommand(args.Skip(1).ToArray());
                    }
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Helper used to execute <see cref="ICliAction"/>s.
    /// </summary>
    public class CliActionExecutor
    {
        private static TraceSource log = Log.CreateSource("tap");
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                log.Error("CurrentDomain Unhandled Exception: " + ex.Message);
                log.Debug(ex);
                log.Flush();
            }
        }
        
        /// <summary> 
        /// Used as command line interface of OpenTAP (PluginManager must have searched for assemblies before this method is called).
        /// This calls Execute with commandline arguments given to this environment and sets Environment.ExitCode. 
        /// </summary>
        public static void Execute(){
            Environment.ExitCode = Execute(Environment.GetCommandLineArgs().Skip(1).ToArray());
        }

        /// <summary>
        /// Used as entrypoint for the command line interface of OpenTAP (PluginManager must have searched for assemblies before this method is called)
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Execute(params string[] args)
        {
            // Trigger plugin manager before anything else.
            if (ExecutorClient.IsRunningIsolated)
            {
                // TODO: This is not needed right now, but might be again when we fix the TODO in tap.exe
                //PluginManager.DirectoriesToSearch.Clear();
                //PluginManager.DirectoriesToSearch.Add(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
                using (var tpmClient = new ExecutorClient())
                {
                    tpmClient.MessageServer("delete " + Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
                }
            }

            try
            {
                // Turn off the default system behavior when CTRL+C is pressed. 
                // When Console.TreatControlCAsInput is false, CTRL+C is treated as an interrupt instead of as input.
                Console.TreatControlCAsInput = false; 
            }
            catch { }
            try
            {
                var execThread = TapThread.Current;
                Console.CancelKeyPress += (s, e) => execThread.Abort();
            }
            catch { }

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Find the called action
            if(!TypeData.GetDerivedTypes<ICliAction>().Any())
            {
                Console.WriteLine("No commands found. Please try reinstalling OpenTAP.");
                return 1;
            }

            ITypeData selectedCommand = null;
            var requestedCommand = args.FirstOrDefault();

            // Find selected command
            var selectedcmd = CliActionTree.Root.GetSubCommand(args);
            if (selectedcmd?.Type != null && selectedcmd?.SubCommands.Any() != true)
                selectedCommand = selectedcmd.Type;

            // Print default info
            if (selectedCommand == null || selectedcmd == null)
            {
                Console.WriteLine("OpenTAP Command Line Interface ({0})",Assembly.GetExecutingAssembly().GetSemanticVersion().ToString(4));
                Console.WriteLine("Usage: tap <command> [<subcommand>] [<args>]\n");

                if (selectedcmd == null)
                {
                    Console.WriteLine("Valid commands are:");

                    foreach (var cmd in CliActionTree.Root.SubCommands)
                    {
                        if (cmd.IsGroup || cmd.Type.GetAttribute<BrowsableAttribute>()?.Browsable == true)
                        {
                            Console.WriteLine($"  {cmd.Name.PadRight(22)}{(cmd.IsGroup ? "" : cmd.Type.GetDisplayAttribute().Description)}");
                            foreach (var subcmd in cmd.SubCommands)
                            {
                                if (subcmd.IsGroup || subcmd.Type.IsBrowsable())
                                    Console.WriteLine($"    {subcmd.Name.PadRight(22)}{(subcmd.IsGroup ? "" : subcmd.Type.GetDisplayAttribute().Description)}");
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Valid commands for '{selectedcmd.Name}':");
                    var availableCommands = selectedcmd.SubCommands.Where(cmd => cmd.IsGroup || (cmd.Type.GetDisplayAttribute() != null && cmd.Type.IsBrowsable())).Distinct().ToList();
                    foreach (var cmd in availableCommands)
                    {
                        if (cmd.IsGroup || cmd.Type.IsBrowsable())
                        {
                            Console.WriteLine($"  {cmd.Name.PadRight(22)}{(cmd.IsGroup ? "" : cmd.Type.GetDisplayAttribute().Description)}");
                            foreach (var subcmd in cmd.SubCommands)
                            {
                                if (subcmd.IsGroup || subcmd.Type.IsBrowsable())
                                    Console.WriteLine($"    {subcmd.Name.PadRight(22)}{(subcmd.IsGroup ? "" : subcmd.Type.GetDisplayAttribute().Description)}");
                            }
                        }
                    }
                }

                Console.WriteLine("\nRun \"tap.exe <command> [<subcommand>] -h\" to get additional help for a specific command.\n");

                if (args.Length == 0 || args.Any(s => s.ToLower() == "--help" || s.ToLower() == "-h"))
                    return 0;
                else
                    return -1;
            }

            {   // setup logging to be relative to the executing assembly.
                var logpath = EngineSettings.Current.SessionLogPath.Expand(date: Process.GetCurrentProcess().StartTime);
                if (Path.IsPathRooted(logpath) == false)
                {
                    var dir = Path.GetDirectoryName(typeof(SessionLogs).Assembly.Location);
                    if (ExecutorClient.IsRunningIsolated)
                    {
                        dir = ExecutorClient.ExeDir;
                    }
                    logpath = Path.Combine(dir, logpath);
                }
                SessionLogs.Rename(logpath);
            }

            if (selectedCommand != TypeData.FromType(typeof(RunCliAction)) && UserInput.Interface == null) // RunCliAction has --non-interactive flag and custom platform interaction handling.          
                CliUserInputInterface.Load();
            
            ICliAction packageAction = null;
            try{
                packageAction = (ICliAction)selectedCommand.CreateInstance();
            }catch(TargetInvocationException e1) when (e1.InnerException is System.ComponentModel.LicenseException e){
                Console.Error.WriteLine("Unable to load CLI Action '{0}'", selectedCommand.GetDisplayAttribute().GetFullName());
                Console.Error.WriteLine(e.Message);
                return -4;
            }

            if (packageAction == null)
            {
                Console.WriteLine("Error instantiating command {0}", selectedCommand.Name);
                return -3;
            }

            try
            {
                int skip = selectedCommand.GetDisplayAttribute().Group.Length + 1; // If the selected command has a group, it takes two arguments to use the command. E.g. "package create". If not, it only takes 1 argument, E.g. "restapi".
                return packageAction.Execute(args.Skip(skip).ToArray());
            }
            catch (ExitCodeException ec)
            {
                log.Error(ec.Message);
                return ec.ExitCode;
            }
            catch(ArgumentException ae)
            {
                // ArgumentException usually contains several lines.
                // Only print the first line as an error message. 
                // Example message:
                //  "Directory is not a git repository.
                //   Parameter name: repositoryDir"
                var lines = ae.Message.Split(new char[] { '\r', '\n' },StringSplitOptions.RemoveEmptyEntries);
                log.Error(lines.First());
                for(int i = 1;i<lines.Length;i++)
                    log.Debug(lines[i]);
                return -1;
            }
            catch (OperationCanceledException ex)
            {
                log.Error(ex.Message);
                return 1;
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                log.Debug(ex);
                return -1;
            }
            finally
            {
                Log.Flush();
            }
        }
    }
   
    

#if DEBUG2 && !NETSTANDARD2_0
    /// <summary>
    /// Helper class for interacting with the visual studio debugger. Does not build on .NET Core.
    /// </summary>
    public static class VisualStudioHelper
    {
        /// <summary>
        /// Attach to the debugger.
        /// </summary>
        public static void AttemptDebugAttach()
        {
            bool requiresAttach = !Debugger.IsAttached;

            // If I don't have a debugger attached, try to attach 
            if (requiresAttach)
            {
                Stopwatch timer = Stopwatch.StartNew();
                //log.Debug("Attaching debugger.");
                int tries = 4;
                EnvDTE.DTE dte = null;
                while (tries-- > 0)
                {
                    try
                    {
                        dte = VisualStudioHelper.GetRunningInstance().FirstOrDefault();
                        if (dte == null)
                        {
                            //log.Debug("Could not attach Visual Studio debugger. No instances found or missing user privileges.");
                            return;
                        }
                        EnvDTE.Debugger debugger = dte.Debugger;
                        foreach (EnvDTE.Process program in debugger.LocalProcesses)
                        {
                            if (program.ProcessID == Process.GetCurrentProcess().Id)
                            {
                                program.Attach();
                                //log.Debug(timer, "Debugger attached.");
                                return;
                            }
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        if (ex.ErrorCode == unchecked((int)0x8001010A)) // RPC_E_SERVERCALL_RETRYLATER
                        {
                            //log.Debug("Visual Studio was busy while trying to attach. Retrying shortly.");
                            System.Threading.Thread.Sleep(500);
                        }
                        else
                        {
                            //log.Debug(ex);
                            // this probably means someone else was launching at the same time, so you blew up. try again after a brief sleep
                            System.Threading.Thread.Sleep(50);
                        }
                    }
                    catch
                    {
                        //log.Debug(ex);
                        // this probably means someone else was launching at the same time, so you blew up. try again after a brief sleep
                        System.Threading.Thread.Sleep(50);
                    }
                    finally
                    {
                        //Need to release teh com object so other processes get a chance to use it
                        VisualStudioHelper.ReleaseInstance(dte);
                    }
                }
            }
            else
            {
                //log.Debug("Debugger already attached.");
            }
        }

        /// <summary>
        /// Create binding context.
        /// </summary>
        /// <param name="reserved"></param>
        /// <param name="ppbc"></param>
        [DllImport("ole32.dll")]
        private static extern void CreateBindCtx(int reserved, out IBindCtx ppbc);

        /// <summary>
        /// Get running objects.
        /// </summary>
        /// <param name="reserved"></param>
        /// <param name="prot"></param>
        /// <returns></returns>
        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

        /// <summary>
        /// Get interfaces to control currently running Visual Studio instances
        /// </summary>
        public static IEnumerable<EnvDTE.DTE> GetRunningInstance()
        {
            IRunningObjectTable rot;
            IEnumMoniker enumMoniker;
            int retVal = GetRunningObjectTable(0, out rot);

            if (retVal == 0)
            {
                rot.EnumRunning(out enumMoniker);

                IntPtr fetched = IntPtr.Zero;
                IMoniker[] moniker = new IMoniker[1];
                while (enumMoniker.Next(1, moniker, fetched) == 0)
                {
                    IBindCtx bindCtx;
                    CreateBindCtx(0, out bindCtx);
                    string displayName;
                    moniker[0].GetDisplayName(bindCtx, null, out displayName);
                    bool isVisualStudio = displayName.StartsWith("!VisualStudio");
                    if (isVisualStudio)
                    {
                        object obj;
                        rot.GetObject(moniker[0], out obj);
                        var dte = obj as EnvDTE.DTE;
                        yield return dte;
                    }
                }
            }
        }
        /// <summary>
        /// Releases the devenv instance.
        /// </summary>
        /// <param name="instance"></param>
        public static void ReleaseInstance(EnvDTE.DTE instance)
        {
            try
            {
                if (instance != null)
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(instance);
            }
            catch (Exception ex)
            {
                TraceSource log = Log.CreateSource("VSHelper");
                log.Debug(ex);
            }
        }
    }
#endif
}
