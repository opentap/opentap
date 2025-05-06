//            Copyright Keysight Technologies 2012-2025
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Resources.NetStandard;
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

    /// <summary>
    /// Whether or not to overwrite existing files
    /// </summary>
    [CommandLineArgument("overwrite", ShortName = "f", Description = "Overwrite existing translation file")]
    public bool Overwrite { get; set; } = false;

    private static readonly TraceSource log = Log.CreateSource("Translate");

    /// <inheritdoc/>
    public int Execute(CancellationToken cancellationToken)
    {
        // Ensure translations are generated for the default language (english)
        using var session = Session.Create(SessionOptions.OverlayComponentSettings);
        EngineSettings.Current.Language = new CultureInfo("en");

        var install = Installation.Current;
        var pkg = install.FindPackage(Package);
        if (pkg == null)
        {
            log.Error($"Package '{Package}' is not installed.");
            return 1;
        }

        var outputdir = Path.Combine(install.Directory, "translations");
        var outputFileName = Path.Combine(outputdir, pkg.Name + ".resx");
        if (!Directory.Exists(outputdir))
            Directory.CreateDirectory(outputdir);

        var types = new List<ITypeData>();
        // first add all plugins
        types.AddRange(TypeData.GetDerivedTypes<ITapPlugin>()); 
        types = [.. types.Distinct()];

        void recursivelyAddReferencedTypes(ITypeData td, HashSet<ITypeData> seen)
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
                        recursivelyAddReferencedTypes(mem.TypeDescriptor, seen);
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

        types = seen.ToList();

        var typesSources = types.Select(x => Path.GetFullPath(TypeData.GetTypeDataSource(x).Location))
            .Where(x => x.StartsWith(install.Directory, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Substring(install.Directory.Length + 1))
            .ToArray();

        var outdir = Path.GetDirectoryName(outputFileName);
        if (!string.IsNullOrWhiteSpace(outdir))
            Directory.CreateDirectory(outdir);

        var OutputFileNameEng = Path.Combine(install.Directory, "translations", $"{pkg.Name}.resx");
        using var writer = new ResXResourceWriter(OutputFileNameEng);
        var packageFiles = new HashSet<string>(pkg.Files.Select(x => x.FileName), StringComparer.OrdinalIgnoreCase);
        List<ITypeData> packageTypes = new();
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
                StringComparer.OrdinalIgnoreCase);

        foreach (var grp in typesBySource)
        {
            foreach (var type in grp)
            {
                if (skipType(type))
                    continue;

                if (type.DescendsTo(typeof(StringLocalizer)) && type.CanCreateInstance && type.CreateInstance() is { } t)
                {
                    // special handling for string localizers
                    // We need to write all fields
                    WriteClassFields(writer, t);
                    continue;
                }

                if (type.DescendsTo(typeof(Enum)) && AsTypeData(type)?.Type is Type enumType)
                {
                    // Special handling for enums. We need to write each enum variant
                    WriteEnumMembers(writer, enumType);
                    continue;
                }
                var members = type.GetMembers().ToArray();
                var typeDisplay = type.GetDisplayAttribute();

                WriteAttribute(writer, type.Name, typeDisplay);
                foreach (var mem in members)
                {
                    if (skipMem(type, mem))
                        continue;
                    var memDisplay = mem.GetDisplayAttribute();
                    WriteAttribute(writer, $"{type.Name}.{mem.Name}", memDisplay);
                }
            }
        }

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

    private static void WriteEnumMembers(IResourceWriter writer, Type enumType)
    {
        var names = Enum.GetNames(enumType);
        foreach (var name in names)
        {
            MemberInfo type = enumType.GetMember(name).FirstOrDefault();
            DisplayAttribute attr = type.GetCustomAttribute<DisplayAttribute>();
            if (attr == null)
            {
                attr = new DisplayAttribute(type.Name, null, Order: -10000, Collapsed: false);
            }
            WriteAttribute(writer, $"{enumType.FullName}.{name}", attr);
        }
    }

    private static void WriteClassFields(IResourceWriter writer, object obj)
    {
        var t = obj.GetType();
        foreach (var fld in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            writer.AddResource($"{t.FullName}.{fld.Name}", fld.GetValue(obj));
        }
    }

    private static void WriteAttribute(IResourceWriter writer, string prefix, DisplayAttribute disp)
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

    static bool skipType(ITypeData type)
    {
        if (type.DescendsTo(typeof(StringLocalizer))) return false;
        if (type.DescendsTo(typeof(Enum))) return false;
        // Skip types that cannot be instantiated if they do not have any members.
        if (!type.CanCreateInstance && !type.GetMembers().Any(mem => !skipMem(type, mem)))
            return true;
        return false;
    }

    static bool skipMem(ITypeData type, IMemberData mem)
    {
        // If this member is inherited, the translation should happen in the base class.
        return !Equals(mem.DeclaringType, type);
    }
}
