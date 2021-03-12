using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Execution;

namespace Keysight.OpenTap.Sdk.MSBuild
{
    internal static class ExtensionClass{
        internal static string ElemOrAttributeValue(this XElement ele, string name, string defaultValue)
        {
            return ele.Attribute(name)?.Value ?? ele.Element(name)?.Value ?? defaultValue;
        }

        internal static XElement[] GetPackageElements(this XElement ele, string packageName)
        {
            var tagNames = new[] {"OpenTapPackageReference", "AdditionalOpenTapPackage"};

            var itemGroups = ele.Elements().Where(e => e.Name.LocalName == "ItemGroup");
            var packageInstallElements = new List<XElement>();

            foreach (var itemGroup in itemGroups)
            {
                var packageElems = itemGroup.Elements().Where(e => tagNames.Contains(e.Name.LocalName));
                packageElems = packageElems.Where(e => e?.Attribute("Include")?.Value == packageName);
                packageInstallElements.AddRange(packageElems);
            }

            return packageInstallElements.ToArray();
        }
    }
    internal class BuildVariableExpander
    {

        private readonly ProjectInstance _projectInstance;
        private const string Pattern = @"\$\(.*?\)";

        internal BuildVariableExpander(string sourceFile)
        {
            _projectInstance = new ProjectInstance(sourceFile);
        }

        internal string ExpandBuildVariables(string input)
        {
            var matches = Regex.Matches(input, Pattern);
            
            foreach (Match match in matches)
            {
                // $(VarName) -> VarName
                var matchName = match.Value.Substring(2, match.Value.Length - 3);
                
                // Expand to csproj variable if available, otherwise expand to environment variable
                var varValue = _projectInstance.GetProperty(matchName)?.EvaluatedValue ??
                               Environment.GetEnvironmentVariable(matchName) ?? "";
                input = input.Replace(match.Value, varValue);
            }

            return input;
        }
    }
}
