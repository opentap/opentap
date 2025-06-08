//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

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
            var td = TypeData.GetTypeData(action);
            if (td == null)
                throw new Exception("Type data is null for action: " + action.GetType().Name);
            var props = td.GetMembers();

            ap.AllOptions.Add("help", 'h', false, "Write help information.");
            ap.AllOptions.Add("verbose", 'v', false, "Show verbose/debug-level log messages.");
            ap.AllOptions.Add("color", 'c', false, "Color messages according to their severity.");
            ap.AllOptions.Add("quiet", 'q', false, "Quiet console logging.");
            ap.AllOptions.Add("log", description: "Specify log file location. Default is ./SessionLogs.");

            var argToProp = new Dictionary<string, IMemberData>();
            var unnamedArgToProp = new List<IMemberData>();

            var overrides = new HashSet<string>();

            foreach (var prop in props)
            {
                if (prop.Readable == false || prop.Writable == false)
                    continue;

                if (prop.HasAttribute<UnnamedCommandLineArgument>())
                {
                    unnamedArgToProp.Add(prop);
                    continue;
                }

                if (!prop.HasAttribute<CommandLineArgumentAttribute>())
                    continue;

                var attr = prop.GetAttribute<CommandLineArgumentAttribute>();

                var needsArg = prop.TypeDescriptor != TypeData.FromType(typeof(bool));

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

                if (ap.AllOptions.Contains(attr.Name))
                {
                    overrides.Add(attr.Name);
                }

                if (!string.IsNullOrWhiteSpace(attr.ShortName))
                {
                    var overriden = ap.AllOptions.FirstOrDefault(opt => opt.Value?.ShortName == attr.ShortName[0])
                        .Value;
                    if (overriden != null)
                    {
                        overrides.Add(overriden.ShortName.ToString());
                        // Set the ShortName of the overriden option to '\0' so the argument parser will resolve this ShortName to the overriding option
                        // The overriden option can still be accessed by its LongName, provided that is not also overriden
                        ap.AllOptions.Add(overriden.LongName, '\0', overriden.NeedsArgument, overriden.Description);
                    }
                }

                var arg = ap.AllOptions.Add(attr.Name, attr.ShortName?.FirstOrDefault() ?? '\0', needsArg, description);
                // attr.Visible has been obsoleted but is still considered for backward compatibility.
#pragma warning disable CS0618
                arg.IsVisible = attr.Visible && (prop.GetAttribute<BrowsableAttribute>()?.Browsable ?? true);
#pragma warning restore CS0618
                argToProp.Add(arg.LongName, prop);
            }

            var args = ap.Parse(parameters);

            if (args.MissingArguments.Any())
                throw new Exception(
                    $"Command line option '{args.MissingArguments.FirstOrDefault().LongName}' is missing an argument.");

            foreach (var @override in overrides)
            {
                if (args.Contains(@override))
                    log.Debug(
                        $"The CLI option '--{@override}' from '{action}' overrides a common CLI option from OpenTAP.");
                else if (@override.Length == 1)
                {
                    var shortName = @override[0];
                    var isUsed = args.Any(a => a.Value?.ShortName == shortName);
                    if (isUsed)
                        log.Debug(
                            $"The CLI option '-{@override}' from '{action}' overrides a common CLI option from OpenTAP.");
                }
            }

            if (!overrides.Contains("help") && args.Contains("help"))
            {
                printOptions(action.GetType().GetAttribute<DisplayAttribute>().Name, ap.AllOptions, unnamedArgToProp);
                return (int)ExitCodes.Success;
            }

            if (!overrides.Contains("log") && args.Contains("log"))
            {
                SessionLogs.Rename(args.Argument("log"));
            }

            foreach (var opts in args)
            {
                if (argToProp.ContainsKey(opts.Key) == false) continue;
                var prop = argToProp[opts.Key];

                if (prop.TypeDescriptor is TypeData propTd)
                {
                    Type propType2 = propTd.Load();

                    object getValue(string src, Type propType)
                    {
                        if (propType == typeof(string)) return src;
                        if (propType == typeof(bool)) return true;
                        if (propType.IsEnum)
                            return parseArbitrary(opts.Key, src, propType);
                        if (propType == typeof(int)
                            || propType == typeof(long)
                            || propType == typeof(uint)
                            || propType == typeof(ushort)
                            || propType == typeof(short)
                            || propType == typeof(byte)
                            || propType == typeof(sbyte)
                            || propType == typeof(ulong)
                            || propType == typeof(double)
                            || propType == typeof(float))
                            return Convert.ChangeType(src, propType);
                        throw new Exception(
                            $"Command line option '{opts.Key}' is of an unsupported type '{propType.Name}'.");
                    }

                    if (propType2 != typeof(string) && propType2.IsArray)
                    {
                        var array = Array.CreateInstance(propType2.GetElementType(), opts.Value.Values.Count);
                        var elemType = propType2.GetElementType();
                        for (int i = 0; i < array.Length; i++)
                            array.SetValue(getValue(opts.Value.Values[i], elemType), i);
                        prop.SetValue(action, array);
                    }
                    else
                    {
                        prop.SetValue(action, getValue(opts.Value.Value, propType2));
                    }
                }
                else
                    throw new Exception(
                        $"Command line option '{opts.Key}' is of an unsupported type '{prop.TypeDescriptor.Name}'.");
            }

            unnamedArgToProp = unnamedArgToProp.OrderBy(p => p.GetAttribute<UnnamedCommandLineArgument>().Order)
                .ToList();
            var requiredArgs = unnamedArgToProp.Where(x => x.GetAttribute<UnnamedCommandLineArgument>().Required)
                .ToHashSet();
            int idx = 0;

            for (int i = 0; i < unnamedArgToProp.Count; i++)
            {
                var p = unnamedArgToProp[i];

                if (p.TypeDescriptor.IsA(typeof(string)))
                {
                    if (idx < args.UnnamedArguments.Length)
                    {
                        p.SetValue(action, args.UnnamedArguments[idx++]);
                        requiredArgs.Remove(p);
                    }
                }
                else if (p.TypeDescriptor.IsA(typeof(string[])))
                {
                    if (idx < args.UnnamedArguments.Length)
                    {
                        p.SetValue(action, args.UnnamedArguments.Skip(idx).ToArray());
                        requiredArgs.Remove(p);
                    }

                    idx = args.UnnamedArguments.Length;
                }
                else if (p.TypeDescriptor is TypeData td2 && td2.Type.IsEnum)
                {
                    if (idx < args.UnnamedArguments.Length)
                    {
                        var name = p.GetAttribute<UnnamedCommandLineArgument>()?.Name ?? p.Name;
                        p.SetValue(action, parseArbitrary($"<{name}>", args.UnnamedArguments[idx++], td2.Type));
                        requiredArgs.Remove(p);
                    }
                }
            }

            if (args.UnknownsOptions.Any() || requiredArgs.Any())
            {
                if (args.UnknownsOptions.Any())
                    Console.WriteLine("Unknown option(s): " + string.Join(" ", args.UnknownsOptions));

                if (requiredArgs.Any())
                    Console.WriteLine("Missing argument(s): " + string.Join(" ",
                        requiredArgs.Select(p => p.GetAttribute<UnnamedCommandLineArgument>().Name)));

                printOptions(action.GetType().GetAttribute<DisplayAttribute>().Name, ap.AllOptions, unnamedArgToProp);
                return (int)ExitCodes.ArgumentParseError;
            }

            var actionFullName = td.GetDisplayAttribute().GetFullName();
            log.Debug($"Executing CLI action: {actionFullName}");
            var sw = Stopwatch.StartNew();
            int exitCode = action.Execute(TapThread.Current.AbortToken);
            log.Debug(sw, "CLI action returned exit code: {0}", exitCode);
            return exitCode;
        }

        static TraceSource log = Log.CreateSource("CLI");

        private static void printOptions(string passName, ArgumentCollection options, List<IMemberData> unnamed)
        {
            var namedArguments = string.Join(" ", options.Values.Where(x => x.IsVisible)
                .Select(x =>
                {
                    var str = x.ShortName != 0 ? $"-{x.ShortName}" : "--" + x.LongName;
                    if (x.NeedsArgument)
                        str += " <arg>";

                    return '[' + str + ']';
                }));
            var unnamedArguments = string.Join(" ", unnamed.Select(x =>
            {
                var attr = x.GetAttribute<UnnamedCommandLineArgument>();
                var str = attr.Name;

                if (attr.Required == false)
                    str = "[<" + str + ">]";
                else
                    str = "<" + str + ">";

                return str;
            }));
            Console.WriteLine($"Usage: {passName} {namedArguments} {unnamedArguments}"); 
            options.UnnamedArgumentData = unnamed;
            Console.Write(options);
        }

        private static object parseArbitrary(string name, string value, Type propertyType)
        {
            var obj = StringConvertProvider.FromString(value, TypeData.FromType(propertyType), null,
                System.Globalization.CultureInfo.InvariantCulture);

            if (obj == null)
                throw new Exception(string.Format(
                    "Could not parse argument '{0}'. Argument given: '{1}'. Valid arguments: {2}", name, value,
                    string.Join(", ", propertyType.GetEnumNames())));

            return obj;
        }
    }
}
