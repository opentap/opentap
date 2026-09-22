using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public class CalculateVersion : Task, ICancelableTask
    {
        private const string TargetName = "OpenTapSetAssemblyVersion";
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
        /// Optional input version. This is useful in CI pipelines where shallow clones
        /// are used for performance reasons.
        /// </summary>
        public string InputVersion { get; set; }
        
        /// <summary>
        /// The output shortversion (x.y.z).
        /// </summary>
        [Microsoft.Build.Framework.Output]
        public string OutputShortVersion { get; set; }

        /// <summary>
        /// The output gitversion plus informational version (x.y.z-beta.1+hash).
        /// </summary>
        [Microsoft.Build.Framework.Output]
        public string OutputLongVersion { get; set; }

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
        private static Regex versionRegex = new Regex(@"^\d+\.\d+\.\d+", RegexOptions.Compiled); 
        private bool tryParseVersion(string inputVersion, out string shortVersion, out string longVersion)
        {
            inputVersion = inputVersion.Trim();
            var m = versionRegex.Match(inputVersion);
            if (m.Success)
            {
                shortVersion = m.Value;
                longVersion = inputVersion;
                return true;
            }

            shortVersion = longVersion = null;
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
                /* Don't fail the build if we are not running inside a git directory.
                 * Otherwise it is not possible to build a project by e.g. zipping the source. */
                Log.LogWarning(
                    $"{TargetName}: The project file '{SourceFile}' is not in a git directory. {TargetName} is only supported in git projects.");
                shortVersion = "0.0.0";
                gitversion = "0.0.0";
                return true;
            }

            // And that it uses gitversioning
            if (!fileIsAncestor(".gitversion", dirInfo))
            {
                Log.LogError(
                    $"{TargetName}: This project does not have a .gitversion file. {TargetName} is only supported in gitversion projects.\n" +
                    $"See https://doc.opentap.io/Developer%20Guide/Plugin%20Packaging%20and%20Versioning/Readme.html#git-assisted-versioning");
                return false;
            }

            try
            {
                var taskDirectory = Path.GetDirectoryName(typeof(CalculateVersion).GetTypeInfo().Assembly.Location);
                var nativeLibrary = GitVersionNativeLibrary.GetPath(taskDirectory);
                try
                {
                    GitVersionNativeLibrary.Load(nativeLibrary);
                }
                catch (Exception ex)
                {
                    var loaderException = ex is TargetInvocationException invocationException && invocationException.InnerException != null
                        ? invocationException.InnerException
                        : ex;
                    Log.LogError($"{TargetName}: Git versioning is not supported on this build host " +
                                 $"({System.Runtime.InteropServices.RuntimeInformation.OSDescription}, " +
                                 $"{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}). " +
                                 $"Set OpenTapSetAssemblyVersion to an explicit semantic version instead of 'gitversion'. " +
                                 $"If this host should be supported, please open an issue at https://github.com/opentap/opentap/issues " +
                                 $"and include this complete error message. Details: {loaderException.Message}");
                    return false;
                }

                var calculatorLog = new global::OpenTap.GitVersioning.GitVersionCalculatorCore.GitVersionLog(
                    message => Log.LogMessage(Microsoft.Build.Framework.MessageImportance.Low, message),
                    message => Log.LogWarning(message),
                    message => Log.LogError(message));
                using (var calculator = new global::OpenTap.GitVersioning.GitVersionCalculatorCore(
                           workingDirectory, calculatorLog, Path.GetDirectoryName(nativeLibrary)))
                {
                    var version = calculator.GetVersion();
                    shortVersion = $"{version.Major}.{version.Minor}.{version.Patch}";
                    gitversion = version.ToString();
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"{TargetName}: Failed to calculate gitversion: {ex.Message}");
                return false;
            }
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
            if (new[] { "true", "1" , "gitversion", "auto", "git" }.Contains(InputVersion.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                if (tryCalculateGitversion(out shortVersion, out longVersion))
                {
                    OutputShortVersion = shortVersion;
                    OutputLongVersion = longVersion;
                    return true;
                }

                return false;
            }

            // Case 2/3: parse the provided version from the input
            if (!tryParseXElement($"<{TargetName}>{InputVersion.Trim()}</{TargetName}>", out var elem))
            {
                // This should not be possible since the input is exactly the inner text of the .csproj property element.
                // If the input is not valid xml, then the compilation should have already failed.
                Log.LogError($"{TargetName}: Failed to parse input.");
                return false;
            }
            
            if (elem.Element("Version") is XElement gv)
            {
                input = gv.Value.Trim();
            }
            else if (tryParseVersion(elem.Value.Trim(), out _, out _))
            {
                input = elem.Value.Trim();
            }
            else
            { 
                Log.LogError($"{TargetName}: Expected element named 'Version'.");
                return false;
            }

            if (!tryParseVersion(input, out shortVersion, out longVersion))
            {
                Log.LogError($"{TargetName}: Provided version is not a valid semantic version: '{input}'"); 
                return false;
            }
            
            OutputShortVersion = shortVersion;
            OutputLongVersion = longVersion;
            return true; 
        }

        public void Cancel()
        {
            // No cancel logic needed
        }
    }
}
