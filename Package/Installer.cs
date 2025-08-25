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
                RenamePackageFiles(TapDir, PackagePaths);

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
                    catch (Exception ex) when (ex is not ExitCodeException)
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
                if (ex is ExitCodeException)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
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
                        RenamePackageFiles(TapDir, PackagePaths);
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

        public static void RenamePackageFiles(string tapDir, PackageDef package)
        {
            var locId = Guid.NewGuid().ToString();
            var loc = Path.Combine(Path.GetTempPath(), locId);
            
            List<(string Source, string Destination)> moves = new();

            void UndoMoves()
            {
                foreach (var (source, destination) in moves)
                {
                    File.Move(destination, source);
                }
            }
            
            foreach (var packageFile in package.Files)
            {
                string file = packageFile.RelativeDestinationPath;
                string source = Path.Combine(tapDir, file);
                if (!File.Exists(source)) continue;
                string destination = Path.Combine(loc, file);
                try
                {
                    // On Windows, it is not possible to delete an open file,
                    // but it is possible to rename / move it. We can make use
                    // of this to allow in-place updates of in-use applications.
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Move(source, destination);
                    moves.Add((source, destination));
                }
                catch
                {
                    UndoMoves();
                    throw;
                }
            }
            
        }
        
        public static void RenamePackageFiles(string tapDir, List<string> packagePaths)
        {
            var locId = Guid.NewGuid().ToString();
            var loc = Path.Combine(Path.GetTempPath(), locId);
            List<(string Source, string Destination)> moves = new();

            void UndoMoves()
            {
                foreach (var (source, destination) in moves)
                {
                    File.Move(destination, source);
                }
            }

            foreach (string packageFileName in packagePaths)
            {
                foreach (string file in PluginInstaller.FilesInPackage(packageFileName))
                {
                    string source = Path.Combine(tapDir, file);
                    if (!File.Exists(source)) continue;
                    string destination = Path.Combine(loc, file);
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        File.Move(source, destination);
                        moves.Add((source, destination));
                    }
                    catch
                    {
                        UndoMoves();
                        throw;
                    }
                }
            }
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
}
