//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using OpenTap.Cli;

namespace OpenTap.Package;

/// <summary>
/// This class contains helpful utilities for creating translations of packages.
/// </summary
[Display("translate", Group: "sdk", Description: "Helpful tools for creating translations of packages.\nVery cool!.")]
public class TranslateAction : ICliAction
{
    /// <summary>
    /// The package to translate
    /// </summary>
    [UnnamedCommandLineArgument(nameof(Package), Description = "The package to translate.")]
    public string Package { get; set; }

    /// <summary>
    /// The language of the translation
    /// </summary>
    [CommandLineArgument("language", ShortName = "l", Description = "The language of the translation.")]
    public string Language { get; set; }

    /// <summary>
    /// Where to put the template
    /// </summary>
    [CommandLineArgument("out", ShortName = "o", Description = "Output file name")]
    public string OutputFileName { get; set; }

    private static readonly TraceSource log = Log.CreateSource("Translate");
    private static string GetRelativeFilePathNormalized(Installation install, ITypeData tp)
    {
        var sourceFile = TypeData.GetTypeDataSource(tp).Location;
        if (string.IsNullOrWhiteSpace(sourceFile)) return null;

        var assemblyPath = Path.GetFullPath(sourceFile);
        var installPath = Path.GetFullPath(install.Directory);

        // The assembly must be rooted in the installation
        if (assemblyPath.StartsWith(installPath) == false)
            return null;
        string relativePath = assemblyPath.Substring(installPath.Length).Replace('\\', '/').TrimStart('/', '\\');
        return relativePath;
    }

    const string IsoLanguageAttributename = "ISO";
    const string LanguageAttributename = "Language";
    const string PropertyNameAttributeName = "ID";
    const string DisplayNameAttributeName = "DisplayName";
    const string DisplayDescriptionAttributeName = "Description";
    const string TypeNameAttributeName = "ID";
    const string MemberElementName = "Property";
    const string RootElementName = "Translation";
    const string TypeElementName = "Class";
    const string PackageNameAttribute = "Package";
    const string FileElementName = "File";
    const string SourceAttributeName = "Source";

    /// <inheritdoc/>
    public int Execute(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Language))
        {
            log.Error("Please specify a language.");
            return 1;
        }

        bool cultureKnown;
        CultureInfo culture = CultureInfo.CurrentCulture;
        try
        {
            culture = new CultureInfo(Language);
            cultureKnown = culture.CultureTypes.HasFlag(CultureTypes.UserCustomCulture) == false;
        }
        catch (Exception ex)
        {
            cultureKnown = false;
        }

        if (!cultureKnown)
        {
            log.Error($"The specified language '{Language}' is not valid. Please provide a two-letter ISO code.");
            return 1;
        }

        log.Info($"Creating translation template for package '{Package}' to language '{culture.TwoLetterISOLanguageName}' ({culture.NativeName}).");
        var install = Installation.Current;
        var pkg = install.FindPackage(Package);
        if (pkg == null)
        {
            log.Error($"Package {Package} is not installed.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(OutputFileName))
        {
            OutputFileName = $"{pkg.Name}_{culture.TwoLetterISOLanguageName}.xml";
        }


        var packageTypes = TypeData.GetDerivedTypes<ITapPlugin>()
            .Where(x => install.FindPackageContainingType(x) == pkg).ToArray();

        XElement translationFile = new(RootElementName);
        translationFile.SetAttributeValue(PackageNameAttribute, pkg.Name);
        translationFile.SetAttributeValue(LanguageAttributename, culture.NativeName);
        translationFile.SetAttributeValue(IsoLanguageAttributename, culture.TwoLetterISOLanguageName);

        var typesBySource = packageTypes.GroupBy(tp => GetRelativeFilePathNormalized(install, tp));
        foreach (var grp in typesBySource)
        {
            var sourceFile = grp.Key;
            var elem = new XElement(FileElementName);
            elem.SetAttributeValue(SourceAttributeName, sourceFile);
            foreach (var type in grp)
            {
                var members = type.GetMembers().ToArray();
                if (members.Length == 0) continue;
                var typename = type.Name;
                var display = type.GetDisplayAttribute();
                var typeElem = new XElement(TypeElementName);
                typeElem.SetAttributeValue(DisplayNameAttributeName, display.Name);
                typeElem.SetAttributeValue(DisplayDescriptionAttributeName, display.Description);
                typeElem.SetAttributeValue(TypeNameAttributeName, typename);
                foreach (var mem in members)
                {
                    var memElem = new XElement(MemberElementName);
                    var memDisplay = mem.GetDisplayAttribute();
                    memElem.SetAttributeValue(DisplayNameAttributeName, memDisplay.Name);
                    memElem.SetAttributeValue(DisplayDescriptionAttributeName, memDisplay.Description);
                    memElem.SetAttributeValue(PropertyNameAttributeName, mem.Name);
                    typeElem.Add(memElem);
                }
                elem.Add(typeElem);
            }
            translationFile.Add(elem);
        }

        var ms = new MemoryStream();
        translationFile.Save(ms);
        var content = ms.ToArray();
        var lines = Encoding.UTF8.GetString(content).Split('\n').Select(x => x.Trim('\r')).ToList();
        // first line contains encoding information
        List<string> toInsert = [
           "<!-- NOTE: Only translate the content of the 'DisplayName' and 'Description' elements.",
           "  All other attributes are used by OpenTAP to identify what the translation applies to.",
           "-->"
        ];
        for (int i = 0; i < toInsert.Count; i++)
        {
            lines.Insert(i + 1, toInsert[i]);
        }

        try
        {
            File.WriteAllLines(OutputFileName, lines);
            log.Info($"Wrote template to '{OutputFileName}'.");
        }
        catch (Exception ex)
        {
            log.Error($"Error writing file: {ex.Message}");
        }

        return 0;
    }
}
