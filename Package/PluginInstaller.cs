//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.IO.Compression;
using System.Threading;
using System.ComponentModel;

namespace OpenTap.Package
{
    internal enum ActionResult
    {
        /// <summary>
        /// An error occurred.
        /// </summary>
        Error,
        /// <summary>
        /// No error occurred.
        /// </summary>
        Ok,
        /// <summary>
        /// No action steps were defined for the given package.
        /// </summary>
        NothingToDo
    }

    /// <summary>
    /// Executes a package action on a single package. Can optionally be used to execute a builtin action first.
    /// </summary>
    internal class ActionExecuter
    {
        static TraceSource log = OpenTap.Log.CreateSource("Plugin");

        private ConcurrentDictionary<string, TraceSource> LogSources { get; } =
            new ConcurrentDictionary<string, TraceSource>();

        /// <summary>
        /// The name of this rule.
        /// </summary>
        public string ActionName { get; set; }

        /// <summary>
        /// The inbuilt action that is executed first.
        /// </summary>
        public Func<PluginInstaller, ActionExecuter, PackageDef, bool, string, ActionResult> Execute { get; set; }

        public ActionResult DoExecute(PluginInstaller pluginInstaller, PackageDef package, bool force, string target)
        {
            try
            {
                if (Execute != null)
                {
                    return Execute(pluginInstaller, this, package, force, target);
                }
                else
                    return ExecutePackageActionSteps(package, force, target);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                return ActionResult.Error;
            }
        }

        internal ActionResult ExecutePackageActionSteps(PackageDef package, bool force, string workingDirectory)
        {
            ActionResult res = ActionResult.NothingToDo;

            // if the package is being installed as a system wide package, we'll  want to look in the system-wide
            // package folder for the executable. Additionally, the system-wide install directory will also be used as 
            // the working directory.
            bool isSystemWide = package.IsSystemWide();
            string systemWideDir = PackageDef.SystemWideInstallationDirectory;
            IEnumerable<string> possiblePaths(string file)
            {
                // If you want to run tap as a PackageActionExtension, you would specify "tap" as ExeFile for cross platform compatibility.
                // In that case, File.Exists wont be successful on Windows. That is why .exe is added if needed.
                if (isSystemWide)
                {
                    yield return Path.Combine(systemWideDir, file);
                    if(!file.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase))
                        yield return Path.Combine(systemWideDir,file + ".exe");
                }
                yield return Path.Combine(workingDirectory, file);
                if(!file.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase))
                    yield return Path.Combine(workingDirectory, file + ".exe");
            }

            foreach (var step in package.PackageActionExtensions)
            {
                if (step.ActionName.Equals(ActionName, StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                var stepName = $"'{step.ExeFile} {step.Arguments}'";
                log.Info($"Starting {step.ActionName} step {stepName}");
                var sw = Stopwatch.StartNew();

                string exefile = step.ExeFile;
                bool isTap = exefile.EndsWith("tap") || exefile.EndsWith("tap.exe");

                if (isTap)
                {
                    // Run verbose in order to inherit log source and log type
                    step.Arguments += " --verbose ";
                }


                // If path is relative, check if file is in fact in workingDirectory (isolated mode)
                if (!Path.IsPathRooted(step.ExeFile))
                {
                    exefile = possiblePaths(step.ExeFile).FirstOrDefault(File.Exists) ?? step.ExeFile;
                }

                // Upgrade to ok output
                res = ActionResult.Ok;

                var pi = new ProcessStartInfo(exefile, step.Arguments);

                pi.CreateNoWindow = step.CreateNoWindow;
                pi.WorkingDirectory = isSystemWide ? systemWideDir : workingDirectory;
                pi.Environment.Remove(ExecutorSubProcess.EnvVarNames.ParentProcessExeDir);
                // If OPENTAP_COLOR is set, the escape symbols for colors in the child process will break the parsing of the forwarded logs.
                // Ensure color is never set in the child process. Colors will still be set in the parent process.
                pi.Environment["OPENTAP_COLOR"] = "never";
                
                try
                {
                    Process p;
                    if (step.UseShellExecute)
                    {
                        pi.UseShellExecute = true;

                        p = Process.Start(pi);
                    }
                    else
                    {
                        pi.RedirectStandardOutput = true;
                        pi.RedirectStandardError = true;
                        pi.UseShellExecute = false;

                        p = Process.Start(pi);

                        p.ErrorDataReceived += (s, e) =>
                        {
                            if (step.Quiet) 
                                return;
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                if (isTap)
                                    RedirectTapLog(e.Data, true);
                                else
                                    log.Error(e.Data);
                            }
                        };
                        p.OutputDataReceived += (s, e) =>
                        {
                            if (step.Quiet) 
                                return;
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                if (isTap)
                                    RedirectTapLog(e.Data, false);
                                else
                                    log.Debug(e.Data);
                            }
                        };

                        p.BeginErrorReadLine();
                        p.BeginOutputReadLine();
                    }

                    p.WaitForExit();

                    if (step.ExpectedExitCodes != "*")
                    {
                        var expectedExitCodes = step.ExpectedExitCodes.Split(',').TrySelect(int.Parse,
                                (_, stringValue) => log.Error($"Failed to parse string '{stringValue}' as an integer."))
                            .ToHashSet();

                        if (expectedExitCodes.Count == 0)
                            expectedExitCodes.Add(0);

                        if (!expectedExitCodes.Contains(p.ExitCode))
                            throw new Exception($"Failed to run {step.ActionName} step {stepName}. Unexpected exitcode: {p.ExitCode}");
                    }

                    log.Info(sw, $"Successfully ran {step.ActionName} step  {stepName}. {(p.ExitCode != 0 ? $"Exitcode: {p.ExitCode}" : "")}");
                }
                catch (Win32Exception) when (step.Optional)
                {
                    log.Warning($"'{step.ExeFile}' not found, skipping action.");
                }
                catch (Exception e)
                {
                    log.Error(sw, e.Message);

                    if (!force)
                        throw new Exception($"Failed to run {step.ActionName} package action.");
                }
            }

