//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace OpenTap.Package
{
    internal class Installer
    {
        private readonly static TraceSource log =  OpenTap.Log.CreateSource("Installer");
        private CancellationToken cancellationToken;

        internal delegate void ProgressUpdateDelegate(int progressPercent, string message);
        internal event ProgressUpdateDelegate ProgressUpdate;
        internal delegate void ErrorDelegate(Exception ex);
        internal event ErrorDelegate Error;
        
        internal bool DoSleep { get; set; }
        internal List<string> PackagePaths { get; private set; }
        internal string TapDir { get; set; }

        internal bool ForceInstall { get; set; }

        internal Installer(string tapDir, CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            DoSleep = true;
            PackagePaths = new List<string>();
            TapDir = tapDir?.Trim() ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if(ExecutorClient.IsRunningIsolated)
            {
                TapDir = tapDir?.Trim() ?? Directory.GetCurrentDirectory();
            }
            else
            {
                TapDir = tapDir?.Trim() ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }
        }
        
        internal void InstallThread()
        {
            if (cancellationToken.IsCancellationRequested) return;

            try
            {
                waitForPackageFilesFree(TapDir, PackagePaths);

                int progressPercent = 10;
                OnProgressUpdate(progressPercent, "");
                foreach (string fileName in PackagePaths)
                {
                    try
                    {
                        OnProgressUpdate(progressPercent, "Installing " + Path.GetFileNameWithoutExtension(fileName));
                        Stopwatch timer = Stopwatch.StartNew();
                        PackageDef pkg = PluginInstaller.InstallPluginPackage(TapDir, fileName);

                        log.Info(timer, "Installed " + pkg.Name + " version " + pkg.Version);

                        progressPercent += 80 / PackagePaths.Count();

                        if (pkg.Files.Any(s => s.Plugins.Any(p => p.BaseType == nameof(ICustomPackageData))) && PackagePaths.Last() != fileName)
                        {
                            log.Info(timer, $"Package '{pkg.Name}' contains possibly relevant plugins for next package installations. Searching for plugins..");
                            PluginManager.DirectoriesToSearch.Add(TapDir);
                            PluginManager.SearchAsync();
                        }
                    }
                    catch
                    {
                        if(!ForceInstall)
                        {
                            if (PackagePaths.Last() != fileName)
                                log.Warning("Aborting installation of remaining packages (use --force to override this behavior).");
                            throw;
                        }
                        else
                        {
                            if (PackagePaths.Last() != fileName)
                                log.Warning("Continuing installation of remaining packages (--force argument used).");
                        }
                    }
                }
                OnProgressUpdate(90, "");

                if (DoSleep)
                    Thread.Sleep(100);

                OnProgressUpdate(100, "Plugin installed.");
                Thread.Sleep(50); // Let Eventhandler get the last OnProgressUpdate

            }
            catch (Exception ex)
            {
                OnError(ex);
                return;
            }

            Installation installation = new Installation(TapDir);
            installation.AnnouncePackageChange();
        }

        internal void UninstallThread()
        {
            RunCommand("uninstall", false);
        }
        
        internal bool RunCommand(string command, bool force)
        {
            var verb = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(command.ToLower()) + "ed";

            try
            {
                waitForPackageFilesFree(TapDir, PackagePaths);

                if (cancellationToken.IsCancellationRequested)
                {
                    log.Debug("Received abort while waiting for package files to be unlocked.");
                    return false;
                }

                int progressPercent = 10;
                OnProgressUpdate(progressPercent, "");

                PluginInstaller pi = new PluginInstaller();

                foreach (string fileName in PackagePaths)
                {
                    OnProgressUpdate(progressPercent, string.Format("Running command '{0}' on {1}", command, Path.GetFileNameWithoutExtension(fileName)));
                    Stopwatch timer = Stopwatch.StartNew();
                    PackageDef pkg = PackageDef.FromXml(fileName);
                    var res = pi.ExecuteAction(pkg, command, force, TapDir);

                    if (res == ActionResult.Error)
                    {
                        OnProgressUpdate(100, "Done");
                        return false;
                    }
                    else if(res == ActionResult.NothingToDo)
                    {
                        log.Info(string.Format("Tried to {0} {1}, but there was nothing to do.", command, pkg.Name));
                    }
                    else
                        log.Info(timer, string.Format("{1} {0} version {2}.", pkg.Name, verb, pkg.Version));

                    progressPercent += 80 / PackagePaths.Count();
                }
                OnProgressUpdate(90, "");

                if (DoSleep)
                    Thread.Sleep(100);

                OnProgressUpdate(100, "Done");
                Thread.Sleep(50); // Let Eventhandler get the last OnProgressUpdate
            }
            catch (Exception ex)
            {
                OnError(ex);
                return false;
            }

            new Installation(TapDir).AnnouncePackageChange();

            return true;
        }

        private void waitForPackageFilesFree(string tapDir, List<string> PackagePaths)
        {
            List<FileInfo> filesInUse = new List<FileInfo>();

            foreach (string packageFileName in PackagePaths)
            {
                foreach (string file in PluginInstaller.FilesInPackage(packageFileName))
                {
                    string fullPath = Path.Combine(tapDir, file);
                    string filename = Path.GetFileName(file);
                    if (filename == "tap" || filename.ToLower() == "tap.exe") // ignore tap.exe as it is not meant to be overwritten.
                        continue;
                    if (IsFileLocked(new FileInfo(fullPath)))
                        filesInUse.Add(new FileInfo(fullPath));
                }
            }

            // Check if the files that are in use are used by any other package
            var packages = PackagePaths.Select(p => p.EndsWith("TapPackage") ? PackageDef.FromPackage(p) : PackageDef.FromXml(p));
            var remainingInstalledPlugins = new Installation(tapDir).GetPackages().Where(i => packages.Any(p => p.Name == i.Name) == false);
            var filesToRemain = remainingInstalledPlugins.SelectMany(p => p.Files).Select(f => f.RelativeDestinationPath).Distinct(StringComparer.InvariantCultureIgnoreCase);
            filesInUse = filesInUse.Where(f => filesToRemain.Contains(f.Name, StringComparer.InvariantCultureIgnoreCase) == false).ToList();
            
            if (filesInUse.Count > 0)
            {
                log.Info("Following files cannot be modified because they are in use:");
                foreach (var file in filesInUse)
                {
                    log.Info("- " + file.FullName);

                    var loaded_asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.IsDynamic == false && x.Location == file.FullName);
                    if (loaded_asm != null)
                        throw new InvalidOperationException($"The file '{file.FullName}' is being used by this process.");

                }

                var allProcesses = Process.GetProcesses().Where(p => 
                    p.ProcessName.ToLowerInvariant().Contains("opentap") && 
                    p.ProcessName.ToLowerInvariant().Contains("vshost") == false &&
                    p.ProcessName != Assembly.GetExecutingAssembly().GetName().Name).ToArray();
                if (allProcesses.Any())
                {
                    // The file could be locked by someone other than OpenTAP processes. We should not assume it's OpenTAP holding the file.
                    log.Warning(Environment.NewLine + "To continue, try closing applications that could be using the files.");
                    foreach (var process in allProcesses)
                        log.Warning("- " + process.ProcessName);
                }

                log.Warning(Environment.NewLine + "Waiting for files to become unlocked...");

                while (isPackageFilesInUse(tapDir, PackagePaths))
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    if (!isTapRunning())
                        OnError(new IOException("One or more plugin files are in use. View log for more information."));
                    Thread.Sleep(300);
                }
            }
        }

        private bool isTapRunning()
        {
            var pname = Process.GetProcesses().Where(s => s.ProcessName.Contains("Keysight.Tap") && s.ProcessName != Process.GetCurrentProcess().ProcessName);
            if (pname.Count() == 0)
                return false;
            else
                return true;
        }

        private bool isPackageFilesInUse(string tapDir, List<string> packagePaths)
        {
            foreach (string packageFileName in PackagePaths)
            {
                foreach (string file in PluginInstaller.FilesInPackage(packageFileName))
                {
                    string fullPath = Path.Combine(tapDir, file);
                    if (IsFileLocked(new FileInfo(fullPath)))
                        return true;
                }
            }

            return false;
        }

        private bool IsFileLocked(FileInfo file)
        {
            if (!file.Exists)
                return false;
            FileStream stream = null;
            try
            {
                stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (DirectoryNotFoundException)
            {
                // the directory of the file doesn't even exist!
                return false;
            }
            catch (FileNotFoundException)
            {
                // the file doesn't even exist!
                return false;
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }
            //file is not locked
            return false;
        }

        /// <summary>
        /// Triggers the Error event.
        /// </summary>
        private void OnError(Exception ex)
        {
            ErrorDelegate handler = Error;
            if (handler != null)
                handler(ex);
            else
                log.Error(ex);
        }

        /// <summary>
        /// Triggers the ProgressUpdate event.
        /// </summary>
        private void OnProgressUpdate(int progressPercent, string message = null)
        {
            ProgressUpdateDelegate handler = ProgressUpdate;
            if (handler != null)
                handler(progressPercent, message);
        }
    }
}
