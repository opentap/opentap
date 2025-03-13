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
using System.Text;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Package
{
    internal class Installer
    {
        private static readonly TraceSource log =  OpenTap.Log.CreateSource("Installer");
        private CancellationToken cancellationToken;

        internal delegate void ProgressUpdateDelegate(int progressPercent, string message);
        internal event ProgressUpdateDelegate ProgressUpdate;
        internal delegate void ErrorDelegate(Exception ex);
        internal event ErrorDelegate Error;

        internal bool DoSleep { get; set; }
        internal List<string> PackagePaths { get; private set; }
        internal string TapDir { get; set; }
        internal bool UnpackOnly { get; set; }

        internal bool ForceInstall { get; set; }

        internal Installer(string tapDir, CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            DoSleep = true;
            UnpackOnly = false;
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
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            try
            {
                WaitForPackageFilesFree(TapDir, PackagePaths);

                // The packages have all been downloaded at this stage. Now we just need to install them.
                // Assume this accounts for roughly 60% of the installation process.
                int progressPercent = 60;
                OnProgressUpdate(progressPercent, "Installing packages.");
                foreach (string fileName in PackagePaths)
                {
                    try
                    {
                        progressPercent += 30 / PackagePaths.Count();

                        log.Info($"Installing {fileName}");
                        OnProgressUpdate(progressPercent, "Installing " + Path.GetFileNameWithoutExtension(fileName));
                        Stopwatch timer = Stopwatch.StartNew();
                        PackageDef pkg = PluginInstaller.InstallPluginPackage(TapDir, fileName, UnpackOnly);

                        log.Info(timer, $"Installed {pkg.Name} version {pkg.Version}");

                        if (pkg.Files.Any(s => s.Plugins.Any(p => p.BaseType == nameof(ICustomPackageData))) &&
                            PackagePaths.Last() != fileName)
                        {
                            var newPlugins = pkg.Files.SelectMany(s => s.Plugins.Select(t => t))
                                .Where(t => t.BaseType == nameof(ICustomPackageData));
                            if (newPlugins.Any(np =>
                                    TypeData.GetTypeData(np.Name) ==
                                    null)) // Only search again, if the new plugins are not already loaded.
                            {
                                if (ExecutorClient.IsRunningIsolated)
                                {
                                    // Only load installed assemblies if we're running isolated. 
                                    log.Info(timer,
                                        $"Package '{pkg.Name}' contains possibly relevant plugins for next package installations. Searching for plugins..");
                                    PluginManager.DirectoriesToSearch.Add(TapDir);
                                    PluginManager.SearchAsync();
                                }
                                else
                                    log.Warning(
                                        $"Package '{pkg.Name}' contains possibly relevant plugins for next package installations, but these will not be loaded.");
                            }
                        }
                    }
                    catch
                    {
                        if (!ForceInstall)
                        {
                            if (PackagePaths.Last() != fileName)
                                log.Warning(
                                    "Aborting installation of remaining packages (use --force to override this behavior).");
                            PackageDef failedPackage = PackageDef.FromPackage(fileName);
                            throw new ExitCodeException((int)PackageExitCodes.PackageInstallError,
                                $"Package failed to install: {failedPackage.Name} version {failedPackage.Version} ({fileName})");
                        }
                        else
                        {
                            if (PackagePaths.Last() != fileName)
                                log.Warning("Continuing installation of remaining packages (--force argument used).");
                        }
                    }
                }


                if (DoSleep)
                    Thread.Sleep(100);

                OnProgressUpdate(100, $"Package installation finished.");
                Thread.Sleep(50);
            }
            catch (Exception ex)
            {
                OnError(ex);
                throw new ExitCodeException((int)PackageExitCodes.PackageInstallError, $"Failed to install packages");
            }

            Installation installation = new Installation(TapDir);
            installation.AnnouncePackageChange();
        }

        internal int UninstallThread()
        {
            var status = RunCommand(PrepareUninstall, false, false);
            if (status == (int)ExitCodes.Success)
                status = RunCommand(Uninstall, false, true);
            return status;
        }

        internal const string Uninstall = "uninstall";
        internal const string PrepareUninstall = "prepareuninstall";
        internal const string Install = "install";
        internal const string Test = "test";

        internal int RunCommand(string command, bool force, bool modifiesPackageFiles)
        {
            // Example usage:
            // "Successfully {pastTense} {pkg.Name} version {pkg.Version}."
            Dictionary<string, string> pastTenseLookup = new(StringComparer.OrdinalIgnoreCase)
            {
                [PrepareUninstall] = "prepared to uninstall", 
                [Uninstall] = "uninstalled",
                [Install] = "installed",
                [Test] = "tested",
            };
            
            // Example usages:
            // "Tried to {commandFriendlyName} {pkg.Name}, but there was nothing to do."
            // "There was an error while trying to {commandFriendlyName} '{pkg.Name}'."
            Dictionary<string, string> friendlyNameLookup = new(StringComparer.OrdinalIgnoreCase)
            { 
                [PrepareUninstall] = "prepare uninstalling",
                [Uninstall] = "uninstall",
                [Install] = "install",
                [Test] = "test",
            };
            
            var pastTense = pastTenseLookup[command];
            var friendlyName = friendlyNameLookup[command];

            try
            {
                if (modifiesPackageFiles)
                {
                    try
                    {
                        WaitForPackageFilesFree(TapDir, PackagePaths);
                    }

                    catch (Exception ex)
                    {
                        log.Warning("Uninstall stopped while waiting for package files to become unlocked.");
                        if (!force)
                        {
                            OnError(ex);
                            throw;
                        }
                    }
                }

                double progressPercent = 10;
                OnProgressUpdate((int)progressPercent, "");

                PluginInstaller pi = new PluginInstaller();

                foreach (string fileName in PackagePaths)
                {
                    PackageDef pkg = PackageDef.FromXml(fileName);
                    pkg.PackageSource = new XmlPackageDefSource { PackageDefFilePath = fileName };

                    OnProgressUpdate((int)progressPercent, $"Running command '{friendlyName}' on '{pkg.Name}'");
                    Stopwatch timer = Stopwatch.StartNew();
                    var res = pi.ExecuteAction(pkg, command, force, TapDir);

                    if (res == ActionResult.Error)
                    {
                        if (!force)
                        {
                            OnProgressUpdate(100, "Done");
                            return (int)ExitCodes.GeneralException;
                        }
                        else
                            log.Warning($"There was an error while trying to {friendlyName} '{pkg.Name}'.");
                    }
                    else if (res == ActionResult.NothingToDo)
                    {
                        log.Debug($"Tried to {friendlyName} {pkg.Name}, but there was nothing to do.");
                    }
                    else
                        log.Info(timer, $"Successfully {pastTense} {pkg.Name} version {pkg.Version}.");

                    progressPercent += (double)80 / PackagePaths.Count();
                }

                OnProgressUpdate(90, "");

                if (DoSleep)
                    Thread.Sleep(100);

                OnProgressUpdate(100, "Done");
                Thread.Sleep(50); // Let Eventhandler get the last OnProgressUpdate
            }
            catch (Exception ex)
            {
                if (ex is ExitCodeException ec)
                {
                    log.Error(ec.Message);
                    return ec.ExitCode;
                }

                if (ex is OperationCanceledException)
                    return (int)ExitCodes.UserCancelled;

                log.Debug(ex);
                return (int)ExitCodes.GeneralException;
            }

            new Installation(TapDir).AnnouncePackageChange();

            return (int)ExitCodes.Success;
        }

        // ignore tap.exe and tap.dll as it is not meant to be overwritten.
        private bool exclude(string filename) => filename.ToLower() == "tap" || filename.ToLower() == "tap.exe" || filename.ToLower() == "tap.dll";
        private FileInfo[] GetFilesInUse(string tapDir, List<string> packagePaths)
        {
            List<FileInfo> filesInUse = new List<FileInfo>();

            foreach (string packageFileName in packagePaths)
            {
                foreach (string file in PluginInstaller.FilesInPackage(packageFileName))
                {
                    string fullPath = Path.Combine(tapDir, file);
                    string filename = Path.GetFileName(file);
                    if (exclude(filename))
                        continue;
                    if (IsFileLocked(new FileInfo(fullPath)))
                        filesInUse.Add(new FileInfo(fullPath));
                }
            }

            // Check if the files that are in use are used by any other package
            var packages = packagePaths.Select(p => p.EndsWith("TapPackage") ? PackageDef.FromPackage(p) : PackageDef.FromXml(p));
            var remainingInstalledPlugins = new Installation(tapDir).GetPackages().Where(i => packages.Any(p => p.Name == i.Name) == false);
            var filesToRemain = remainingInstalledPlugins.SelectMany(p => p.Files).Select(f => f.RelativeDestinationPath).Distinct(StringComparer.InvariantCultureIgnoreCase);
            filesInUse = filesInUse.Where(f => filesToRemain.Contains(f.Name, StringComparer.InvariantCultureIgnoreCase) == false).ToList();

            return filesInUse.ToArray();
        }

        private void WaitForPackageFilesFreeWindows(List<string> packagePaths)
        {
            var allfiles = packagePaths.SelectMany(PluginInstaller.FilesInPackage).ToArray();

            retry:
            var procs = RestartManager.GetProcessesUsingFiles(allfiles);
            if (procs.Count == 0)
                return;
            var msg = new StringBuilder();
            msg.AppendLine("The following applications are blocking the operation:");
            var procString = string.Join("", procs.Select(p => $"\n - {p}"));
            msg.AppendLine(procString);
            msg.AppendLine("\nPlease close these applications and try again.");

            var req = new AbortOrShutdownRequest("Files In Use", msg.ToString());
            UserInput.Request(req);
            if (req.Response == AbortOrRetryOrShutdownResponse.Retry) 
                goto retry;
            else if (req.Response == AbortOrRetryOrShutdownResponse.Abort)
            {
                OnError(new IOException(msg.ToString()));
                throw new OperationCanceledException(); 
            }
        }
        private void WaitForPackageFilesFree(string tapDir, List<string> packagePaths)
        {
            var noninteractive = UserInput.GetInterface() is NonInteractiveUserInputInterface;
            if (OperatingSystem.Current == OperatingSystem.Windows && noninteractive == false)
            {
                try
                {
                    WaitForPackageFilesFreeWindows(packagePaths);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch 
                {
                    // fallback to old logic -- This shouldn't happen, but let's be safe.
                }
            }
            var filesInUse = GetFilesInUse(tapDir, packagePaths);

            if (filesInUse.Length > 0)
            {
                var allProcesses = Process.GetProcesses().Where(p =>
                    p.ProcessName.ToLowerInvariant().Contains("opentap") &&
                    p.ProcessName.ToLowerInvariant().Contains("vshost") == false &&
                    p.ProcessName != Assembly.GetExecutingAssembly().GetName().Name).ToArray();

                if (allProcesses.Any())
                {
                    // The file could be locked by someone other than OpenTAP processes. We should not assume it's OpenTAP holding the file.
                    log.Warning(Environment.NewLine +
                                "To continue, try closing applications that could be using the files.");
                    foreach (var process in allProcesses)
                        log.Warning("- " + process.ProcessName);
                }

                var tries = 0;
                const int maxTries = 10;
                var delaySeconds = 3;
                var inUseString = BuildString(filesInUse);
                if (noninteractive)
                    log.Warning(inUseString);

                while (isPackageFilesInUse(tapDir, packagePaths, exclude))
                {
                    var req = new AbortOrRetryRequest("Package Files Are In Use", inUseString) {Response = AbortOrRetryResponse.Abort};
                    UserInput.Request(req, waitForFilesTimeout, true);

                    if (req.Response == AbortOrRetryResponse.Abort)
                    {
                        if (noninteractive && tries < maxTries)
                        {
                            tries += 1;
                            log.Info($"Package files are in use. Retrying in {delaySeconds} seconds. ({tries} / {maxTries})");
                            TapThread.Sleep(TimeSpan.FromSeconds(delaySeconds));
                            continue;
                        }

                        OnError(new IOException(inUseString));
                        throw new OperationCanceledException();
                    }

                    filesInUse = GetFilesInUse(tapDir, packagePaths);
                    inUseString = BuildString(filesInUse);
                }
            }
        }

        private string BuildString(FileInfo[] filesInUse)
        {
            var sb = new StringBuilder();
            sb.AppendLine("The following files cannot be modified because they are in use:");
            foreach (var file in filesInUse)
            {
                sb.AppendLine("- " + file.FullName);

                var loaded_asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(x => x.IsDynamic == false && x.Location == file.FullName);
                if (loaded_asm != null)
                    throw new InvalidOperationException(
                        $"The file '{file.FullName}' is being used by this process.");
            }

            sb.AppendLine("To continue, try closing applications that could be using the files.");

            return sb.ToString();
        }


        static readonly TimeSpan waitForFilesTimeout = TimeSpan.FromMinutes(2);

        private bool isPackageFilesInUse(string tapDir, List<string> packagePaths, Func<string, bool> exclude = null)
        {
            foreach (string packageFileName in packagePaths)
            {
                foreach (string file in PluginInstaller.FilesInPackage(packageFileName))
                {
                    string fullPath = Path.Combine(tapDir, file);
                    string filename = Path.GetFileName(file);
                    if (exclude != null && exclude(filename))
                        continue;
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
            catch (UnauthorizedAccessException)
            {
              log.Warning($"File {file.FullName} cannot be deleted by the current user. ({Environment.UserName})");
              throw;
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

    enum AbortOrRetryResponse
    {
        Abort,
        Retry
    }

    class AbortOrRetryRequest
    {
        public AbortOrRetryRequest(string title, string message)
        {
            Message = message;
            Name = title;
        }
        
        [Browsable(false)] public string Name { get; }

        [Browsable(true)]
        [Layout(LayoutMode.FullRow)]
        public string Message { get; }
        [Layout(LayoutMode.FullRow | LayoutMode.FloatBottom)]
        [Submit] public AbortOrRetryResponse Response { get; set; }
    }
    enum AbortOrRetryOrShutdownResponse
    {
        Abort,
        Retry,
    }

    class AbortOrShutdownRequest
    {
        public AbortOrShutdownRequest(string title, string message)
        {
            Message = message;
            Name = title;
        }
        
        [Browsable(false)] public string Name { get; } 

        [Browsable(true)]
        [Layout(LayoutMode.FullRow)]
        [Display("Message", Order: 1)]
        public string Message { get; }

        [Layout(LayoutMode.FullRow | LayoutMode.FloatBottom)]
        [Submit] public AbortOrRetryOrShutdownResponse Response { get; set; }
    }
}