            return res;
        }

        private void RedirectTapLog(string lines, bool IsStandardError)
        {

            foreach (var line in lines.Split('\n'))
            {
                var message = line;
                string split = " : ";

                var logParts = line.Split(new string[] { split }, StringSplitOptions.None);

                if (logParts.Length < 4)
                {
                    if (IsStandardError)
                        log.Error(message);
                    else
                        log.Info(message);
                    continue;
                }

                var sourceName = logParts[1].Trim();
                var logType = logParts[2].Trim();

                var idx = 0;

                for (int i = 0; i < 3; i++)
                {
                    idx = message.IndexOf(split, idx, StringComparison.Ordinal) + split.Length;
                }

                message = message.Substring(idx);

                var source = LogSources.GetOrAdd(sourceName, Log.CreateSource);

                switch (logType)
                {
                    case "Information":
                        source.Info(message);
                        break;
                    case "Error":
                        source.Error(message);
                        break;
                    case "Warning":
                        source.Warning(message);
                        break;
                    default:
                        source.Debug(message);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Install system for Tap Plugins, which are OpenTAP dll/waveforms in a renamed zip.
    /// </summary>
    internal class PluginInstaller
    {
        static List<ActionExecuter> builtinActions = new List<ActionExecuter>
        {
            new ActionExecuter{ ActionName = Installer.Uninstall, Execute = DoUninstall }
        };

        static TraceSource log = OpenTap.Log.CreateSource("package");

        /// <summary>
        /// Returns the names of the files in a plugin package.
        /// </summary>
        /// <param name="packagePath"></param>
        /// <returns></returns>
        internal static List<string> FilesInPackage(string packagePath)
        {
            List<string> files = new List<string>();
            string fileType = Path.GetExtension(packagePath);
            if (fileType == ".xml")
            {
                var pkg = PackageDef.FromXml(packagePath);
                return pkg.Files.Select(f => f.FileName).ToList();
            }

            try
            {
                using var fileStream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var zip = new ZipArchive(fileStream, ZipArchiveMode.Read);
                foreach (var part in zip.Entries)
                {
                    if (part.Name == "[Content_Types].xml" || part.Name == ".rels" || part.FullName.StartsWith("package/services/metadata/core-properties"))
                        continue; // skip strange extra files that are created by System.IO.Packaging.Package (TAP 7.x)

                    string path = Uri.UnescapeDataString(part.FullName);
                    files.Add(path);
                }
            }
            catch (InvalidDataException)
            {
                log.Error($"Could not unpack '{packagePath}'.");
                throw;
            }
            return files;

        }

        /// <summary>
        /// Tries to install a plugin from 'path', throws an exception on error.
        /// </summary>
        internal static PackageDef InstallPluginPackage(string target, string path, bool unpackOnly = false)
        {
            checkExtension(path);
            checkFileExists(path);

            var package = PackageDef.FromPackage(path);
            var destination = package.IsSystemWide() ? PackageDef.SystemWideInstallationDirectory : target;

            try
            {
                if (Path.GetExtension(path).ToLower().EndsWith("tappackages")) // This is a bundle
                {
                    var tempDir = Path.GetTempPath();
                    var bundleFiles = UnpackPackage(path, tempDir);

                    foreach (var file in bundleFiles)
                        UnpackPackage(file, destination);
                }
                else
                    UnpackPackage(path, destination);
            }
            catch (Exception e)
            {
                log.Error($"Install failed to execute for '{package.Name}'.");
                tryUninstall(path, package, target);
                throw new Exception($"Failed to install package '{path}'.", e);
            }

            if (unpackOnly)
            {
                log.Info("Skipping install actions as unpack-only was specified.");
                return package;
            }

            var pi = new PluginInstaller();
            if (pi.ExecuteAction(package, Installer.Install, false, target) == ActionResult.Error)
            {
                log.Error($"Install package action failed to execute for '{package.Name}'.");
                tryUninstall(path, package, target);
                throw new Exception($"Failed to install package '{path}'.");
            }

            CustomPackageActionHelper.RunCustomActions(package, PackageActionStage.Install,
                new CustomPackageActionArgs(null, false));

            return package;
        }

        static void tryUninstall(string path, PackageDef package, string target)
        {
            log.Info("Uninstalling package '{0}'.", package.Name);
            Uninstall(package, target);
            log.Flush();
            log.Info("Uninstalled package '{0}'.", path);
        }

        static void checkExtension(string path)
        {
            string pathGetExtension = Path.GetExtension(path);
            if (String.Compare(pathGetExtension, ".tappackage", true) != 0 && String.Compare(pathGetExtension, ".tapplugin", true) != 0 && String.Compare(pathGetExtension, ".tappackages", true) != 0)
            {
                throw new Exception(String.Format("Unknown file type '{0}'.", pathGetExtension));
            }
        }

        static void checkFileExists(string path)
        {
            if (!File.Exists(path))
            {
                throw new Exception(String.Format("TapPackage does not exist '{0}'.", path));
            }
        }

        /// <summary>
        /// Unpacks a *.TapPackages file to the specified directory.
        /// </summary>
        internal static List<string> UnpackPackage(string packagePath, string destinationDir)
        {
            List<string> installedParts = new List<string>();
            string packageName = null;
            try
            {
                packageName = PackageDef.FromPackage(packagePath).Name;
            }
            catch
            {
                // This is fine, it could be a bundle. The package name is only required if the package is OpenTAP
            }

            try
            {
                using (var packageStream = File.OpenRead(packagePath))
                using (var zip = new ZipArchive(packageStream, ZipArchiveMode.Read))
                {
                    foreach (var part in zip.Entries)
                    {
                        if (part.Name == "[Content_Types].xml" || part.Name == ".rels" || part.FullName.StartsWith("package/services/metadata/core-properties"))
                            continue; // skip strange extra files that are created by System.IO.Packaging.Package (TAP 7.x)
                        if (string.IsNullOrWhiteSpace(part.Name))
                            continue;

                        string path = Uri.UnescapeDataString(part.FullName).Replace('\\', '/');
                        path = Path.Combine(destinationDir, path).Replace('\\', '/');

                        if (OperatingSystem.Current == OperatingSystem.Windows && packageName == "OpenTAP" && Path.GetFileNameWithoutExtension(part.FullName) == "tap")
                        {
                            // tap.dll and tap.exe cannot be overwritten because they are in use by this process -- extract them to a temp location so they can be overwritten later
                            if (File.Exists(path))
                                path += ".new";
                        }

                        var sw = Stopwatch.StartNew();

                        int Retries = 0, MaxRetries = 10;
                        while (true)
                        {
                            try
                            {
                                FileSystemHelper.EnsureDirectoryOf(path);
                                if (OperatingSystem.Current == OperatingSystem.Windows)
                                {
                                    // on windows, hidden files cannot be overwritten.
                                    // an exception will be thrown in File.Create further down.
                                    if (Path.GetFileName(path).StartsWith(".") && File.Exists(path))
                                    {
                                        var attrs = File.GetAttributes(path);
                                        var attrs2 = attrs & ~FileAttributes.Hidden;
                                        if(attrs2 != attrs)
                                            File.SetAttributes(path, attrs2);
                                    }
                                }


                                var deflate_stream = part.Open();
                                using (var fileStream = File.Create(path))
                                {
                                    var task = deflate_stream.CopyToAsync(fileStream, 4096, TapThread.Current.AbortToken);
                                    ConsoleUtils.PrintProgressTillEnd(task, "Decompressing", () => fileStream.Position, () => part.Length);
                                }
                                log.Debug(sw, "Decompressed {0}", path);
                                installedParts.Add(path);
                                break;
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (IOException ex) when (ex.Message.Contains("There is not enough space on the disk"))
                            {
                                log.Error(ex.Message);
                                var req = new AbortOrRetryRequest("Not Enough Disk Space", $"File '{part.FullName}' requires {Utils.BytesToReadable(part.Length)} of free space. " +
                                                                  $"Please free some space to continue.") {Response = AbortOrRetryResponse.Abort};
                                UserInput.Request(req, true);
                                if (req.Response == AbortOrRetryResponse.Abort)
                                    throw new OperationCanceledException("Installation aborted due to missing disk space.");
                            }
                            catch (Exception ex)
                            {
                                if (Path.GetFileNameWithoutExtension(path) == "tap")
                                    break; // this is ok tap.exe (or just tap on linux) is not designed to be overwritten

                                if (Retries == MaxRetries)
                                    throw;
                                Retries++;
                                log.Warning("Unable to unpack file {0}. Retry {1} of {2}.", path, Retries, MaxRetries);
                                log.Debug(ex);
                                Thread.Sleep(200);
                            }
                        }
                    }
                }
            }
            catch (InvalidDataException)
            {
                log.Error($"Could not unpackage '{packagePath}'.");
                throw;
            }

            SetHiddenAttributes(installedParts);
            return installedParts;
        }

        private static void SetHiddenAttributes(List<string> parts)
        {
            if (OperatingSystem.Current == OperatingSystem.Windows)
            {
                foreach (var path in parts)
                {
                    // Set file hidden attribute
                    if (Path.GetFileName(path).StartsWith("."))
                        File.SetAttributes(path, FileAttributes.Hidden);

                    // Set directory hidden attribute
                    var hiddenIndex = path.IndexOf("/.");
                    while (hiddenIndex > 0)
                    {
                        var hiddenDirLength = path.Substring(++hiddenIndex).IndexOf('/');
                        if (hiddenDirLength > 0)
                        {
                            var tempPath = path.Substring(0, hiddenIndex + hiddenDirLength + 1);
                            File.SetAttributes(tempPath, FileAttributes.Hidden);
                        }
                        hiddenIndex = path.IndexOf("/.", ++hiddenIndex);
                    }
                }
            }
        }

        /// <summary>
        /// Unpackages a plugin file.
        /// </summary>
        /// <param name="packagePath"></param>
        /// <param name="relativeFilePath"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        internal static bool UnpackageFile(string packagePath, string relativeFilePath, Stream destination)
        {
            try
            {
                using var fileStream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var zip = new ZipArchive(fileStream, ZipArchiveMode.Read);
                foreach (var part in zip.Entries)
                {
                    if (part.Name == "[Content_Types].xml" || part.Name == ".rels" || part.FullName.StartsWith("package/services/metadata/core-properties"))
                        continue; // skip strange extra files that are created by System.IO.Packaging.Package (TAP 7.x)

                    string path = Uri.UnescapeDataString(part.FullName);
                    if (path == relativeFilePath)
                    {
                        part.Open().CopyTo(destination);
                        return true;
                    }
                }
            }
            catch (InvalidDataException)
            {
                log.Error($"Could not unpackage '{packagePath}'.");
                throw;
            }
            return false;
        }

        /// <summary>
        /// Uninstalls a package.
        /// </summary>
        internal static void Uninstall(PackageDef package, string target)
        {
            var pi = new PluginInstaller();

            pi.ExecuteAction(package, Installer.PrepareUninstall, true, target);
            pi.ExecuteAction(package, Installer.Uninstall, true, target);
        }

        internal ActionResult ExecuteAction(PackageDef package, string actionName, bool force, string target)
        {
            ActionExecuter action = builtinActions.FirstOrDefault(r => r.ActionName == actionName);

            if (action == null)
                action = new ActionExecuter { ActionName = actionName };

            return action.DoExecute(this, package, force, target);
        }

        private static ActionResult DoUninstall(PluginInstaller pluginInstaller, ActionExecuter action, PackageDef package, bool force, string target)
        {

            var result = ActionResult.Ok;
            var destination = package.IsSystemWide() ? PackageDef.SystemWideInstallationDirectory : target;

            var filesToRemain = new Installation(destination).GetPackages()
                .Where(p => p.Name != package.Name)
                .SelectMany(p => p.Files)
                .Select(f => f.RelativeDestinationPath)
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

            try
            {
                CustomPackageActionHelper.RunCustomActions(package, PackageActionStage.Uninstall, new CustomPackageActionArgs(null, force));
            }
            catch (Exception ex)
            {
                log.Error(ex);
                result = ActionResult.Error;
            }

            try
            {
                if (action.ExecutePackageActionSteps(package, force, target) == ActionResult.Error)
                    throw new Exception();
            }
            catch
            {
                log.Error($"Uninstall package action failed to execute for package '{package.Name}'.");
                result = ActionResult.Error;
            }

            int totalDeleteRetries = 0;
            bool ignore(string filename) => filename.ToLower() == "tap" || filename.ToLower() == "tap.exe" || filename.ToLower() == "tap.dll";
            foreach (var file in package.Files)
            {
                if (ignore(file.RelativeDestinationPath)) // ignore tap, tap.dll, and tap.exe as they are not meant to be overwritten.
                    continue;

                string fullPath;
                if (package.IsSystemWide())
                {
                    fullPath = Path.Combine(PackageDef.SystemWideInstallationDirectory, file.RelativeDestinationPath);
                }
                else
                {
                    fullPath = Path.Combine(destination, file.RelativeDestinationPath);
                }

                if (filesToRemain.Contains(file.RelativeDestinationPath))
                {
                    log.Debug("Skipping deletion of file '{0}' since it is required by another plugin package.", file.RelativeDestinationPath);
                    continue;
                }

                try
                {
                    log.Debug("Deleting file '{0}'.", file.RelativeDestinationPath);

                    const int maxRetries = 10;
                    FileSystemHelper.SafeDelete(fullPath, maxRetries, (i, ex) =>
                    {
                        if (ex is UnauthorizedAccessException || ex is IOException)
                        {
                            // the number of retries goes across files, to avoid an install taking several minutes.
                            if (totalDeleteRetries >= maxRetries) throw ex;
                            totalDeleteRetries++;
                            // File.Delete might throw either exception depending on if it is a
                            // program _or_ a file in use.
                            
                            log.Warning("Unable to delete file '{0}' file might be in use. Retrying {1} of {2} in 1 second.", file.RelativeDestinationPath, totalDeleteRetries, maxRetries, totalDeleteRetries);
                            log.Debug("Error: {0}", ex.Message);
                            TapThread.Sleep(1000);
                        }
                        else throw ex;
                    });
                    
                }
                catch (Exception e)
                {
                    if (e is FileNotFoundException || e is DirectoryNotFoundException)
                    {
                        log.Debug($"File not found: {file.RelativeDestinationPath}");
                    }
                    else
                    {
                        log.Debug(e);
                        result = ActionResult.Error;
                    }
                }

                DeleteEmptyDirectory(new FileInfo(fullPath).Directory);
            }

            var packageFile = PackageDef.GetDefaultPackageMetadataPath(package, target);

            if (!File.Exists(packageFile))
            {
                // TAP 8.x support:
                packageFile = $"Package Definitions/{package.Name}.package.xml";
            }
            if (File.Exists(packageFile))
            {
                log.Debug("Deleting file '{0}'.", packageFile);
                File.Delete(packageFile);
                DeleteEmptyDirectory(new FileInfo(packageFile).Directory);
            }

            if (package.PackageSource is XmlPackageDefSource f2 && File.Exists(f2.PackageDefFilePath))
                // in case the package def XML was not in the default package definition directory
                // it is better to delete it anyway, because otherwise it will seem like it is still installed.
                File.Delete(f2.PackageDefFilePath);

            return result;
        }

        private static void DeleteEmptyDirectory(DirectoryInfo dir)
        {
            if (dir == null) return;
            if (!dir.Exists) return;
            if (dir.EnumerateFiles().Any() || dir.EnumerateDirectories().Any()) return;

            try
            {
                dir.Delete(false);
                // If it succeeded then we should check the parent directory.
                DeleteEmptyDirectory(dir.Parent);
            }
            catch
            {
                // Do nothing, it's not a big deal anyway
            }
        }
    }
}
