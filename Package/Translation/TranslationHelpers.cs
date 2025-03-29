//            Copyright Keysight Technologies 2012-2025
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System.IO;
namespace OpenTap.Package;

internal static class TranslationHelpers
{
    public const string IsoLanguageAttributename = "ISO";
    public const string LanguageAttributename = "Language";
    public const string PropertyIdAttributeName = "ID";
    public const string DisplayNameAttributeName = "Name";
    public const string DisplayDescriptionAttributeName = "Description";
    public const string TypeIdPropertyName = "ID";
    public const string MemberElementName = "Property";
    public const string RootElementName = "Translation";
    public const string TypeElementName = "Class";
    public const string PackageNameAttribute = "Package";
    public const string FileElementName = "File";
    public const string SourceAttributeName = "Path";

    internal static string GetRelativeFilePathNormalized(Installation install, ITypeData tp)
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
}

