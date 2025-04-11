//            Copyright Keysight Technologies 2012-2025
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Resources.NetStandard;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Package.Translation;

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
        Type[] PluginTypes = [
            typeof(ITypeData),
            typeof(IComponentSettings),
            /* TODO: Doesn't work. Enums are needed because they may be used as AvailableValues in other plugins,
             so it is not really possible to know if they are required or not. By default, we add all enums
             */
            typeof(Enum),
            typeof(ICliAction),
        ];

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
        var pluginTypes = TypeData.GetDerivedTypes<ITapPlugin>();

        // BUG: Enums not correctly discovered
        // BUG: Enum members not correctly discovered
        // TODO: string AvailableValues cannot be supported
        // BUG: Embedded member lookup not working (although embedding works fine)
        // TODO: Update type scanning logic? If a type has a member which depends on a type which was not added,
        // just add that type? (Unless the type belongs to another package)

        static void AddEmbeddedMembers(ITypeData td, HashSet<ITypeData> added)
        {
            try
            { 
                foreach (var mem in td.GetMembers())
                {
                    if (mem.HasAttribute<EmbedPropertiesAttribute>())
                    {
                        // If this type was already added, we are in a recursive loop.
                        // This is okay, we can safely terminate the loop now.
                        if (!added.Add(mem.TypeDescriptor))
                            return;
                        // Embedded members can be recursive
                        AddEmbeddedMembers(mem.TypeDescriptor, added);
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        // Embedded types are not necessarily plugins. Those must also be added.
        var embeddedTypes = new HashSet<ITypeData>();
        foreach (var plug in pluginTypes)
        {
            AddEmbeddedMembers(plug, embeddedTypes);
        }
        
        types.AddRange(embeddedTypes); 
        // enumTypes are not considered plugins, but should be included.
        types.AddRange(TypeData.GetDerivedTypes<Enum>());

        types = types.Distinct().ToList();

        var typesSources = types.Select(x => Path.GetFullPath(TypeData.GetTypeDataSource(x).Location))
            .ToArray();
        
        var outdir = Path.GetDirectoryName(outputFileName);
        if (!string.IsNullOrWhiteSpace(outdir))
            Directory.CreateDirectory(outdir);

        var OutputFileNameEng = Path.Combine(install.Directory, "translations", $"{pkg.Name}.resx");
        using var writer = new ResXResourceWriter(OutputFileNameEng);
        var packageFiles = pkg.Files.Select(x => x.FileName).ToHashSet();
        List<ITypeData> packageTypes = new();
        {
            for (int i = 0; i < typesSources.Length; i++)
            {
                if (typesSources[i] == null) continue;
                if (packageFiles.Contains(typesSources[i]))
                { 
                    packageTypes.Add(types[i]);
                }
            }
        }

        var typesBySource =
            packageTypes.GroupBy(tp => Path.GetFullPath(TypeData.GetTypeDataSource(tp).Location),
                StringComparer.OrdinalIgnoreCase);

        static void WriteAttribute(IResourceWriter writer, string prefix, DisplayAttribute disp)
        { 
            writer.AddResource($"{prefix}.DisplayName", disp.Name ?? "");
            if (!string.IsNullOrWhiteSpace(disp.Description))
                writer.AddResource($"{prefix}.DisplayDescription", disp.Description);
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (disp.Order != DisplayAttribute.DefaultOrder)
            {
                writer.AddResource($"{prefix}.DisplayOrder", disp.Order);
            }

            if (disp.Group.Length > 0)
            { 
                writer.AddResource($"{prefix}.Group", string.Join(" \\ ", disp.Group));
            }
        }
        foreach (var grp in typesBySource)
        {
            foreach (var type in grp)
            {
                if (skipType(type))
                    continue;
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
    static bool skipType(ITypeData type)
    {
        // Skip types that cannot be instantiated if they do not have any members.
        if (!type.CanCreateInstance && !type.GetMembers().Any(mem => !skipMem(type, mem)))
            return true;
        return false;
    }

    static bool skipMem(ITypeData type, IMemberData mem)
    {
        // If this member is inherited, the translation should happen in the base class.
        if (mem.DeclaringType != type) return true;
        return false;
    }

    static void AddDisplayAttributes(XElement element, DisplayAttribute disp)
    { 
        element.SetAttributeValue(TranslationHelpers.DisplayNameAttributeName, disp.Name);
        element.SetAttributeValue(TranslationHelpers.DisplayDescriptionAttributeName, disp.Description);
        if (Math.Abs(disp.Order - DisplayAttribute.DefaultOrder) > 0.1)
            element.SetAttributeValue(TranslationHelpers.DisplayOrderAttributeName, disp.Order);
    }

    static XElement EncapsulateInGroup(XElement element, DisplayAttribute disp)
    {
        var root = element;
        foreach (var grp in disp.Group.Reverse())
        {
            var elm = new XElement(TranslationHelpers.DisplayGroupElementName);
            elm.SetAttributeValue(TranslationHelpers.DisplayNameAttributeName, grp); 
            elm.Add(root);
            root = elm;
        }

        return root;
    }

    static void MergeGroupElements(XElement root)
    {
        var groups = root.Elements(TranslationHelpers.DisplayGroupElementName)
            .GroupBy(g => g.Attribute(TranslationHelpers.DisplayNameAttributeName).Value);

        foreach (var g in groups)
        {
            var elems = g.ToArray();
            if (elems.Length < 2) continue;
            var fst = elems.First();
            // Transplant children of other elements into this one
            foreach (var e in elems.Skip(1))
            { 
                foreach (var sub in e.Elements())
                {
                    fst.Add(sub); 
                }
                e.Remove();
            }
            // Merge subgroups in the new super element
            MergeGroupElements(fst);
        }
    }
}

