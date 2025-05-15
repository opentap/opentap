//            Copyright Keysight Technologies 2012-2025
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Threading;
using OpenTap.Cli;
using OpenTap.Package;
using OpenTap.Translation;

namespace OpenTap.Sdk.New;

/// <summary>
/// This class contains helpful utilities for creating translations of packages.
/// </summary>
[Display("translate", Group: "sdk", Description: "Create a new translation template for a package.")]
public class TranslateAction : ICliAction
{
    /// <summary>
    /// The packages to translate
    /// </summary>
    [UnnamedCommandLineArgument(nameof(Package), Description = "The package to translate.")]
    [Display("Package", "The packages to translate")]
    public string Package { get; set; }

    private static readonly TraceSource log = Log.CreateSource("Translate");

    /// <inheritdoc/>
    public int Execute(CancellationToken cancellationToken)
    {
        // Ensure translations are generated for the default language (english)
        using var session = Session.Create(SessionOptions.OverlayComponentSettings);
        EngineSettings.Current.Language = CultureInfo.InvariantCulture;

        var install = Installation.Current;
        if (string.IsNullOrWhiteSpace(Package))
        {
            log.Error($"Please specify a package name.");
            return 1;
        }
        var pkg = install.FindPackage(Package);
        if (pkg == null)
        {
            log.Error($"Package '{Package}' is not installed.");
            return 1;
        }

        var outputdir = TranslationManager.TranslationDirectory;
        var outputFileName = Path.Combine(outputdir, pkg.Name + ".resx");
        if (!Directory.Exists(outputdir))
            Directory.CreateDirectory(outputdir);

        var types = new List<ITypeData>();
        // first add all plugins
        types.AddRange(TypeData.GetDerivedTypes<ITapPlugin>());
        types = [.. types.Distinct()];

        static void recursivelyAddReferencedTypes(ITypeData td, HashSet<ITypeData> seen)
        {
            try
            {
                foreach (var mem in td.GetMembers())
                {
                    // These types would be filtered out later anyway, but let's just not even consider them
                    if (mem.TypeDescriptor.Name.StartsWith("System.")
                        || mem.TypeDescriptor.Name.StartsWith("Microsoft."))
                        continue;
                    if (seen.Add(mem.TypeDescriptor))
                    {
                        recursivelyAddReferencedTypes(mem.TypeDescriptor, seen);
                    }
                }
            }
            catch
            {
                // This happens if a typedata implementation throws in GetMembers()
                // We cannot really do anything about this
            }
        }

        var seen = new HashSet<ITypeData>(types);
        foreach (var td in types)
        {
            recursivelyAddReferencedTypes(td, seen);
        }

        types = [.. seen];

        {
            // We are not interested in creating a different translation for each 
            // variant of a generic type we use. 
            // Remove all instances of generic types, and add a single reference to the generic variant.
            // TODO: The translation implementation needs to support this. (add test)
            HashSet<TypeData> add = [];
            HashSet<TypeData> remove = [];
            foreach (var type in types)
            {
                if (AsTypeData(type) is { } td && td.Type.IsGenericType)
                {
                    remove.Add(td);
                    var gen = TypeData.FromType(td.Type.GetGenericTypeDefinition());
                    add.Add(gen);
                }
            }
            types.RemoveAll(remove.Contains);
            types.AddRange(add);
        }

        static string normalizePath(string path) => path.Replace('\\', '/');
        var typesSources = types.Select(x => Path.GetFullPath(TypeData.GetTypeDataSource(x).Location))
            .Where(x => x.StartsWith(install.Directory, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Substring(install.Directory.Length + 1))
            .Select(normalizePath)
            .ToArray();

        var outdir = Path.GetDirectoryName(outputFileName);
        if (!string.IsNullOrWhiteSpace(outdir))
            Directory.CreateDirectory(outdir);

        var writer = new ResXWriter(outputFileName);
        var packageFiles = new HashSet<string>(pkg.Files.Select(x => normalizePath(x.FileName)), StringComparer.OrdinalIgnoreCase);
        List<ITypeData> packageTypes = [];
        for (int i = 0; i < typesSources.Length; i++)
        {
            if (typesSources[i] == null) continue;
            if (packageFiles.Contains(typesSources[i]))
            {
                packageTypes.Add(types[i]);
            }
        }

        var typesBySource =
            packageTypes.GroupBy(tp => Path.GetFullPath(TypeData.GetTypeDataSource(tp).Location),
                StringComparer.OrdinalIgnoreCase).ToArray();

        if (!typesBySource.Any())
        { 
            log.Error($"0 types discovered for package '{Package}'. This is likely a bug.");
            return 1;
        }
        
        foreach (var grp in typesBySource)
        {
            foreach (var type in grp)
            {
                if (SkipType(type))
                    continue;

                if (type.DescendsTo(typeof(Enum)) && AsTypeData(type)?.Type is Type enumType)
                {
                    // Special handling for enums. We need to write each enum variant
                    WriteEnumMembers(writer, enumType);
                    continue;
                }

                if (type.DescendsTo(typeof(IStringLocalizer)) && type.CanCreateInstance && type.CreateInstance() is IStringLocalizer t)
                {
                    WriteStringLocalizerStrings(writer, t);
                }


                var members = type.GetMembers().ToArray();
                var typeDisplay = type.GetDisplayAttribute();

                WriteAttribute(writer, type.Name, typeDisplay);
                foreach (var mem in members)
                {
                    if (SkipMem(type, mem))
                        continue;
                    var memDisplay = mem.GetDisplayAttribute();
                    WriteAttribute(writer, $"{type.Name}.{mem.Name}", memDisplay);
                }
            }
        }
        
        writer.Generate();
        log.Info($"Created translation template file at {outputFileName}");

        return 0;
    }

    private static TypeData AsTypeData(ITypeData type)
    {
        do
        {
            if (type is TypeData td)
                return td;
            type = type?.BaseType;
        } while (type != null);
        return null;
    }

    private static void WriteEnumMembers(ResXWriter writer, Type enumType)
    {
        var names = Enum.GetNames(enumType);
        foreach (var name in names)
        {
            MemberInfo type = enumType.GetMember(name).FirstOrDefault();
            DisplayAttribute attr = type.GetCustomAttribute<DisplayAttribute>();
            attr ??= new DisplayAttribute(type.Name, null, Order: -10000, Collapsed: false);
            WriteAttribute(writer, $"{enumType.FullName}.{name}", attr);
        }
    }

    private static void WriteStringLocalizerStrings(ResXWriter writer, IStringLocalizer obj)
    {
        var t = obj.GetType();
        HashSet<string> added = [];
        Func<IStringLocalizer, string, string, CultureInfo, string> hook = (localizer, neutral, key, language) =>
        {
            var fullkey = $"{t.FullName}.{key}";
            if (added.Add(fullkey))
                writer.AddResource(fullkey, neutral);
            return neutral;
        };
        // inject hook
        var mgr = typeof(TranslationManager);
        mgr.GetField("TranslateFunction", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, hook);

        // we need to call the property getter for all properties to trigger all calls to Translate()
        foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            if (prop.PropertyType != typeof(string) && prop.PropertyType != typeof(FormatString)) continue;
            try
            {
                object owner = prop.GetGetMethod().IsStatic ? null : obj;
                prop.GetValue(owner);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static void WriteAttribute(ResXWriter writer, string prefix, DisplayAttribute disp)
    {
        writer.AddResource($"{prefix}.Name", disp.Name ?? "");
        if (!string.IsNullOrWhiteSpace(disp.Description))
            writer.AddResource($"{prefix}.Description", disp.Description ?? "");
        if (disp.Order != DisplayAttribute.DefaultOrder)
        {
            writer.AddResource($"{prefix}.Order", disp.Order);
        }

        if (disp.Group.Length > 0)
        {
            writer.AddResource($"{prefix}.Group", string.Join(" \\ ", disp.Group));
        }
    }

    static bool SkipType(ITypeData type)
    {
        try
        {
            if (type.DescendsTo(typeof(IStringLocalizer)))
            {
                if (type.CanCreateInstance) return false;
                // It is currently a requirement that IStringLocalizer can be instantiated.
                // In the future, we can improve the string detection algorithm to relax this requirement,
                // but for now we should warn the user that this will not work.
                log.Error($"String localizer '{type.Name}' does not have an empty constructor, and will not be translated.");
                return true;
            }
            if (type.DescendsTo(typeof(Enum))) return false; 
            {
                var members = type.GetMembers().ToArray();
                foreach (var mem in members)
                {
                    // If at least one member should be translated, we can't skip the type.
                    if (SkipMem(type, mem) == false)
                        return false;
                }
            }
        }
        catch (Exception ex)
        {
            // ignore. This can happen for bad typedata implementations. We should just ignore the type in this case
            // since we can't translate it if we can't enumerate the members.
            log.Error($"Error reflecting type '{type.Name}'. This type will not be translated.");
            log.Debug(ex);
        } 
        return true;
    }

    static bool SkipMem(ITypeData type, IMemberData mem)
    {
        try
        {
            // If this member is inherited, the translation should happen in the base class.
            if (!Equals(mem.DeclaringType, type))
                return true;
            // Skip the member if it is unbrowsable
            var browsable = mem.GetAttribute<BrowsableAttribute>()?.Browsable;
            if (browsable != null) return !browsable.Value;
            // Otherwise skip the member if it is not writable.
            // This is the primary factor determining whether or not something is visible in most UIs.
            return !mem.Writable;
        }
        catch (Exception ex)
        { 
            log.Error($"Error reflecting member '{type.Name}.{mem.Name}'. This member will not be translated.");
            log.Debug(ex);
            return true;
        }
    }
}