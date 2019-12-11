//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading;

namespace OpenTap
{
    /// <summary>
    /// SessionLogs are logs of all events that occur during startup/shutdown of a OpenTAP session and of all the session's TestPlan runs. 
    /// A new session log is created at the start of each session, which starts when a process is launched and ends when it closes. 
    /// Only 10 session logs are allowed to exist, with the oldest deleted as new logs are created. 
    /// By comparison, a result log typically shows only the log activity for a single run of a TestPlan. 
    /// </summary>
    public static class SessionLogs
    {
        private static readonly TraceSource log = Log.CreateSource("Session");

        /// <summary> The number of files kept at a time. </summary>
        const int maxNumberOfTraceFiles = 20;

        /// <summary> The maximally allowed size of trace files. </summary>
        const long maxTotalSizeOfTraceFiles = 2_000_000_000L; 

        /// <summary>
        /// If two sessions needs the same log file name, an integer is added to the name. 
        /// This is the max number of times that we are going to test new names.
        /// </summary>
        const int maxNumberOfConcurrentSessions = maxNumberOfTraceFiles;

        private static FileTraceListener traceListener;

        /// <summary>
        /// File path to the current log file. Path is updated upon UI launch.
        /// </summary>
        public static string GetSessionLogFilePath() => currentLogFile;

        static string currentLogFile;

        /// <summary>
        /// Initializes the logging. Uses the following file name formatting: SessionLogs\\[Application Name]\\[Application Name] [yyyy-MM-dd HH-mm-ss].txt.
        /// </summary>
        public static void Initialize()
        {
            if (currentLogFile != null) return;
            
           

            var timestamp = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToString("yyyy-MM-dd HH-mm-ss");

            // Path example: <TapDir>/SessionLogs/SessionLog <timestamp>.txt
            string pathEnding = $"SessionLog {timestamp}";

            if (Assembly.GetEntryAssembly() != null && !String.IsNullOrWhiteSpace(Assembly.GetEntryAssembly().Location))
            {
                string exeName = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
                // Path example: <TapDir>/SessionLogs/tap/tap <timestamp>.txt
                pathEnding = $"{exeName} {timestamp}";
            }

            Initialize($"{FileSystemHelper.GetCurrentInstallationDirectory()}/SessionLogs/{pathEnding}.txt");
        }
        
        /// <summary>
        /// Initializes the logging. 
        /// </summary>
        public static void Initialize(string tempLogFileName)
        {
            if (currentLogFile == null)
            {
                Rename(tempLogFileName);
                SystemInfoTask = Task.Factory
                    // Ensure that the plugin manager is loaded before running SystemInfo.
                    // this ensures that System.Runtime.InteropServices.RuntimeInformation.dll is loaded. (issue #4000).
                    .StartNew(PluginManager.Load)
                    // then get the system info on a separate thread (it takes ~1s)
                    .ContinueWith(tsk => SystemInfo()); 

                AppDomain.CurrentDomain.ProcessExit += FlushOnExit;
                AppDomain.CurrentDomain.UnhandledException += FlushOnExit;
            }
            else
            {
                if (currentLogFile != tempLogFileName)
                    Rename(tempLogFileName);
            }

            currentLogFile = tempLogFileName;

            // Log debugging information of the current process.
            log.Debug($"Running '{Environment.CommandLine}' in '{Directory.GetCurrentDirectory()}'.");
        }

        private static void FlushOnExit(object sender, EventArgs e)
        {
            try
            {
                SystemInfoTask.Wait(); // wait for the task if we are crashing to make sure the info is in the log
            }
            catch (Exception ex)
            {
                if (ex is ThreadAbortException == false)
                    log.Debug("Unexpected error while printing system information.");
            }
            Flush();
        }

        /// <summary>
        /// Class for managing a hidden file containing a list of recent files. This can be accessed by multiple processes, so possible race conditions needs to be handled.
        /// </summary>
        class RecentFilesList
        {
            IEnumerable<string> names;
            private string name = null;
            public RecentFilesList(List<string> filenameoptions)
            {
                names = filenameoptions;
            }
            static string[] readLinesSafe(string name)
            {
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        return File.ReadAllLines(name);
                    }
                    catch (Exception) when (i < 4)
                    {
                        Thread.Sleep(100);
                    }
                }

