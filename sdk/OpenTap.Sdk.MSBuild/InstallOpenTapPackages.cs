using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        public Microsoft.Build.Framework.ITaskItem[] PackagesToInstall { get; set; }

        public string TapDir { get; set; }

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
                Log.LogMessagesFromStream(p.StandardOutput, MessageImportance.Normal);
                if (p.ExitCode != 0)
                {
                    Log.LogError($"Failed to install package {package}.");
                    return false;
                }
            }

            return true;
        }
    }
}
