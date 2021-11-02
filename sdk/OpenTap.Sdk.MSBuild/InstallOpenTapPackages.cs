using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;


namespace Keysight.OpenTap.Sdk.MSBuild
{
    /// <summary>
    /// MSBuild Task to help install packages. This task is invoked when using 'OpenTapPackageReference' and 'AdditionalOpenTapPackages' in .csproj files
    /// </summary>
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
        public ITaskItem[] PackagesToInstall { get; set; }

        /// <summary>
        /// The build directory containing 'tap.exe' and 'OpenTAP.dll'
        /// </summary>
        public string TapDir { get; set; }
        
        /// <summary>
        /// The target platform defined in msbuild
        /// </summary>
        public string PlatformTarget { get; set; }

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
            if (!PackagesToInstall.Any()) return true;

            var tapInstall = Path.Combine(TapDir, "tap");
            if (File.Exists(tapInstall) == false)
                tapInstall = Path.Combine(TapDir, "tap.exe");
            if (File.Exists(tapInstall) == false)
                throw new Exception($"No tap install found in directory {TapDir}");

            Environment.SetEnvironmentVariable("OPENTAP_NO_UPDATE_CHECK", "true");
            Environment.SetEnvironmentVariable("OPENTAP_DEBUG_INSTALL", "true");

            using (var installer = new OpenTapImageInstaller(TapDir))
            {
                installer.OnError += err =>
                {
                    // Attempt to retrieve a relevant line number
                    var item = PackagesToInstall.FirstOrDefault(i => err.Contains(i.ItemSpec));
                    var lineNum = item != null ? GetLineNum(item) : Array.Empty<int>();
                    if (lineNum.Any())
                    {
                        var lineNumber = lineNum.First();
                        Log.LogError(null, "OpenTAP Install", null,
                            SourceFile, lineNumber, 0, 0, 0, err);
                    }
                    // Otherwise just log it
                    else
                        Log.LogError(err);
                };

                installer.OnInfo += info => Log.LogMessage(info);
                installer.OnDebug += debug => Log.LogMessage(MessageImportance.Low, debug);
                installer.OnWarning += warn => Log.LogWarning(warn);

                try
                {
                    return installer.InstallImage(PackagesToInstall);
                }
                catch (Exception ex)
                {
                    Log.LogError(ex.Message);
                    return false;
                }
            }
        }
    }
}