                return null;
            }

            static void setHidden(string name)
            {
                if (OperatingSystem.Current == OperatingSystem.Windows)
                    File.SetAttributes(name, FileAttributes.Hidden | FileAttributes.Archive);
            }

            string[] ensureFileExistsAndReadLines()
            {
                var _names = names;
                if (name != null)
                    _names = new[] {name}; // a name was already decided.
                foreach (var name in _names)
                {
                    try
                    {
                        if (!File.Exists(name))
                        {
                            File.Create(name).Close();

                            try
                            {
                                setHidden(name);
                            }
                            catch
                            {
                                // The file could not be made hidden. This is probably ok.
                            }
                            this.name = name;
                            return Array.Empty<string>();
                        }
                        if(readLinesSafe(name) is string[] str) 
                        {
                            this.name = name;
                            return str;
                        }
                    }
                    catch
                    {
                        // ignore exceptions throws. Try a different file.   
                    }
                }
                return Array.Empty<string>();
            }


            static Mutex recentLock = new Mutex(false, "opentap_recent_logs_mutex");

            public string[] GetRecent()
            {
                recentLock.WaitOne();
                try
                {
                    return ensureFileExistsAndReadLines();
                }
                finally
                {
                    recentLock.ReleaseMutex();
                }
            }

            public void AddRecent(string newname)
            {
                // Important to lock the file and to re-read if the file was changed since last checked.
                // otherwise there is a risk that a log file will be forgotten and never cleaned up.
                recentLock.WaitOne();
                try
                {
                    var currentFiles = ensureFileExistsAndReadLines().Append(newname).DistinctLast();
                    currentFiles.RemoveIf(x => File.Exists(x) == false);
                    using (var f = File.Open(name, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                    {
                        f.SetLength(0);
                        using (var sw = new StreamWriter(f))
                        {
                            currentFiles.ForEach(sw.WriteLine);
                        }
                    }
                }
                finally
                {
                    recentLock.ReleaseMutex();
                }
            }
        }

        private const string RecentFilesName = ".opentap_recent_logs";
        static List<string> getLogRecentFilesName()
        {
            // If the tap installation folder is write-protected by the user,
            // we'll try to find some other valid location instead.
            List<string> options = new List<string>();

            void addOption(Func<string> f)
            {
                try
                {
                    string option = f();
                    if(option != null)
                        options.Add(option);
                }
                catch
                {
                    
                }
            }
            if (ExecutorClient.IsRunningIsolated)
            {   // Use the recent system logs from original directory to avoid leaking log files.
                addOption(() => Path.Combine(ExecutorClient.ExeDir, RecentFilesName));
            }
            addOption(() => Path.Combine(Path.GetDirectoryName(typeof(SessionLogs).Assembly.Location), RecentFilesName));
            addOption(() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), RecentFilesName));
            addOption(() => RecentFilesName);

