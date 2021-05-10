using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;


namespace Keysight.OpenTap.Sdk.MSBuild
{
    /// <summary>
    /// MSBuild Task to help install packages. This task is invoked when using 'OpenTapPackageReference' and 'AdditionalOpenTapPackages' in .csproj files
    [Serializable]
    public class InstallOpenTapPackages : Task
    {
        /// <summary>
        /// Full qualified path to the .csproj file for which packages are being installed
        /// </summary>
        public string SourceFile { get; set; }
        
        /// <summary>
        /// Array of packages to install, including version and repository
        /// </summary>
        public Microsoft.Build.Framework.ITaskItem[] PackagesToInstall { get; set; }

        /// <summary>
        /// The build directory containing 'tap.exe' and 'OpenTAP.dll'
        /// </summary>
        public string TapDir { get; set; }
        
        /// <summary>
        /// The target platform defined in msbuild
        /// </summary>
        public string PlatformTarget { get; set; }

        private string BuildArgumentString(ITaskItem item)
        {
            var package = item.ItemSpec;
            var repository = item.GetMetadata("Repository");
            if (string.IsNullOrWhiteSpace(repository))
                repository = "packages.opentap.io";
            var version = item.GetMetadata("Version");

            var unpackOnly = item.GetMetadata("UnpackOnly") ?? "";

            var arguments = $@"package install --dependencies ""{package}"" -r ""{repository}"" --non-interactive";
            if (string.IsNullOrWhiteSpace(version) == false)
                arguments += $@" --version ""{version}""";

            if (unpackOnly.Equals("true", StringComparison.CurrentCultureIgnoreCase))
                arguments += " --unpack-only";

            return arguments;
        }
        
        private XElement _document;

        internal XElement Document
        {
            get
            {
                if (_document != null)
                    return _document;
                
                var expander = new BuildVariableExpander(SourceFile);
                // Expand all the build variables in the document to accurately identify which element corresponds to 'item'
                _document = XElement.Parse(expander.ExpandBuildVariables(File.ReadAllText(SourceFile)),
                    LoadOptions.SetLineInfo);
                return _document;
            }
        }
        
        private int[] GetLineNum(ITaskItem item)
        {
            var lines = new List<int>();

            try
            {
                var packageName = item.ItemSpec;
                var version = item.GetMetadata("Version");
                var repository = item.GetMetadata("Repository");

                foreach (var elem in Document.GetPackageElements(packageName))
                {
                    if (elem is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
                    {
                        // If there is no exact match, return every possible match
                        lines.Add(lineInfo.LineNumber);

                        var elemVersion = elem.ElemOrAttributeValue("Version", "");
                        var elemRepo = elem.ElemOrAttributeValue("Repository", "");

                        if (elemVersion != version || elemRepo != repository)
                            continue;

                        return new[] {lineInfo.LineNumber};
                    }
                }
            }
            catch
            {
                // Ignore exception
            }

            if (lines.Count > 0)
                return lines.ToArray();
            return new[] {0};
        }

        /// <summary>
        /// Install the packages. This will be invoked by MSBuild
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            Environment.SetEnvironmentVariable("OPENTAP_NO_UPDATE_CHECK", "true");
            Environment.SetEnvironmentVariable("OPENTAP_DEBUG_INSTALL", "true");

            if (PackagesToInstall == null || PackagesToInstall.Any() == false)
                return true;
            if (string.IsNullOrWhiteSpace(TapDir))
                return false;

            // Task instances are sometimes reused by the build engine -- ensure '_document' is reinstantiated
            _document = null;

            var success = true;

            var tapInstall = Path.Combine(TapDir, "tap");
            if (File.Exists(tapInstall) == false)
                tapInstall = Path.Combine(TapDir, "tap.exe");
            if (File.Exists(tapInstall) == false)
                throw new Exception($"No tap install found in directory {TapDir}");

            foreach (var item in PackagesToInstall)
            {
                // OpenTAP is always installed through nuget
                // but is sometimes added to include additional references
                // Skip it
                if (item.ItemSpec == "OpenTAP")
                {
                    var version = item.GetMetadata("Version")?.ToLower() ?? "";
                    if (string.IsNullOrWhiteSpace(version) == false && version.ToLower() != "any")
                    {
                        var packageXml = Path.Combine(TapDir, "Packages", "OpenTAP", "package.xml");
                        var content = File.ReadAllText(packageXml);
                        var nugetVersion = Regex.Match(content, "Version=\"(.*?)\"").Groups[1].Value.ToLower();

                        if (nugetVersion.StartsWith(version) == false)
                        {
                            var lineNum = GetLineNum(item).FirstOrDefault();

                            Log.LogWarning(null, "OpenTAP Install", null,
                                SourceFile, lineNum, 0, 0, 0,
                                $"Element specifies OpenTAP version '{version}' but version '{nugetVersion}' is installed through nuget. " +
                                "Using a different OpenTAP version from the nuget package can cause issues. " +
                                "Omitting the version number for this element is recommended. " +
                                $"This build will proceed with OpenTAP version '{nugetVersion}' from nuget.");
                        }
                    }

                    continue;
                }

                var arguments = BuildArgumentString(item);
                

                var startInfo = new ProcessStartInfo()
                {
                    FileName = tapInstall,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = false,
                    WorkingDirectory = TapDir,
                };

                var p = Process.Start(startInfo);
                Log.LogMessage($"Running '{tapInstall} {arguments}'.");
                p.WaitForExit();
                
                var installLog = p.StandardOutput.ReadToEnd();
                Log.LogMessage(installLog);
                
                if (p.ExitCode != 0)
                {
                    success = false;
                    
                    foreach (var lineNumber in GetLineNum(item))
                    {
                        Log.LogError(null, "OpenTAP Install", null,
                            SourceFile, lineNumber, 0, 0, 0,
                            $"Failed to install package '{item.ItemSpec}'. {installLog.Replace("\r", "").Replace('\n', ' ')}");
                    }
                }
            }

            return success;
        }
    }
}
