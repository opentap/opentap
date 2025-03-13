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
using System.Xml.Linq;
using System.Text;

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

            HashSet<ITypeData> overridenTypes = commands.Where(c => c.HasAttribute<OverrideCliActionAttribute>())
                .Select(c => TypeData.FromType(c.GetAttribute<OverrideCliActionAttribute>().OverrideType))
                .Cast<ITypeData>()
                .ToHashSet();

            foreach (var item in commands)
            {
                if (!overridenTypes.Contains(item))
                {
                    ParseCommand(item, item.GetDisplayAttribute().Group, Root);
                }
            }
        }

        CliActionTree(CliActionTree parent, string name)
        {
            Name = name;
            Root = parent.Root;
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
    /// Used to override a CLI action with a new CLI action.
    /// Adding this attribute, removes the old CLI action, and adds a new one in its place.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class OverrideCliActionAttribute : Attribute
    {
        /// <summary>
        /// The type to override.
        /// </summary>
        public Type OverrideType { get; set; }

        /// <summary>
        /// Used to override a CLI action with a new CLI action.
        /// Adding this attribute, removes the old CLI action, and adds a new one in its place.
        /// </summary>
        /// <param name="overrideType">
        /// The type to override.
        /// </param>
        public OverrideCliActionAttribute(Type overrideType)
        {
            OverrideType = overrideType;
        }
    }

    /// <summary>
    /// Helper used to execute <see cref="ICliAction"/>s.
    /// </summary>
    public class CliActionExecutor
    {
        internal static ITypeData SelectedAction = null;
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
            try
            {
                // Turn off the default system behavior when CTRL+C is pressed. 
                // When Console.TreatControlCAsInput is false, CTRL+C is treated as an interrupt instead of as input.
                // Assigning to this property while the property is running in the background will cause it to be suspended until 
                // it becomes the foreground process. Therefore we should only assign it if it is not already set to false
                if (Console.TreatControlCAsInput)
                    Console.TreatControlCAsInput = false;
            }
            catch { }
            var execThread = TapThread.Current;

            void abort()
            {
                execThread.AbortNoThrow();
            }

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                abort();
            };
            // Signals are not supported on Windows.
            if (OperatingSystem.Current != OperatingSystem.Windows)
            {
                PosixSignals.SignalCallback handler = context =>
                {
                    abort();
                    context.Cancel = true;
                };
                PosixSignals.AddSignalHandler(PosixSignals.SIGINT, handler);
                PosixSignals.AddSignalHandler(PosixSignals.SIGTERM, handler);
            }

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Find the called action
            if(!TypeData.GetDerivedTypes<ICliAction>().Any())
            {
                Console.WriteLine("No commands found. Please try reinstalling OpenTAP.");
                return (int)ExitCodes.UnknownCliAction;
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
                    var dir = ExecutorClient.ExeDir;
                    logpath = Path.Combine(dir, logpath);
                }

                SessionLogs.Rename(logpath);
            }
            catch (Exception e)
            {
                log.Error("Path defined in Engine settings contains invalid characters: {0}", EngineSettings.Current.SessionLogPath);
                log.Debug(e);
            }

            // Find selected command
            var actionTree = new CliActionTree();
            var selectedcmd = actionTree.GetSubCommand(args);
            if (selectedcmd?.Type != null && selectedcmd?.SubCommands.Any() != true)
                SelectedAction = selectedcmd.Type;

            // Run check for update
            TapThread.Start(() =>
            {
                TapThread.Sleep(3000); // don't spend time on update checking for very short running actions (e.g. 'tap package list -i')
                using (CliUserInputInterface.AcquireUserInputLock())
                {
                    try
                    {
                        var checkUpdatesCommands = actionTree.GetSubCommand(new[] {"package", "check-updates"});
                        var checkUpdateAction = checkUpdatesCommands?.Type?.CreateInstance() as ICliAction;
                        if (SelectedAction != checkUpdatesCommands?.Type)
                            checkUpdateAction?.PerformExecute(new[] {"--startup"});
                    }
                    catch (Exception e)
                    {
                        log.Error(e);
                    }
                }
            });

            void print_command(CliActionTree cmd, int level, int descriptionStart)
            {
                if (cmd.IsBrowsable)
                {
                    // Calculate amount of characters to pad right before description start to ensure description alignments.
                    int relativePadding = level * LevelPadding; 

                    var sb = new StringBuilder();
                    sb.Append("".PadRight(relativePadding));
                    sb.Append(cmd.Name.PadRight(descriptionStart - relativePadding));

                    if (cmd.Type?.IsBrowsable() ?? false)
                    {
                        sb.Append(cmd.Type.GetDisplayAttribute().Description);
                    }

                    log.Info(sb.ToString());

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
            if (SelectedAction == null)
            {
                string getVersion()
                {
                    // We cannot access the 'OpenTap.Package.Installation.Current' from Engine. Parse the XML instead.
                    var xmlFile = Path.Combine(ExecutorClient.ExeDir, "Packages", "OpenTAP", "package.xml");
                    if (File.Exists(xmlFile))
                    {
                        try
                        {
                            var pkg = XElement.Load(xmlFile);
                            if (pkg.Attribute("Version") is XAttribute x) return x.Value;
                        }
                        catch
                        {
                            // This is fine to silently ignore
                        }
                    }

                    return TypeData.FromType(typeof(CliActionExecutor)).Assembly.SemanticVersion.ToString(); // OpenTAP is not installed. lets just return this. 
                }
                string tapCommand = OperatingSystem.Current == OperatingSystem.Windows ? "tap.exe" : "tap";

                var helpOptions = new string[] { "--help", "-help", "-h" };
                bool isHelp = args.Length == 0 || args.Any(a => helpOptions.Contains(a.ToLower()));

                if (!isHelp)
                {
                    log.Error($"\"{tapCommand} {string.Join(" ", args)}\" is not a recognized command.");
                }

                log.Info("OpenTAP Command Line Interface ({0})", getVersion());
                log.Info($"Usage: \"{tapCommand} <command> [<subcommand(s)>] [<args>]\"\n");

                if (selectedcmd == null)
                {
                    log.Info("Valid commands are:");
                    foreach (var cmd in actionTree.SubCommands)
                    {
                        print_command(cmd, 0, actionTree.GetMaxCommandTreeLength(LevelPadding) + LevelPadding);
                    }
                }
                else
                {
                    log.Info($"Valid subcommands of");
                    print_command(selectedcmd, 0, actionTree.GetMaxCommandTreeLength(LevelPadding) + LevelPadding);
                }

                log.Info($"\nRun \"{tapCommand} <command> [<subcommand(s)>] -h\" to get additional help for a specific command.\n");

                if (isHelp)
                    return (int)ExitCodes.Success;
                else
                    return (int)ExitCodes.ArgumentParseError;
            }

            if (SelectedAction != TypeData.FromType(typeof(RunCliAction)) && UserInput.Interface == null) // RunCliAction has --non-interactive flag and custom platform interaction handling.          
                CliUserInputInterface.Load();

            ICliAction packageAction = null;
            try
            {
                packageAction = (ICliAction)SelectedAction.CreateInstance();
            }
            catch (TargetInvocationException e1) when (e1.InnerException is System.ComponentModel.LicenseException e)
            {
                log.Error("Unable to load CLI Action '{0}'", SelectedAction.GetDisplayAttribute().GetFullName());
                log.Info("{0}", e.Message);
                return (int)ExitCodes.UnknownCliAction;
            }
            catch (InvalidOperationException ex)
            {
                log.Error("Unable to load CLI Action '{0}'", SelectedAction.GetDisplayAttribute().GetFullName());
                log.Info("{0}", ex.Message);
                return (int)ExitCodes.GeneralException;
            }
            
            if (packageAction == null)
            {
                Console.WriteLine("Error instantiating command {0}", SelectedAction.Name);
                return (int)ExitCodes.UnknownCliAction;
            }

            try
            {
                int skip = SelectedAction.GetDisplayAttribute().Group.Length + 1; // If the selected command has a group, it takes two arguments to use the command. E.g. "package create". If not, it only takes 1 argument, E.g. "restapi".
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
                return (int)ExitCodes.ArgumentError;
            }
            catch (OperationCanceledException ex)
            {
                log.Error(ex.Message);
                return (int)ExitCodes.UserCancelled;
            }
            catch (Exception ex)
            {
                Log.Flush();
                if (ex is AggregateException aex)
                {
                    foreach (var innerException in aex.InnerExceptions)
                        log.Error(innerException.Message);
                }
                
                log.Error("{0}", ex.Message);
                log.Debug(ex);
                return (int)ExitCodes.GeneralException;
            }
            finally
            {
                Log.Flush();
            }
        }
    }
}
