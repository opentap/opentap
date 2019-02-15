//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace OpenTap.Cli
{
    /// <summary>
    /// Helper class that enhances the <see cref="ICliAction">ICliAction</see> with extra extensions methods.
    /// </summary>
    internal static class ICliActionExtensions
    {
        /// <summary>
        /// Executes the action with the given parameters.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        /// <param name="parameters">The parameters for the action.</param>
        /// <returns></returns>
        public static int PerformExecute(this ICliAction action, string[] parameters)
        {
            ArgumentsParser ap = new ArgumentsParser();
            var props = action.GetType().GetProperties();

            ap.AllOptions.Add("help", 'h', false, "Write help information.");
            ap.AllOptions.Add("verbose", 'v', false, "Also show vebose/debug level messages.");
            ap.AllOptions.Add("color", 'c', false, "Color messages according to their level.");

            var argToProp = new Dictionary<string, PropertyInfo>();
            var unnamedArgToProp = new List<PropertyInfo>();

            foreach (var prop in props)
            {
                if (prop.GetSetMethod() == null || prop.GetGetMethod() == null)
                    continue;

                if (prop.HasAttribute<UnnamedCommandLineArgument>())
                {
                    unnamedArgToProp.Add(prop);
                    continue;
                }

                if (!prop.HasAttribute<CommandLineArgumentAttribute>())
                    continue;

                var attr = prop.GetAttribute<CommandLineArgumentAttribute>();

                var needsArg = prop.PropertyType != typeof(bool);

                string description = "";
                if (prop.HasAttribute<ObsoleteAttribute>())
                {
                    var obsoleteAttribute = prop.GetAttribute<ObsoleteAttribute>();
                    description = $"[OBSOLETE: {obsoleteAttribute.Message}] {attr.Description}";
                }
                else
                {
                    description = attr.Description;
                }

                var arg = ap.AllOptions.Add(attr.Name, attr.ShortName == null ? '\0' : attr.ShortName.FirstOrDefault(), needsArg, description);
                argToProp.Add(arg.LongName, prop);
            }

            var args = ap.Parse(parameters);
            var isVerbose = args.Contains("verbose");

            var cliTraceListener = new ConsoleTraceListener(isVerbose, false, args.Contains("color"));
            OpenTap.Log.AddListener(cliTraceListener);
            AppDomain.CurrentDomain.ProcessExit += (s, e) => cliTraceListener.Flush();

            if (isVerbose) args.Remove("verbose");
            if (args.Contains("color")) args.Remove("color");

            if (args.Contains("help"))
            {
                Console.WriteLine("Options:");
                printOptions(action.GetType().GetAttribute<DisplayAttribute>().Name, ap.AllOptions, unnamedArgToProp);
                return 0;
            }

            foreach (var opts in args)
            {
                var prop = argToProp[opts.Key];

                if (prop.PropertyType == typeof(Boolean)) prop.SetValue(action, true);
                else if (prop.PropertyType.IsEnum) prop.SetValue(action, ParseEnum(opts.Key, opts.Value.Value, prop.PropertyType));
                else if (prop.PropertyType == typeof(string)) prop.SetValue(action, opts.Value.Value);
                else if (prop.PropertyType == typeof(string[])) prop.SetValue(action, opts.Value.Values.ToArray());
                else throw new Exception(string.Format("Invalid command line option: {0}", opts.Key));
            }

            unnamedArgToProp = unnamedArgToProp.OrderBy(p => p.GetAttribute<UnnamedCommandLineArgument>().Order).ToList();
            var requiredArgs = unnamedArgToProp.Where(x => x.GetAttribute<UnnamedCommandLineArgument>().Required).ToHashSet();
            int idx = 0;

            for (int i = 0; i < unnamedArgToProp.Count; i++)
            {
                var p = unnamedArgToProp[i];

                if (p.PropertyType == typeof(string))
                {
                    if (idx < args.UnnamedArguments.Length)
                    {
                        p.SetValue(action, args.UnnamedArguments[idx++]);
                        requiredArgs.Remove(p);
                    }
                }
                else if (p.PropertyType == typeof(string[]))
                {
                    if (idx < args.UnnamedArguments.Length)
                    {
                        p.SetValue(action, args.UnnamedArguments.Skip(idx).ToArray());
                        requiredArgs.Remove(p);
                    }

                    idx = args.UnnamedArguments.Length;
                }
                else if (p.PropertyType.IsEnum)
                {
                    if (idx < args.UnnamedArguments.Length)
                    {
                        var name = p.GetCustomAttribute<UnnamedCommandLineArgument>()?.Name ?? p.Name;
                        p.SetValue(action, ParseEnum($"<{name}>", args.UnnamedArguments[idx++], p.PropertyType));
                        requiredArgs.Remove(p);
                    }
                }
            }

            if (args.UnknownsOptions.Any() || requiredArgs.Any())
            {
                if (args.UnknownsOptions.Any())
                    Console.WriteLine("Unknown options: " + string.Join(" ", args.UnknownsOptions));

                if (requiredArgs.Any())
                    Console.WriteLine("Missing argument: " + string.Join(" ", requiredArgs.Select(p => p.GetAttribute<UnnamedCommandLineArgument>().Name)));

                Console.WriteLine("Options:");
                printOptions(action.GetType().GetAttribute<DisplayAttribute>().Name, ap.AllOptions, unnamedArgToProp);
                return 0;
            }

            CancellationTokenSource source = new CancellationTokenSource();
            // Register handler for CTRL-C key press to enable user to cancel the run
            try
            {
                Console.TreatControlCAsInput = false; // Turn off the default system behavior when CTRL+C is pressed. When Console.TreatControlCAsInput is false, CTRL+C is treated as an interrupt instead of as input.
                Console.CancelKeyPress += (s, e) => 
                {
                    source.Cancel();
                    e.Cancel = true;
                };
            }
            catch (IOException)
            {
                if (isVerbose)
                    Log.CreateSource("CliAction").Warning("Handling of CTRL-C failed.");
            }
            
            return action.Execute(source.Token);
        }

        private static void printOptions(string passName, ArgumentCollection options, List<PropertyInfo> unnamed)
        {
            Console.WriteLine("Usage: {2} {0} {1}",
                string.Join(" ", options.Select(x =>
                {
                    var str = x.Value.ShortName != '\0' ? string.Format("-{0}", x.Value.ShortName) : "--" + x.Value.LongName;

                    if (x.Value.NeedsArgument)
                        str += " <arg>";

                    return '[' + str + ']';
                })),
                string.Join(" ", unnamed.Select(x =>
                {
                    var str = x.GetAttribute<UnnamedCommandLineArgument>().Name;

                    if (x.PropertyType == typeof(string[]))
                        str = "[" + str + "]";
                    else
                        str = "<" + str + ">";

                    return str;
                })), passName);

            foreach (var option in options)
            {
                var opt = option.Value;
                var arg = "--" + opt.LongName;
                if (opt.ShortName != default(char))
                {
                    arg = String.Format("-{0}, {1}", opt.ShortName, arg);
                }

                arg = "  " + arg;
                if (!string.IsNullOrEmpty(opt.Description))
                {
                    foreach (var descSplit in opt.Description.Split('\n'))
                    {
                        arg = arg + new String(' ', Math.Max(25 - arg.Length, 1)) + descSplit;
                        Console.WriteLine(arg);
                        arg = "";
                    }
                }
                else
                    Console.WriteLine(arg);
            }
        }
        
        private static object ParseEnum(string name, string value, Type propertyType)
        {
            var conv = new StringConvertPlugins.EnumStringConvertProvider();
            var obj = conv.FromString(value, propertyType, null, System.Globalization.CultureInfo.InvariantCulture);

            if (obj == null)
                throw new Exception(string.Format("Could not parse argument '{0}'. Argument given: '{1}'. Valid arguments: {2}", name, value, string.Join(", ", propertyType.GetEnumNames())));

            return obj;
        }
    }
}
