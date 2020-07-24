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
using System.Runtime.CompilerServices;

namespace OpenTap.Cli
{
    internal class CliActionTree
    {
        public string Name { get; }
        public bool IsGroup => (SubCommands?.Count ?? 0) > 0;
        public bool IsBrowsable => (Type?.IsBrowsable() ?? false) || SubCommands.Any(x => x.IsBrowsable); 
        
        public ITypeData Type { get; private set; }
        public List<CliActionTree> SubCommands { get; private set; }

        public CliActionTree Root { get; }

        public CliActionTree()
        {
            var commands = TypeData.GetDerivedTypes(TypeData.FromType(typeof(ICliAction)))
                .Where(t => t.CanCreateInstance && t.GetDisplayAttribute() != null).ToList();
            Name = "tap";
            Root = this;
            foreach (var item in commands)
                ParseCommand(item, item.GetDisplayAttribute().Group, Root);
        }

        CliActionTree(CliActionTree parent, string name)
        {
            Name = name;
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
                    existingCommand = new CliActionTree(command, group[0]);
                    command.SubCommands.Add(existingCommand);
                }

                ParseCommand(type, group.Skip(1).ToArray(), existingCommand);
            }
            else
            {
                command.SubCommands.Add(new CliActionTree(command, type.GetDisplayAttribute().Name) { Type = type, SubCommands = new List<CliActionTree>()});
                command.SubCommands.Sort((x,y) => string.Compare(x.Name, y.Name));
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
                    var subCmd = item.GetSubCommand(args.Skip(1).ToArray());
                    return subCmd ?? item;
                }
            }

            return null;
        }

        /// <summary>
        /// This method calculates the max length in a command tree. Consider the tree outputted by tap help:
        /// run
        /// package
        ///    create
        ///    install  = Longest command (10 characters), this method would return the integer 10.
        /// </summary>
        /// <param name="levelPadding">How much is each level indenting? In the example above, the subcommands to 'package' is indented with 3 characters</param>
        /// <returns>Max character length of commands outputted</returns>
        internal int GetMaxCommandTreeLength(int levelPadding)
        {
            var initial = this == Root ? 0 : levelPadding;
            var x = 0;

            if (SubCommands.Count == 0)
                return x + Name.Length;

            foreach(var cmd in SubCommands)
            {
                int length = cmd.GetMaxCommandTreeLength(levelPadding);
                if (length > x)
                    x = length;
            }
            return x + initial;
        }
    }

    /// <summary>
    /// Helper used to execute <see cref="ICliAction"/>s.
    /// </summary>
    public class CliActionExecutor
    {
        internal static readonly int LevelPadding = 3;
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
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    execThread.Abort();
                };

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

            try
            {
                // setup logging to be relative to the executing assembly.
                // at this point SessionLogs.Initialize has already been called (PluginManager.Load).
                // so the log is already being saved at a different location.
                var logpath = EngineSettings.Current.SessionLogPath.Expand(date: Process.GetCurrentProcess().StartTime);
                bool isPathRooted = Path.IsPathRooted(logpath);
                if (isPathRooted == false)
                {
                    var dir = Path.GetDirectoryName(typeof(SessionLogs).Assembly.Location);
                    if (ExecutorClient.IsRunningIsolated)
                    {
                        // redirect the isolated log path to the non-isolated path.
                        dir = ExecutorClient.ExeDir;
                    }

                    logpath = Path.Combine(dir, logpath);
                }

                SessionLogs.Rename(logpath);
            }
            catch (Exception e)
            {
                log.Error("Path defined in Engine settings contains invalid characters: {0}", EngineSettings.Current.SessionLogPath);
                log.Debug(e);
            }

            ITypeData selectedCommand = null;
            
            // Find selected command
            var actionTree = new CliActionTree();
            var selectedcmd = actionTree.GetSubCommand(args);
            if (selectedcmd?.Type != null && selectedcmd?.SubCommands.Any() != true)
                selectedCommand = selectedcmd.Type;
            void print_command(CliActionTree cmd, int level, int descriptionStart)
            {
                if (cmd.IsBrowsable)
                {
                    int relativePadding = descriptionStart - (level * LevelPadding); // Calculate amount of characters to pad right before description start to ensure description alignments.
                    Console.Write($"{"".PadRight(level * LevelPadding)}{cmd.Name.PadRight(relativePadding)}");
                    if (cmd.Type?.IsBrowsable() ?? false)
                    {
                        Console.WriteLine($"{cmd.Type.GetDisplayAttribute().Description}");
                    }
                    else
                    {
                        Console.WriteLine();
                    }

                    if (cmd.IsGroup)
                    {
                        foreach (var subCmd in cmd.SubCommands)
                        {
                            print_command(subCmd, level + 1, descriptionStart);
                        }
                    }
                }
            }
            
            // Print default info
            if (selectedCommand == null)
            {
                Console.WriteLine("OpenTAP Command Line Interface ({0})",Assembly.GetExecutingAssembly().GetSemanticVersion().ToString(4));
                Console.WriteLine("Usage: tap <command> [<subcommand(s)>] [<args>]\n");

                if (selectedcmd == null)
                {
                    Console.WriteLine("Valid commands are:");
                    foreach (var cmd in actionTree.SubCommands)
                    {
                        print_command(cmd, 0, actionTree.GetMaxCommandTreeLength(LevelPadding) + LevelPadding);
                    }
                }
                else
                {
                    Console.Write("Valid subcommands of ");
                    print_command(selectedcmd, 0, actionTree.GetMaxCommandTreeLength(LevelPadding) + LevelPadding);
                }

                Console.WriteLine($"\nRun \"{(OperatingSystem.Current == OperatingSystem.Windows ? "tap.exe" : "tap")} " +
                                   "<command> [<subcommand>] -h\" to get additional help for a specific command.\n");

                if (args.Length == 0 || args.Any(s => s.ToLower() == "--help" || s.ToLower() == "-h"))
                    return 0;
                else
                    return -1;
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
