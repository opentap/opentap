using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using OpenTap.Authentication;
using Task = Microsoft.Build.Utilities.Task;


namespace Keysight.OpenTap.Sdk.MSBuild
{
    /// <summary>
    /// MSBuild Task to help install packages. This task is invoked when using 'OpenTapPackageReference' and 'AdditionalOpenTapPackages' in .csproj files
    /// </summary>
    [Serializable]
    public class InstallOpenTapPackages : Task, ICancelableTask
    {
        internal IImageDeployer ImageDeployer { get; set; }
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
        /// The runtime directory in the OpenTAP nuget package
        /// </summary>
        public string OpenTapRuntimeDir { get; set; }

        /// <summary>
        /// The target platform defined in msbuild
        /// </summary>
        public string PlatformTarget { get; set; }

        private XElement _document;

        internal XElement Document
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SourceFile)) return null;
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

                var doc = Document;
                if (doc == null) return new[] { 0 };

                foreach (var elem in doc.GetPackageElements(packageName))
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
            if (PackagesToInstall == null || PackagesToInstall.Length == 0) 
                return true; 

            using (OpenTapContext.Create(TapDir, OpenTapRuntimeDir))
                return InstallPackages();
        }

        private CancellationTokenSource cts = new CancellationTokenSource();

        public void Cancel()
        {
            cts.Cancel();
        }

        /// <summary> Load Authentication tokens from Items into settings. </summary>
        /// <returns>True if successful.</returns>
        bool ProcessAuthTokens()
        {
            var repo2token = new Dictionary<string, string>(); 
            {
                var repoTokens = new List<(string repo, string token)>();

                foreach (var item in Repositories ?? Array.Empty<ITaskItem>())
                {
                    // Read token from <OpenTapRepository Repository="X" Token="Y"/>
                    var repo = item.GetMetadata("Repository")?.Trim();
                    var token = item.GetMetadata("Token").Trim();
                    if (string.IsNullOrWhiteSpace(token))
                        continue;

                    if (string.IsNullOrWhiteSpace(repo))
                    {
                        Log.LogError("When Token is used Repository must be specified.");
                        return false;
                    }
                    repoTokens.Add((repo, token));

                }
                
                foreach (var package in PackagesToInstall)
                {
                    // Read token from <OpenTapPackageReference Include="Z" Version="W" Repository="X" Token="Y"/>
                    // also applies top AdditionalOpenTapPackage.
                    var repo = package.GetMetadata("Repository")?.Trim();
                    var token = package.GetMetadata("Token").Trim();
                    if (string.IsNullOrWhiteSpace(token))
                        continue;

                    if (string.IsNullOrWhiteSpace(repo))
                    {
                        Log.LogError("When Token is used Repository must be specified.");
                        return false;
                    }
                    repoTokens.Add((repo, token));
                }
                foreach (var (repo, token) in repoTokens)
                {
                    if (repo2token.TryGetValue(repo, out var token2))
                    {
                        if (token == token2)
                            continue;
                        Log.LogError($"Repository ({repo}) already specified with a different token.");
                        return false;
                    }
                    repo2token.Add(repo, token);
                }
            }

            
            foreach (var tokenPair in repo2token)
            {
                try
                {
                    var uri = new Uri(tokenPair.Key, UriKind.RelativeOrAbsolute);
                    string host;
                    if (uri.IsAbsoluteUri)
                    {
                        host = uri.Host;
                    }
                    else
                    {
                        host = tokenPair.Key;
                    }


                    AuthenticationSettings.Current.Tokens.Insert(0, new TokenInfo(tokenPair.Value, "", host));
                }
                catch (Exception e)
                {
                    Log.LogError($"Could not parse URI for token: {e.Message}");
                    return false;
                }
            }
            return true;
        } 
        
        private void UpdateMarkerFiles()
        {
            // This is to avoid the following scenario:
            // 1. Install foo version 1.0.0, creating a marker file
            // 2. Install foo version 1.0.1, creating a marker file
            // 3. Downgrade foo to version 1.0.0. If marker file exists, this will be skipped
            foreach (var pkg in PackagesToInstall)
            {
                var name = pkg.ItemSpec; 
                var path = Path.Combine(TapDir, "Packages", name);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                var markerFiles = Directory.GetFiles(path, ".v*.marker");
                foreach (var f in markerFiles)
                {
                    try
                    {
                        File.Delete(f);
                    }
                    catch
                    {
                        // ignore. Not a big deal
                    }
                }
                var version = pkg.GetMetadata("Version") ?? "";
                var fs = File.Create(Path.Combine(path, $".v{version}.marker"));
                fs.Close();
            }
        }
        
        /// <summary>
        /// We *must* be in an OpenTapContext before calling this method because
        /// it depends on OpenTAP DLLs.
        /// </summary>
        /// <returns></returns>
        private bool InstallPackages()
        {
            if (!ProcessAuthTokens())
                return false;
            
            var repos = Repositories?.SelectMany(r =>
                    r.ItemSpec.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                .ToList() ?? new List<string>();

            repos.AddRange(PackagesToInstall.Select(p => p.GetMetadata("Repository"))
                .Where(m => string.IsNullOrWhiteSpace(m) == false));

            repos = repos.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            using (var imageInstaller = new OpenTapImageInstaller(TapDir, OpenTapRuntimeDir, cts.Token))
            {
                imageInstaller.LogMessage += OnInstallerLogMessage;
                imageInstaller.ImageDeployer = ImageDeployer;

                try
                {
                    var success = imageInstaller.InstallImage(PackagesToInstall, repos); 
                    if (success)
                        UpdateMarkerFiles(); 
                    return success;
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
