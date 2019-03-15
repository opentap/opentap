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
        public Func<PluginInstaller, ActionExecuter, PackageDef, bool, ActionResult> Execute { get; set; }

        public ActionResult DoExecute(PluginInstaller pluginInstaller, PackageDef package, bool force)
        {
            try
            {
                if (Execute != null)
                {
                    return Execute(pluginInstaller, this, package, force);
                }
                else
                    return ExecutePackageActionSteps(package, force);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                return ActionResult.Error;
            }
        }

        internal ActionResult ExecutePackageActionSteps(PackageDef package, bool force)
        {
            ActionResult res = ActionResult.NothingToDo;

            foreach (var step in package.PackageActionExtensions)
            {
                if (step.ActionName != ActionName)
                    continue;

                if (File.Exists(step.ExeFile) == false)
                    throw new Exception($"Could not find file '{step.ExeFile}' from ActionStep '{step.ActionName}'.");
                
                // Upgrade to ok output
                res = ActionResult.Ok;

                log.Debug("Running '{0}' with arguments: '{1}'", step.ExeFile, step.Arguments);

                var pi = new ProcessStartInfo(step.ExeFile, step.Arguments);

                pi.CreateNoWindow = step.CreateNoWindow;

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

            using (var zip = new ZipArchive(File.OpenRead(packagePath), ZipArchiveMode.Read))
            {
                foreach (var part in zip.Entries)
                {
                    if (part.Name == "[Content_Types].xml" || part.Name == ".rels" || part.FullName.StartsWith("package/services/metadata/core-properties"))
                        continue; // skip strange extra files that are created by System.IO.Packaging.Package (TAP 7.x)

                    string path = Uri.UnescapeDataString(part.FullName);
                    files.Add(path);
                }
            }
            return files;

        }
        
        /// <summary>
        /// Only public method. Tries to install a plugin from 'path', throws an exception on error.
        /// </summary>
        /// <param name="path">Absolute or relative path to tap plugin</param>
        /// <returns>List of installed parts.</returns>
        internal static PackageDef InstallPluginPackage(string tapDir, string path)
        {
            checkExtension(path);
            checkFileExists(path);

            var package = PackageDef.FromPackage(path);
            var destination = package.IsSystemWide() ? PackageDef.SystemWideInstallationDirectory : tapDir;

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
                tryUninstall(path, package);
                throw new Exception($"Failed to install package '{path}'.", e);
            }

            var pi = new PluginInstaller();
            if (pi.ExecuteAction(package, "install", false) == ActionResult.Error)
            {
                log.Error($"Install package action failed to execute for '{package.Name}'.");
                tryUninstall(path, package);
                throw new Exception($"Failed to install package '{path}'.");
            }


            CustomPackageActionHelper.RunCustomActions(package, PackageActionStage.Install, new CustomPackageActionArgs(null, false));

            
            return package;
        }

        static void tryUninstall(string path, PackageDef package)
        {
            log.Info("Uninstalling package '{0}'.", package.Name);
            Uninstall(package);
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
            using (var zip = new ZipArchive(File.OpenRead(packagePath), ZipArchiveMode.Read))
            {
                foreach (var part in zip.Entries)
                {
                    if (part.Name == "[Content_Types].xml" || part.Name == ".rels" || part.FullName.StartsWith("package/services/metadata/core-properties"))
                        continue; // skip strange extra files that are created by System.IO.Packaging.Package (TAP 7.x)
                    if (string.IsNullOrWhiteSpace(part.Name))
                        continue;

                    
                    string path = Uri.UnescapeDataString(part.FullName).Replace('\\', '/');
                    path = Path.Combine(destinationDir, path).Replace('\\', '/');
                    
                    
                    int Retries = 0, MaxRetries = 10;
                    while(true)
                    {
                        try
                        {
                            FileSystemHelper.EnsureDirectory(path);
                            var deflate_stream = part.Open();
                            using (var fileStream = File.Create(path))
                            {
                                deflate_stream.CopyTo(fileStream);
                            }

                            installedParts.Add(path);
                            break;
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
            using (var zip = new ZipArchive(File.OpenRead(packagePath), ZipArchiveMode.Read))
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
            return false;
        }

        /// <summary>
        /// Uninstalls a package.
        /// </summary>
        /// <param name="package"></param>
        internal static void Uninstall(PackageDef package)
        {
            
            var pi = new PluginInstaller();

            pi.ExecuteAction(package, "uninstall", true);
        }

        internal ActionResult ExecuteAction(PackageDef package, string actionName, bool force)
        {
            ActionExecuter action = builtinActions.FirstOrDefault(r => r.ActionName == actionName);

            if (action == null)
                action = new ActionExecuter { ActionName = actionName };

            return action.DoExecute(this, package, force);
        }

        private static ActionResult DoUninstall(PluginInstaller pluginInstaller, ActionExecuter action, PackageDef package, bool force)
        {
            
            var result = ActionResult.Ok;
            var destination = package.IsSystemWide() ? PackageDef.SystemWideInstallationDirectory : Directory.GetCurrentDirectory();
          
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
                if (action.ExecutePackageActionSteps(package, force) == ActionResult.Error)
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

                var fullPath = Path.GetFullPath(file.RelativeDestinationPath);
                if (package.IsSystemWide())
                {
                    fullPath = Path.Combine(PackageDef.SystemWideInstallationDirectory, file.RelativeDestinationPath);
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

            var packageFile = PackageDef.GetDefaultPackageMetadataPath(package);
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

        static IMemorizer<string, PackageDef> installedPackageMemorizer = new Memorizer<string, PackageDef, string>(null, loadPackageDef)
        {
            Validator = file => new FileInfo(file).LastWriteTimeUtc.Ticks
        };

        static PackageDef loadPackageDef(string file)
        {
            try
            {
                using (var f = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    return PackageDef.FromXml(f);
            }
            catch (Exception e)
            {
                log.Warning("Unable to read package file '{0}'. Moving it to '.broken'", file);
                log.Debug(e);
                var brokenfile = file + ".broken";
                if (File.Exists(brokenfile))
                    File.Delete(brokenfile);
                File.Move(file, brokenfile);
            }
            return null;
        }
    }
}
