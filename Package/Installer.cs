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
        private bool hasSetPath = false;
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

        internal bool SetWorkingDir()
        {
            if (hasSetPath)
                return true;

            //Get all paths as fullpaths since we are changing current directory to where this is run.
            for (int i = 0; i < PackagePaths.Count(); i++)
            {
                PackagePaths[i] = Path.GetFullPath(PackagePaths[i]);
            }

            // change directory to the app dir
            Directory.SetCurrentDirectory(TapDir);
            log.Debug("Running {0}.", Path.GetFileName(Assembly.GetExecutingAssembly().Location));
            log.Debug("Operating in folder '{0}'.", TapDir);

            hasSetPath = true;

            return true;
        }
        
        internal void InstallThread()
        {
            if (cancellationToken.IsCancellationRequested) return;

            if (!SetWorkingDir())
                return;

            try
            {
                waitForPackageFilesFree(PackagePaths);

                int progressPercent = 10;
                OnProgressUpdate(progressPercent, "");
                foreach (string fileName in PackagePaths)
                {
                    try
                    {
                        OnProgressUpdate(progressPercent, "Installing " + Path.GetFileNameWithoutExtension(fileName));
                        Stopwatch timer = Stopwatch.StartNew();
                        PackageDef pkg = PluginInstaller.InstallPluginPackage(fileName);
                        log.Info(timer, "Installed " + pkg.Name + " version " + pkg.Version);

                        progressPercent += 80 / PackagePaths.Count();
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

            using (var changeId = new ChangeId(TapDir))
                changeId.SetChangeId(changeId.GetChangeId() + 1);
        }

        internal void UninstallThread()
        {
            RunCommand("uninstall", false);
        }
        
        internal bool RunCommand(string command, bool force)
        {
            if (!SetWorkingDir())
                return false;

            var verb = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(command.ToLower()) + "ed";

            try
            {
                waitForPackageFilesFree(PackagePaths);

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
                    PackageDef pkg = PackageDef.FromXmlFile(fileName);
                    var res = pi.ExecuteAction(pkg, command, force);

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

            using (var changeId = new ChangeId(TapDir))
                changeId.SetChangeId(changeId.GetChangeId() + 1);

            return true;
        }

        private void waitForPackageFilesFree(List<string> PackagePaths)
        {
            List<FileInfo> filesInUse = new List<FileInfo>();
            string endDir = Directory.GetCurrentDirectory();

            foreach (string packageFileName in PackagePaths)
            {
                foreach (string file in PluginInstaller.FilesInPackage(packageFileName))
                {
                    string fullPath = Path.Combine(endDir, file);
                    string filename = Path.GetFileName(file);
                    if (filename == "tap" || filename.ToLower() == "tap.exe") // ignore tap.exe as it is not meant to be overwritten.
                        continue;
                    if (IsFileLocked(new FileInfo(fullPath)))
                        filesInUse.Add(new FileInfo(fullPath));
                }
            }

            // Check if the files that are in use are used by any other package
            var packages = PackagePaths.Select(p => p.EndsWith("TapPackage") ? PackageDef.FromPackage(p) : PackageDef.FromXmlFile(p));
            var remainingInstalledPlugins = new Installation(Directory.GetCurrentDirectory()).GetPackages().Where(i => packages.Any(p => p.Name == i.Name) == false);
            var filesToRemain = remainingInstalledPlugins.SelectMany(p => p.Files).Select(f => f.RelativeDestinationPath).Distinct(StringComparer.InvariantCultureIgnoreCase);
            filesInUse = filesInUse.Where(f => filesToRemain.Contains(f.Name, StringComparer.InvariantCultureIgnoreCase) == false).ToList();
            
            if (filesInUse.Count > 0)
            {
                log.Info("Following files cannot be modified because they are in use:");
                foreach (var file in filesInUse)
                {
                    log.Info("- " + file.FullName);
                }

                var allProcesses = Process.GetProcesses().Where(p => 
                    p.ProcessName.ToLowerInvariant().Contains("opentap") && 
                    p.ProcessName.ToLowerInvariant().Contains("vshost") == false &&
                    p.ProcessName != Assembly.GetExecutingAssembly().GetName().Name).ToArray();
                if (allProcesses.Any())
                {
                    // The file could be locked by someone other than TAP processes. We should not assume it's TAP holding the file.
                    log.Warning(Environment.NewLine + "To continue, try closing applications that could be using the files.");
                    foreach (var process in allProcesses)
                        log.Warning("- " + process.ProcessName);
                }

                log.Warning(Environment.NewLine + "Waiting for files to become unlocked...");

                while (isPackageFilesInUse(PackagePaths))
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

        private bool isPackageFilesInUse(List<string> packagePaths)
        {
            string endDir = Directory.GetCurrentDirectory();
            foreach (string packageFileName in PackagePaths)
            {
                foreach (string file in PluginInstaller.FilesInPackage(packageFileName))
                {
                    string fullPath = Path.Combine(endDir, file);
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
