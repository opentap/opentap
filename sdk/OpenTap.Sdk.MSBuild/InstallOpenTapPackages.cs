using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
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
        /// Array of packages to install, including version and repository
        /// </summary>
        public ITaskItem[] PackagesToInstall { get; set; }
        /// <summary>
        /// Full qualified path to the tap directory where packages will be installed (usually $(ProjectRoot)\bin\$(Configuration))
        /// </summary>
        public string TapDir { get; set; }
        /// <summary>
        /// Full qualified path to the .csproj file for which packages are being installed
        /// </summary>
        public string SourceFile { get; set; }
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
            if (PackagesToInstall == null || PackagesToInstall.Any() == false)
                return true;
            if (string.IsNullOrWhiteSpace(TapDir))
                return false;

            // Task instances are sometimes reused by the build engine -- ensure 'document' is reinstantiated
            _document = null;

            var success = true;

            var tapInstall = Path.Combine(TapDir, "tap");
            if (File.Exists(tapInstall) == false)
                tapInstall = Path.Combine(TapDir, "tap.exe");
            if (File.Exists(tapInstall) == false)
                throw new Exception($"No tap install found in directory {TapDir}");

            foreach (var item in PackagesToInstall)
            {
                var package = item.ItemSpec;
                var repository = item.GetMetadata("Repository");
                if (string.IsNullOrWhiteSpace(repository))
                    repository = "packages.opentap.io";
                var version = item.GetMetadata("Version");

                var arguments = $@"package install --dependencies ""{package}"" -r ""{repository}"" --non-interactive";
                if (string.IsNullOrWhiteSpace(version) == false)
                    arguments += $@" --version ""{version}""";

                var startInfo = new ProcessStartInfo()
                {
                    FileName = tapInstall,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = false,
                    WorkingDirectory = TapDir,
                    EnvironmentVariables = {{"OPENTAP_DEBUG_INSTALL", "true"}}
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
                            $"Failed to install package '{package}'. {installLog.Replace("\r", "").Replace('\n', ' ')}");
                    }
                }
            }

            return success;
        }
    }
}
