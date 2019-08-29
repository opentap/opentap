using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Keysight.OpenTap.Sdk.MSBuild
{
    public static class BuildType
    {
        public const string None = "None";
        public const string Package = "Package";
    }

    public static class InstallType
    {
        public const string None = "None";
        public const string PackageDef = "PackageDef";
    }

    /// <summary>
    /// MSBuild Task to help package plugin. This task is used by the OpenTAP SDK project template
    /// </summary>
    [LoadInSeparateAppDomain]
    [Serializable]
    public class PackageTask : AppDomainIsolatedTask
    {
        [Required]
        public string ConfFile { get; set; }

        [Required]
        public string Dir { get; set; }

        public string Build { get; set; }

        public string Install { get; set; }

        public PackageTask()
        {
            Build = BuildType.Package;
            Install = InstallType.PackageDef;
        }

        void confFileRun(string packagePath)
        {
            ProcessStartInfo info = new ProcessStartInfo(packagePath);
            
            info.Arguments = String.Format("package create \"{0}\" --project-directory \"{1}\" -v", ConfFile, Directory.GetCurrentDirectory());

            switch (Build)
            {
                case BuildType.Package:
                    info.Arguments += " -o \"\""; // Means "make tappackage with the correct name"
                    break;
            }


            switch (Install)
            {
                case InstallType.PackageDef:
                    if (Build != BuildType.Package)
                        info.Arguments += " -o \"\""; // Means "make tappackage with the correct name"

                    info.Arguments += " --install"; // Means "copy files not already present and output package xml to make it appear installed"
                    break;
            }

            info.WorkingDirectory = Dir;
            Log.LogMessage("{0} {1}", packagePath, info.Arguments);
            
            info.WindowStyle = ProcessWindowStyle.Normal;
            info.RedirectStandardError = true;
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;
            Process p = Process.Start(info);

            // find the output filename from the stdout of the Tap.Package.exe process
            // (we do this in a separate thread in parallel with reading stderr to avoid a potential deadlock)
            string filename = null;
            var task = System.Threading.Tasks.Task.Run(() =>
            {
                // Handle all non-error log messages
                while (!p.StandardOutput.EndOfStream)
                {
                    string line = p.StandardOutput.ReadLine();
                    // Catch the name of the generated file.
                    Regex sucessRegex = new Regex(@"TAP plugin package '(?<filename>[^']+)' containing '(?<name>[^']+)' successfully created.");
                    Match m = sucessRegex.Match(line);
                    if (m.Success)
                    {
                        filename = m.Groups["filename"].Value;
                    }

                    // extract messages of type Warning, Information and Debug. These are messages coming from OpenTAP.
                    Regex msgRegex = new Regex(@":[ ]*(?<source>.*)[ ]*:[ ]*(?<type>Warning|Information|Debug)[ ]*:[ ]*(?<msg>.+)");
                    Match msgMatch = msgRegex.Match(line);
                    if (msgMatch.Success)
                    {
                        string source = msgMatch.Groups["source"].Value;
                        string type = msgMatch.Groups["type"].Value;
                        string message = msgMatch.Groups["msg"].Value;

                        switch(type)
                        {
                            case "Warning":
                                BuildEngine.LogWarningEvent(new BuildWarningEventArgs(source, "", ConfFile, 1, 1, 0, 0, message, "", "tap.exe"));
                                break;
                            case "Information":
                                BuildEngine.LogMessageEvent(new BuildMessageEventArgs(source, "", ConfFile, 1, 1, 0, 0, message, "", "tap.exe", MessageImportance.High));
                                break;
                            case "Debug":
                                Log.LogMessage(source, "1", "", ConfFile, 1, 1, 0, 0, MessageImportance.Normal, message);
                                break;
                        }
                    }
                }
            });

            // Handle all error messages
            while (!p.StandardError.EndOfStream)
            {
                string line = p.StandardError.ReadLine();
                // Match Dotfuscator errors.
                Regex dtofErrorRegex = new Regex(@"(?<path>[^\(]+)\((?<line>[0-9]+),(?<column>[0-9]+)\): error: (?<msg>.+)");
                Match dotfErrorMatch = dtofErrorRegex.Match(line);
                if (dotfErrorMatch.Success)
                {
                    int lineNum = int.Parse(dotfErrorMatch.Groups["line"].Value);
                    int columnNum = int.Parse(dotfErrorMatch.Groups["column"].Value);
                    string message = dotfErrorMatch.Groups["msg"].Value;
                    BuildErrorEventArgs errorEvent = new BuildErrorEventArgs("Xml", "", ConfFile, lineNum, columnNum, 0, 0, message, "", "tap.exe");
                    BuildEngine.LogErrorEvent(errorEvent);
                }
                else
                {
                    // extract messages of type Error.  These are messages coming from OpenTAP.
                    Regex tapErrorRegex = new Regex(@":[ ]*(?<source>.*)[ ]*:[ ]*Error[ ]*:[ ]*(?<msg>.+)");
                    Match tapErrorMatch = tapErrorRegex.Match(line);
                    if (tapErrorMatch.Success)
                    {
                        string source = tapErrorMatch.Groups["source"].Value;
                        string message = tapErrorMatch.Groups["msg"].Value;

                       BuildEngine.LogErrorEvent(new BuildErrorEventArgs(source, "", ConfFile, 1, 1, 0, 0, message, "", "tap.exe"));
                    }
                    else
                        Log.LogError(line);
                }
            }

            task.Wait();
            p.WaitForExit();

            // Make sure we log an error. If Tap.Package did not succeed
            if (p.ExitCode == 0)
            {
                if(!string.IsNullOrEmpty(filename))
                    Log.LogMessage(MessageImportance.High, "Created package '{0}'", Path.Combine(Dir, filename));
                else
                    Log.LogMessage(MessageImportance.High, "Created package in '{0}'", Dir);
            }
            else
                Log.LogError("Error {1} creating package from {0}", ConfFile, p.ExitCode);
        }

        public override bool Execute()
        {
            string packagePath = GetTapExePath();

            if (!File.Exists(packagePath))
            {
                Log.LogError("Cannot find OpenTAP installation. Is the OpenTAP SDK installed?");
                return false;
            }

            if (ConfFile == null)
            {
                Log.LogError("Missing package cofiguration file (ConfFile).");
                return false;
            }

            var be3 = BuildEngine as IBuildEngine3;

            if (be3 != null) be3.Yield();
            try
            {
                confFileRun(packagePath);
            }
            finally
            {
                if (be3 != null) be3.Reacquire();
            }
            return !Log.HasLoggedErrors;
        }

        private string GetTapExePath()
        {
            try
            {
                string installRootDir = Path.GetDirectoryName(this.GetType().Assembly.Location);
                for(int i = 0; i<4;i++)
                {
                    if (File.Exists(Path.Combine(installRootDir,"tap.exe")))
                    {
                        return Path.Combine(installRootDir, "tap.exe");
                    }
                    installRootDir = Path.GetDirectoryName(installRootDir);
                }
            }
            catch
            {
            }
            return null;
        }
        private string formatOutput(string output, bool appendVersionToOutput, string assemblyPath)
        {
            if (appendVersionToOutput == false)
                return output;
            string extension = Path.GetExtension(output);
            string name = Path.GetFileNameWithoutExtension(output);
            string version = FileVersionInfo.GetVersionInfo(assemblyPath).ProductVersion;
            return String.Format("{0}.{1}{2}", name, version, extension);
        }
    }
}