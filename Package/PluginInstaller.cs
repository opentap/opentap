//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.IO.Compression;
using System.Threading;
using System.Runtime.InteropServices;

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
        static TraceSource log =  OpenTap.Log.CreateSource("Plugin");

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

            foreach (var step in package.PackageActionExtensions)
            {
                if (step.ActionName != ActionName)
                    continue;

                string exefile = step.ExeFile;
                // If path is relative, check if file is in fact in ExecutorClient.ExeDir (isolated mode)
                if (!Path.IsPathRooted(step.ExeFile) && File.Exists(Path.Combine(ExecutorClient.ExeDir, step.ExeFile)))
                    exefile = Path.Combine(ExecutorClient.ExeDir, step.ExeFile);

                // Upgrade to ok output
                res = ActionResult.Ok;

                log.Debug($"Running '{exefile}' in '{workingDirectory}' with arguments: '{step.Arguments}'.");

                var pi = new ProcessStartInfo(exefile, step.Arguments);

                pi.CreateNoWindow = step.CreateNoWindow;
                pi.WorkingDirectory = workingDirectory;
                pi.Environment.Remove(ExecutorSubProcess.EnvVarNames.ParentProcessExeDir);

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
                            if (!string.IsNullOrEmpty(e.Data)) log.Error(e.Data);
                        };
                        p.OutputDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data)) log.Debug(e.Data);
                        };

                        p.BeginErrorReadLine();
                        p.BeginOutputReadLine();
                    }

                    p.WaitForExit();

                    if (p.ExitCode != 0)
                        throw new Exception($"Failed to run command. Exitcode: {p.ExitCode}");
                }
                catch (Exception e)
                {
                    log.Error(e.Message);
                    log.Debug(e);
                    
                    if (!force)
                        throw new Exception("Failed to run package action.");
                }
            }
            return res;
        }
    }

    /// <summary>
    /// Install system for Tap Plugins, which are OpenTAP dll/waveforms in a renamed zip.
    /// </summary>
    internal class PluginInstaller
    {
        static List<ActionExecuter> builtinActions = new List<ActionExecuter>
        {
            new ActionExecuter{ ActionName = "uninstall", Execute = DoUninstall }
        };

        static TraceSource log =  OpenTap.Log.CreateSource("package");
        
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
                using (var fileStream = File.OpenRead(packagePath))
                using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Read))
                {
                    foreach (var part in zip.Entries)
                    {
                        if (part.Name == "[Content_Types].xml" || part.Name == ".rels" || part.FullName.StartsWith("package/services/metadata/core-properties"))
                            continue; // skip strange extra files that are created by System.IO.Packaging.Package (TAP 7.x)

                        string path = Uri.UnescapeDataString(part.FullName);
                        files.Add(path);
                    }
                }
            }
            catch (InvalidDataException)
            {
                log.Error($"Could not unpackage '{packagePath}'.");
                throw;
            }
            return files;

        }
        
        /// <summary>
        /// Tries to install a plugin from 'path', throws an exception on error.
        /// </summary>
        internal static PackageDef InstallPluginPackage(string target, string path)
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

            var pi = new PluginInstaller();
            if (pi.ExecuteAction(package, "install", false, target) == ActionResult.Error)
            {
                log.Error($"Install package action failed to execute for '{package.Name}'.");
                tryUninstall(path, package, target);
                throw new Exception($"Failed to install package '{path}'.");
            }


            CustomPackageActionHelper.RunCustomActions(package, PackageActionStage.Install, new CustomPackageActionArgs(null, false));

            return package;
        }

        static void tryUninstall(string path, PackageDef package,string target)
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
                        var sw = Stopwatch.StartNew();
                        
                        int Retries = 0, MaxRetries = 10;
                        while(true)
                        {
                            try
                            {
                                FileSystemHelper.EnsureDirectory(path);
                                var deflate_stream = part.Open();
                                using (var fileStream = File.Create(path))
                                {
                                    var task = deflate_stream.CopyToAsync(fileStream, 4096, TapThread.Current.AbortToken);
                                    ConsoleUtils.PrintProgressTillEnd(task, "Decompressing", ()=> fileStream.Position, ()=> part.Length);
                                }
                                log.Debug(sw, "Decompressed {0}", path);
                                installedParts.Add(path);
                                break;
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch
                            {                            
                                if(Path.GetFileNameWithoutExtension(path) == "tap")
                                    break; // this is ok tap.exe (or just tap on linux) is not designed to be overwritten

                                if(Retries==MaxRetries)
                                    throw;
                                Retries++;
                                log.Warning("Unable to unpack file {0}. Retry {1} of {2}.", path, Retries, MaxRetries);
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
            return installedParts;
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
                using (var fileStream = File.OpenRead(packagePath))
                using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Read))
                {
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

            pi.ExecuteAction(package, "uninstall", true, target);
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
          
            var filesToRemain = new Installation(destination).GetPackages().Where(p => p.Name != package.Name).SelectMany(p => p.Files).Select(f => f.RelativeDestinationPath).Distinct(StringComparer.InvariantCultureIgnoreCase).ToHashSet(StringComparer.InvariantCultureIgnoreCase);

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
            
            foreach (var file in package.Files)
            {
                if (file.RelativeDestinationPath == "tap" || file.RelativeDestinationPath.ToLower() == "tap.exe") // ignore tap.exe as it is not meant to be overwritten.
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
                    File.Delete(fullPath);
                }
                catch(Exception e)
                {
                    log.Debug(e);
                    result = ActionResult.Error;
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
            return result;
        }

        private static void DeleteEmptyDirectory(DirectoryInfo dir)
        {
            if (dir == null) return;
            if (!dir.Exists) return;

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
