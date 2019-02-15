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

namespace OpenTap.Cli
{
    /// <summary>
    /// Helper used to execute <see cref="ICliAction"/>s.
    /// </summary>
    public class CliActionExecutor
    {
        private static TraceSource log = Log.CreateSource("tap");
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Flush();
            if (e.ExceptionObject is AggregateException)
                Console.WriteLine("Error: " + String.Join(" ", ((AggregateException)e.ExceptionObject).InnerExceptions.Select(i => i.Message)));
            else if (e.ExceptionObject is Exception)
                Console.WriteLine("Error: " + ((Exception)e.ExceptionObject).ToString());
            else
                return;
            Environment.Exit(-4);
        }

        static bool tryParseEnumString(string str, Type type, out Enum result)
        {
            try
            {   // Look for an exact match.
                result = (Enum)Enum.Parse(type, str);
                var values = Enum.GetValues(type);
                if (Array.IndexOf(values, result) == -1)
                    return false;
                return true;
            }
            catch (ArgumentException)
            {
                // try a more robust parse method. (tolower, trim, '_'=' ')
                str = str.Trim().ToLower();
                var fixedNames = Enum.GetNames(type).Select(name => name.Trim().ToLower()).ToArray();
                for (int i = 0; i < fixedNames.Length; i++)
                {
                    if (fixedNames[i] == str || fixedNames[i].Replace('_', ' ') == str)
                    {
                        result = (Enum)Enum.GetValues(type).GetValue(i);
                        return true;
                    }
                }
            }
            result = null;
            return false;
        }

        private static List<IPlatformRequest> WaitForInputRequest(List<IPlatformRequest> requests, TimeSpan timeout, PlatformInteraction.RequestType requestType, string title)
        {
            Log.Flush();

            if (string.IsNullOrWhiteSpace(title) == false)
            {
                Console.WriteLine(title);
            }

            foreach (var message in requests)
            {
                start:
                Console.Write(message.Message);

                if (message.ResponseType == typeof(bool))
                    Console.WriteLine(" [y/n]?");
                else
                    Console.WriteLine();

                var read = (Console.ReadLine() ?? "").Trim();

                try
                {
                    if (message.ResponseType.IsEnum)
                    {
                        Enum response;
                        bool ok = tryParseEnumString(read, message.ResponseType, out response);
                        if (ok)
                            message.Response = response;
                        else
                            throw new FormatException(string.Format("Unable to parse '{0}'", read));
                    }
                    else
                        message.Response = StringConvertProvider.FromString(read, message.ResponseType, null);
                }
                catch (Exception)
                {
                    Console.WriteLine("Unable to parse '{0}' as a '{1}'", read, message.ResponseType);
                    if (message.ResponseType.IsEnum)
                    {
                        Console.WriteLine("Please write one of the following:");
                        var names = Enum.GetNames(message.ResponseType);
                        var values = Enum.GetValues(message.ResponseType);
                        for (int i = 0; i < names.Length; i++)
                            Console.WriteLine("* {0} ({1})", names[i], (int)values.GetValue(i));
                        Console.WriteLine();
                    }
                    goto start;
                }
            }

            return requests;
        }

        /// <summary>
        /// Used as entrypoint for the command line interface of TAP (PluginManager must have searched for assemblies before this method is called)
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Execute()
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

            string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Find the called action
            if(PluginManager.LocateTypeData(typeof(ICliAction).FullName).DerivedTypes == null)
            {
                Console.WriteLine("No commands found. Please try reinstalling TAP.");
                Environment.ExitCode = 1;
                return;
            }

            var commands = PluginManager.LocateTypeData(typeof(ICliAction).FullName).DerivedTypes.Where(t => !t.Attributes.HasFlag(TypeAttributes.Abstract) && t.Display != null).ToList();
            TypeData selectedCommand = null;
            var requestedCommand = args.FirstOrDefault();

