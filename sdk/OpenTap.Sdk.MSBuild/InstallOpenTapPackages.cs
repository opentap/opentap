using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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
    public class InstallOpenTapPackages : Task, ICancelableTask
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
        /// Array of package repositories to resolve packages from
        /// </summary>
        public ITaskItem[] Repositories { get; set; }

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

            // This alters the value returned by 'ExecutorClient.ExeDir' which would otherwise return the location of
            // OpenTap.dll which in an MSBuild context would be the nuget directory which leads to unexpected behavior
            // because the expected location is the build directory in all common use cases.
            Environment.SetEnvironmentVariable("OPENTAP_INIT_DIRECTORY", TapDir, EnvironmentVariableTarget.Process);

            var tapInstall = Path.Combine(TapDir, "tap");
            if (File.Exists(tapInstall) == false)
                tapInstall = Path.Combine(TapDir, "tap.exe");
            if (File.Exists(tapInstall) == false)
                throw new Exception($"No tap install found in directory {TapDir}");

            Environment.SetEnvironmentVariable("OPENTAP_NO_UPDATE_CHECK", "true");
            Environment.SetEnvironmentVariable("OPENTAP_DEBUG_INSTALL", "true");

            var projectDir = Path.GetFullPath(Path.GetDirectoryName(SourceFile));
            var buildDir = Path.Combine(projectDir, TapDir);
            var openTapDll = Path.Combine(buildDir, "OpenTap.dll");
            var openTapPackageDll = Path.Combine(buildDir, "OpenTap.Package.dll");

            // This is sort of a hack because the standard resolver will try to resolve OpenTap 9.4.0.0,
            // but we need to load whatever is in the NuGet directory
            Assembly resolve(object sender, ResolveEventArgs args)
            {
                if (args.Name.StartsWith("OpenTap.Package"))
                    return Assembly.LoadFile(openTapPackageDll);
                if (args.Name.StartsWith("OpenTap"))
                    return Assembly.LoadFile(openTapDll);
                return null;
            }

            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += resolve;
                return InstallPackages();
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= resolve;
            }
        }

        private CancellationTokenSource cts = new CancellationTokenSource();

        public void Cancel()
        {
            cts.Cancel();
        }

        private bool InstallPackages()
        {
            using (var installer = new OpenTapImageInstaller(TapDir, cts.Token))
            {
                installer.LogMessage += OnInstallerLogMessage;

                var repos = Repositories?.SelectMany(r =>
                        r.ItemSpec.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                    .ToList() ?? new List<string>();

                repos.AddRange(PackagesToInstall.Select(p => p.GetMetadata("Repository"))
                    .Where(m => string.IsNullOrWhiteSpace(m) == false));

                if (!repos.Any(r => r.ToLower().Contains("packages.opentap.io")))
                    repos.Add("packages.opentap.io");

                try
                {
                    return installer.InstallImage(PackagesToInstall, repos.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
                }
                catch (Exception ex)
                {
                    Log.LogError(ex.Message);
                    return false;
                }
            }
        }

        private void OnInstallerLogMessage(string message, int logEventType, ITaskItem item)
        {
            // This mirrors the LogEventType enum from OpenTAP, but this class must not depend on OpenTAP being already
            // resolved. Special care is given to resolving the correct OpenTAP dll in the Execute method.
            var logLevelMap = new Dictionary<int, string>()
            {
                [10] = "Error",
                [20] = "Warning",
                [30] = "Information",
                [40] = "Debug",
            };

            var logLevel = logLevelMap[logEventType];

            var numbers = item == null ? Array.Empty<int>() : GetLineNum(item);
            var lineNumber = numbers.Any() ? numbers.First() : 0;

            // Log the messages with line number and source file information
            // A line number of '0' causes the line number to be emitted by the MSBuild logger
            var source = "OpenTAP Install";
            switch (logLevel)
            {
                case "Error":
                    Log.LogError(null, source, null, SourceFile, lineNumber, 0, 0, 0, message);
                    break;
                case "Warning":
                    Log.LogWarning(null, source, null, SourceFile, lineNumber, 0, 0, 0, message);
                    break;
                case "Information":
                    Log.LogMessage(null, source, null, SourceFile, lineNumber, 0, 0, 0, MessageImportance.Normal, message);
                    break;
                case "Debug":
                    Log.LogMessage(null, source, null, SourceFile, lineNumber, 0, 0, 0, MessageImportance.Low, message);
                    break;
            }
        }
    }
}
