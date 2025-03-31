//            Copyright Keysight Technologies 2012-2025
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System.IO;
namespace OpenTap.Package;

internal static class TranslationHelpers
{
    // Update this if the schema is changed.
    // We probably need to maintain different parsers to maintain backward compatibility if we need to make major changes.
    public static readonly SemanticVersion SchemaVersion = new SemanticVersion(1, 0, 0, null, null);
    public const string OpenTapVersionAttributeName = "GeneratedByOpenTapVersion";
    public const string SchemaVersionAttributeName = "SchemaVersion";
    public const string IsoLanguageAttributename = "ISO";
    public const string LanguageAttributename = "Language";
    public const string PropertyIdAttributeName = "ID";
    public const string DisplayNameAttributeName = "Name";
    public const string DisplayGroupElementName = "Group";
    public const string DisplayOrderAttributeName = "Order";
    public const string DisplayDescriptionAttributeName = "Description";
    public const string TypeIdPropertyName = "ID";
    public const string MemberElementName = "Property";
    public const string TranslationElementName = "Translation";
    public const string TypeElementName = "Class";
    public const string PackageElementName = "Package";
    public const string PackageNameAttributeName = "Name";
    public const string PackageVersionAttributeName = "Version";
    public const string FileElementName = "File";
    public const string SourceAttributeName = "Path";

    internal static string GetRelativeFilePathNormalized(string from, ITypeData tp)
    {
        var sourceFile = TypeData.GetTypeDataSource(tp).Location;
        if (string.IsNullOrWhiteSpace(sourceFile)) return null;

        var assemblyPath = Path.GetFullPath(sourceFile);
        var installPath = Path.GetFullPath(from);

        // The assembly must be rooted in the installation
        if (assemblyPath.StartsWith(installPath) == false)
            return null;
        string relativePath = assemblyPath.Substring(installPath.Length).Replace('\\', '/').TrimStart('/', '\\');
        return relativePath;
    }
}

