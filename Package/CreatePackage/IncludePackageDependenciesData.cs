using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace OpenTap.Package
{
    [Display("Include Package Dependencies")]
    class IncludePackageDependencies : ICustomPackageAction
    {
        /// <summary>
        /// Defines the IncludePackageDependencies XML element that indicates that the package should inherit plugin dependencies from this file.
        /// </summary>
        [Display("IncludePackageDependencies")]
        class IncludePackageDependenciesData : ICustomPackageData
        {

        }
        /// <summary>
        /// It doesn't really matter when this is run.
        /// </summary>
        /// <returns></returns>
        public int Order() => 10;

        public PackageActionStage ActionStage => PackageActionStage.Create;
        private static TraceSource log = Log.CreateSource(nameof(IncludePackageDependencies));

        public bool Execute(PackageDef pkgDef, CustomPackageActionArgs customActionArgs)
        {
            // Add all package dependencies specified in an xml file with the following hierarchy to 'pkgDef'
            // <Package.Dependencies>
            //     <Package Name="a" Version="b" />    
            // </Package.Dependencies>
            bool success = true;
            foreach (var item in pkgDef.Files)
            {
                // Dependencies should not be included from this file -- skip
                if (item.HasCustomData<IncludePackageDependenciesData>() == false)
                    continue;

                XDocument document;
                try
                {
                    document = XDocument.Load(item.FileName, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
                }
                catch
                {
                    log.Error($"File '{item.FileName}' is not a valid XML document.");
                    success = false;
                    continue;
                }

                var selector = "//Package.Dependencies/Package";

                var nodes = document.XPathSelectElements(selector).ToArray();

                // No dependencies specified in the file -- skip
                if (nodes.Length == 0)
                    continue;

                log.Info($"Inheriting package dependencies from {item.FileName}:");

                string AttributeError(string errorMessage, XElement ele)
                {
                    string details;
                    if (ele is IXmlLineInfo l && l.HasLineInfo())
                        details = $"({ele} - line {l.LineNumber})";
                    else
                        details = $"({ele})";

                    var msg = $"{errorMessage} {details}";

                    return msg;
                }

                foreach (var node in nodes)
                {
                    if (!(node is XElement ele)) continue;
                    
                    var thisName = ele.Attribute("Name")?.Value;

                    if (string.IsNullOrWhiteSpace(thisName))
                    {
                        log.Error(AttributeError("Attribute 'Name' is not set.", ele));
                        success = false;
                        continue;
                    }

                    var thisVersion = ele.Attribute("Version")?.Value;

                    VersionSpecifier thisVersionSpec;
                    SemanticVersion thisSemver;
                    var thisAny = string.IsNullOrWhiteSpace(thisVersion) ||
                                  thisVersion.Equals("any", StringComparison.InvariantCultureIgnoreCase);

                    try
                    {
                        if (thisAny)
                        {
                            thisVersionSpec = VersionSpecifier.Any;
                            thisVersion = "any";
                            thisSemver = null;
                        }
                        else
                        {
                            if (thisVersion.StartsWith("^"))
                            {
                                thisVersionSpec = VersionSpecifier.Parse(thisVersion);
                                thisSemver = SemanticVersion.Parse(thisVersion.Substring(1));
                            }
                            else
                            {
                                thisVersionSpec = VersionSpecifier.Parse("^" + thisVersion);
                                thisSemver = SemanticVersion.Parse(thisVersion);
                            }
                        }
                    }
                    catch
                    {
                        log.Error("Attribute 'Version' is not a valid version specifier.", ele);
                        success = false;
                        continue;
                    }

                    var thisDep = new PackageDependency(thisName, thisVersionSpec, thisVersion);

                    // Avoid adding duplicate entries
                    var existing = pkgDef.Dependencies.FirstOrDefault(d => d.Name == thisName);

                    // Determine if we should replace the existing version
                    if (existing != null)
                    {
                        var otherAny =
                            existing.RawVersion.Equals("any", StringComparison.InvariantCultureIgnoreCase);

                        var otherSemver = otherAny ? null : SemanticVersion.Parse(existing.RawVersion);

                        // If the existing dependency is compatible with this one, we can safely ignore it
                        if (thisAny || (otherAny == false && thisVersionSpec.IsCompatible(otherSemver))) continue;

                        // If the new version is compatible with the previous one, we can safely replace the previous version.
                        if (existing.Version.IsCompatible(thisSemver) || otherAny)
                        {
                            pkgDef.Dependencies.Remove(existing);
                            pkgDef.Dependencies.Add(thisDep);
                        }
                        // Otherwise the two versions are in conflict. This error is not recoverable
                        else
                        {
                            log.Error($"Dependency conflict: {thisName} v. '{thisVersion}' and '{existing.RawVersion}' are mutually exclusive.");
                            success = false;
                            continue;
                        }
                    }
                    // Otherwise add a new dependency for this element
                    else
                    {
                        pkgDef.Dependencies.Add(thisDep);
                    }
                }

                item.RemoveCustomData<IncludePackageDependenciesData>();
            }

            return success;
        }
    }
}