            var groupedCommands = commands.Where(p => !string.IsNullOrWhiteSpace(p.Display.Group.FirstOrDefault())).ToLookup(s => s.Display.Group.FirstOrDefault());
            var ungroupedCommands = commands.Where(p => string.IsNullOrWhiteSpace(p.Display.Group.FirstOrDefault()));

            if(groupedCommands.Contains(requestedCommand))
            {
                if(args.Length >= 2)
                {
                    string subcommand = args[1];
                    selectedCommand = groupedCommands[requestedCommand].FirstOrDefault(s => s.Display.Name == subcommand);
                }
            }

            if(selectedCommand == null)
                selectedCommand = ungroupedCommands.FirstOrDefault(s => s.Display.Name == requestedCommand);


            // Print default info
            if (selectedCommand == null)
            {
                Console.WriteLine("Usage: tap <command> [<subcommand>] [<args>]\n\n");
                Console.WriteLine("Valid commands are:\n");

                var availableCommands = commands
                    .Where(cmd => cmd.Display != null && cmd.IsBrowsable)
                    .Select(s => string.IsNullOrWhiteSpace(s.Display.Group.FirstOrDefault()) ? s.Display.Name : s.Display.Group.FirstOrDefault())
                    .Distinct()
                    .ToList();

                foreach (var command in availableCommands.OrderBy(s => s))
                {
                    if(groupedCommands.Any(s => s.Key == command))
                    {
                        Console.WriteLine($"  {command}");
                        foreach(var subcommand in groupedCommands.Where(s => s.Key == command).FirstOrDefault())
                        {
                            if(subcommand.IsBrowsable)
                                Console.WriteLine($"    {subcommand.Display.Name.PadRight(22)}{subcommand.Display.Description}");
                        }
                    }
                    else
                    {
                        TypeData ungrped = ungroupedCommands.FirstOrDefault(s => s.Display.Name == command);
                        if (ungrped.IsBrowsable)
                            Console.WriteLine($"  {ungrped.Display.Name.PadRight(24)}{ungrped.Display.Description}");
                    }

                }
                Console.WriteLine("\nRun \"tap.exe <command> [<subcommand>] -h\" to get additional help for a specific command.\n");

                if (args.Count() == 0 || args.Any(s => s.ToLower() == "--help" || s.ToLower() == "-h"))
                    Environment.ExitCode = 0;
                else
                    Environment.ExitCode = -1;
                return;
            }

            if(selectedCommand.Load() != typeof(RunCliAction)) // RunCliAction has --non-interactive flag and custom platform interaction handling.
                PlatformInteraction.WaitForInput += WaitForInputRequest;

            // Create action and attach handlers
            var startupListener = new ConsoleTraceListener(false,false,false);
            Log.AddListener(startupListener);
            Type selectedType = selectedCommand.Load();
            if(selectedType == null)
            {
                Environment.ExitCode = -2;
                Console.WriteLine("Error loading command {0}", selectedCommand.FullName);
                return;
            }
            var inst = selectedType.CreateInstance();
            var packageAction = (ICliAction)inst;
            Log.RemoveListener(startupListener);

            if (packageAction == null)
            {
                Environment.ExitCode = -3;
                Console.WriteLine("Error instanciating command {0}", selectedCommand.FullName);
                return;
            }

            //packageAction.ProgressUpdate += (p, message) => log.Debug("{0}% {1}", p, message);
            //packageAction.Error += ex => log.Error(ex);


            try
            {
                int skip = string.IsNullOrWhiteSpace(selectedCommand.Display.Group.FirstOrDefault()) ? 1 : 2; // If the selected command has a group, it takes two arguments to use the command. E.g. "package create". If not, it only takes 1 argument, E.g. "restapi".
                Environment.ExitCode = packageAction.Execute(args.Skip(skip).ToArray());
            }
            catch (ExitCodeException ec)
            {
                log.Error(ec.Message);
                Environment.ExitCode = ec.ExitCode;
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
                Environment.ExitCode = -1;
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                log.Debug(ex);
                Environment.ExitCode = -1;
            }

            Log.Flush();
        }

    }
   
}