            return options;
        }
        
        static RecentFilesList recentSystemlogs = new RecentFilesList(getLogRecentFilesName());

        /// <summary>
        /// Renames a previously initialized temporary log file.
        /// </summary>
        static void rename(string path, bool newFile = false)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            List<string> recentFiles = recentSystemlogs.GetRecent().Where(File.Exists).ToList();
            long getTotalSize()
            {
                return recentFiles.Select(x => new FileInfo(x).Length).Sum();
            }
            
            bool checkCondition()
            {
                bool tooManyFiles = (recentFiles.Count + 1) > maxNumberOfTraceFiles ;
                if (tooManyFiles) return true;

                if (recentFiles.Count <= 2) return false; // Do not remove the last couple of log files, event though they might exceed limits.
                var totalSize = getTotalSize();
                bool filesTooBig = totalSize > maxTotalSizeOfTraceFiles;
                if (filesTooBig) return true;
                return false;
            }

            int ridx = 0;
            while (checkCondition() && ridx < recentFiles.Count)
            {
                try
                {
                    if (File.Exists(recentFiles[ridx]))
                        File.Delete(recentFiles[ridx]);
                    recentFiles.RemoveAt(ridx);
                }
                catch (Exception)
                {
                    ridx++;
                }
            }
            string name = Path.GetFileNameWithoutExtension(path);
            string dir = Path.GetDirectoryName(path);
            string ext = Path.GetExtension(path);
            bool fileNameChanged = false;
            for (int idx = 0; idx < maxNumberOfConcurrentSessions; idx++)
            {
                try
                {
                    path = Path.Combine(dir, name + (idx == 0 ? "" : idx.ToString()) + ext);
                    if (traceListener == null || newFile)
                    {
                        if (string.IsNullOrWhiteSpace(dir) == false)
                            Directory.CreateDirectory(Path.GetDirectoryName(path));
                        traceListener = new FileTraceListener(path) { FileSizeLimit = 100000000 }; // max size for log files is 100MB.
                        traceListener.FileSizeLimitReached += TraceListener_FileSizeLimitReached;
                        Log.AddListener(traceListener);
                    }
                    else
                    {
                        traceListener.ChangeFileName(path);
                    }
                    fileNameChanged = true;
                    break;
                }
                catch
                {
                    // File was probably locked by another process.
                }
            }
            if (!fileNameChanged)
            {
                log.Debug("Unable to rename log file. Continuing with log '{0}'.", currentLogFile);
            }
            else
            {
                currentLogFile = path;
                log.Debug(sw, "Session log loaded as '{0}'.", currentLogFile);
                recentSystemlogs.AddRecent(Path.GetFullPath(path));
            }
        }

        static int sessionLogCount = 0;
        static object sessionLogRotateLock = new object();
        static void TraceListener_FileSizeLimitReached(object sender, EventArgs e)
        {
            traceListener.FileSizeLimitReached -= TraceListener_FileSizeLimitReached;
            
            Task.Factory.StartNew(() =>
            {
                lock (sessionLogRotateLock)
                {
                    string newname = currentLogFile.Replace("__" + sessionLogCount.ToString(), "");

                    sessionLogCount += 1;
                    var nextFile = addLogRotateNumber(newname, sessionLogCount);

                    log.Info("Switching log to the file {0}", nextFile);

                    Log.RemoveListener((FileTraceListener)sender);

                    rename(nextFile, newFile: true);
                }
            });
        }
        
        static string addLogRotateNumber(string fullname, int cnt)
        {
            if (cnt == 0) return fullname;
            var dir = Path.GetDirectoryName(fullname);
            var filename = Path.GetFileNameWithoutExtension(fullname);
            if (Path.HasExtension(fullname))
            {
                var ext = Path.GetExtension(fullname);
                return Path.Combine(dir, filename + "__" + cnt.ToString() + ext);
            }
            else
            {
                return Path.Combine(dir, filename + "__" + cnt.ToString());
            }
        }

        /// <summary>
        /// Renames a previously initialized temporary log file.
        /// </summary>
        public static void Rename(string path)
        {
            try
            {
                rename(path);
            }
            catch (UnauthorizedAccessException e)
            {
                log.Warning("Unable to rename log file to {0} as permissions was denied.", path);
                log.Debug(e);
            }catch (IOException e)
            { // This could also be an error the the user does not have permissions. E.g OpenTAP installed in C:\\
                log.Warning("Unable to rename log file to {0} as the file could not be created.", path);
                log.Debug(e);
            }
        }

        static Task SystemInfoTask;
        private static void SystemInfo()
        {
            if (!String.IsNullOrEmpty(RuntimeInformation.OSDescription))
                log.Debug("{0}{1}", RuntimeInformation.OSDescription, RuntimeInformation.OSArchitecture); // This becomes something like "Microsoft Windows 10.0.14393 X64"

            if (!String.IsNullOrEmpty(RuntimeInformation.FrameworkDescription))
                log.Debug(RuntimeInformation.FrameworkDescription); // This becomes something like ".NET Framework 4.6.1586.0"
            var version = SemanticVersion.Parse(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);
            log.Debug("OpenTAP Engine {0} {1}", version, RuntimeInformation.ProcessArchitecture);
        }
        
        /// <summary>
        /// Flushes the buffered logs. Useful as the last thing to do in case of crash.
        /// </summary>
        public static void Flush()
        {
            if (traceListener != null)
                traceListener.Flush();
        }
    }
}
