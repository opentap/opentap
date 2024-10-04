using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace Keysight.OpenTap.Sdk.MSBuild
{
    /// <summary>
    /// MSBuild Task to version the build output using gitversion.
    /// </summary>
    [Serializable]
    public class SetGitVersion : Task, ICancelableTask
    {
        private const string TargetName = "OpenTapGitAssistedAssemblyVersion";
        /// <summary>
        /// The build directory containing 'tap.exe' and 'OpenTAP.dll'
        /// </summary>
        public string TapDir { get; set; }

        /// <summary>
        /// csproj file. This is needed because OpenTAP supports multiple gitversion files.
        /// Gitversion should be resolved from the directory containing the project file.
        /// </summary>
        public string SourceFile { get; set; }

        /// <summary>
        /// Optional input gitversion. This is useful in CI pipelines where shallow clones
        /// are used for performance reasons.
        /// </summary>
        public string InputGitVersion { get; set; }
        
        /// <summary>
        /// The output gitversion.
        /// </summary>
        [Microsoft.Build.Framework.Output]
        public string OutputShortVersion { get; set; }

        /// <summary>
        /// The output gitversion plus informational version.
        /// </summary>
        [Microsoft.Build.Framework.Output]
        public string OutputGitVersion { get; set; }

        private bool tryParseXElement(string text, out XElement elem)
        {
            try
            {
                elem = XElement.Parse(text, LoadOptions.None);
                return true;
            }
            catch
            {
                elem = null;
                return false;
            }
        }

        private bool runProcess(string filename, string arguments, string workingDirectory, out string stdout,
            out string stderr)
        {
            var si = new ProcessStartInfo(filename, arguments)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory,
            };
            var errStream = new StringBuilder();
            var outStream = new StringBuilder();
            var proc = new Process();
            proc.StartInfo = si;
            proc.OutputDataReceived += (_, data) =>
            {
                if (!string.IsNullOrWhiteSpace(data?.Data)) outStream.Append(data.Data);
            };
            proc.ErrorDataReceived += (_, data) =>
            {
                if (!string.IsNullOrWhiteSpace(data?.Data)) errStream.Append(data.Data);
            };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit(10000); 

            if (!proc.HasExited)
            {
                stdout = stderr = null;
                Log.LogError($"{TargetName}: tap sdk gitversion appears to hanging.");
                return false;
            }
            stdout = outStream.ToString();
            stderr = errStream.ToString();

            return true;
        }

        private bool isWindows()
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                    return true;
                default:
                    return false;
            }
        }

        // Check if a file with the given name exists in any ancestor directory
        private bool fileIsAncestor(string name, DirectoryInfo root)
        {
            var comparer = isWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            while (root != null)
            {
                if (root.EnumerateFileSystemInfos().Any(i => i.Name.Equals(name, comparer)))
                    return true;
                root = root.Parent;
            }

            return false;
        }

        // This does not need to be perfect.
        private static Regex gitversionRegex = new Regex(@"^\d+\.\d+\.\d+", RegexOptions.Compiled); 
        private bool tryParseGitVersion(string inputGitVersion, out string shortVersion, out string gitversion)
        {
            inputGitVersion = inputGitVersion.Trim();
            var m = gitversionRegex.Match(inputGitVersion);
            if (m.Success)
            {
                shortVersion = m.Value;
                gitversion = inputGitVersion;
                return true;
            }

            shortVersion = gitversion = null;
            return false;
        }

        private bool tryCalculateGitversion(out string shortVersion, out string gitversion)
        {
            shortVersion = null;
            gitversion = null;

            var workingDirectory = Path.GetDirectoryName(SourceFile);
            var dirInfo = new DirectoryInfo(workingDirectory);

            // Ensure this is a git repository
            if (!fileIsAncestor(".git", dirInfo))
            {
                Log.LogError(
                    $"{TargetName}: The project file '{SourceFile}' is not in a git directory. {TargetName} is only supported in git projects.");
                return false;
            }

            // And that it uses gitversioning
            if (!fileIsAncestor(".gitversion", dirInfo))
            {
                Log.LogError(
                    $"{TargetName}: This project does not have a .gitversion file. {TargetName} is only supported in gitversion projects.\n" +
                    $"See https://doc.opentap.io/Developer%20Guide/Plugin%20Packaging%20and%20Versioning/Readme.html#git-assisted-versioning");
                return false;
            }

            // Start a subprocess to get the gitversion. There are a couple of reasons we don't calculate it in-process:
            // 1. libgit uses a native dll which is annoying to load. OpenTAP already includes logic for this which
            // makes several assumptions that do not apply during dotnet build.
            // 2. GitVersionCalculator is internal, and I would prefer to not make it public.
            // For these reasons, it is much simpler to just start a process and parse the output.
            string tapName = isWindows() ? "tap.exe" : "tap";
            var tap = Path.Combine(TapDir, tapName);
            if (!runProcess(tap, "sdk gitversion", workingDirectory, out var stdout, out var stderr))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                // This could indicate a problem, but logging it as an error would fail the build.
                // Log it as a warning instead so the user is at least aware
                Log.LogWarning(stderr);
            }

            // There could potentially be multiple lines in the output due diagnostics or warnings from OpenTAP.
            // Find the line that looks like a gitversion
            var lines = stdout.Split('\n').Select(line => line.Trim()).ToArray();

            foreach (var l in lines)
            {
                if (tryParseGitVersion(l, out shortVersion, out gitversion))
                    return true;
            }

            Log.LogError($"{TargetName}: Unable to parse gitversion from output:\n{stdout}");

            return false;
        } 

        public override bool Execute()
        {
            string shortVersion;
            string longVersion;
            string input;

            // parse input gitversion. It can have a couple of different formats:
            // 1. A flag, such as '1' or 'true'
            // 2. A semantic version
            // 3. An xml tag enclosing a value, such as <GitVersion>1.2.3</GitVersion>

            // Case 1: calculate the version
            if (new[] { "true", "1" }.Contains(InputGitVersion.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                if (tryCalculateGitversion(out shortVersion, out longVersion))
                {
                    OutputShortVersion = shortVersion;
                    OutputGitVersion = longVersion;
                    return true;
                }

                return false;
            }

            // Case 2/3: parse the provided version from the input
            if (!tryParseXElement($"<{TargetName}>{InputGitVersion.Trim()}</{TargetName}>", out var elem))
            {
                // This should not be possible since the input is exactly the inner text of the .csproj property element.
                // If the input is not valid xml, then the compilation should have already failed.
                Log.LogError($"{TargetName}: Failed to parse input.");
                return false;
            }
            
            if (elem.Element("GitVersion") is XElement gv)
            {
                input = gv.Value.Trim();
            }
            else if (tryParseGitVersion(elem.Value.Trim(), out _, out _))
            {
                input = elem.Value.Trim();
            }
            else
            { 
                Log.LogError($"{TargetName}: Expected element named 'GitVersion'.");
                return false;
            }

            if (!tryParseGitVersion(input, out shortVersion, out longVersion))
            {
                Log.LogError($"{TargetName}: Provided gitversion is not a valid semantic version: '{input}'"); 
                return false;
            }
            
            OutputShortVersion = shortVersion;
            OutputGitVersion = longVersion;
            return true; 
        }

        public void Cancel()
        {
            // No cancel logic needed
        }
    }
}