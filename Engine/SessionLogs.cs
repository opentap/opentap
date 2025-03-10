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
using System.Diagnostics;

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
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateHardLink")]
        static extern bool CreateHardLinkWin(
          string lpFileName,
          string lpExistingFileName,
          IntPtr lpSecurityAttributes
        );

        [DllImport("libc", EntryPoint = "link")]
        static unsafe extern bool CreateHardLinkLin(
            char *target,
            char *linkpath
        );

        static unsafe void CreateHardLink(string targetFile, string linkName)
        {
            if (OperatingSystem.Current == OperatingSystem.Windows)
            {
                CreateHardLinkWin(linkName, targetFile, IntPtr.Zero);
            }
            else if (OperatingSystem.Current == OperatingSystem.Linux)
            {
                IntPtr target = Marshal.StringToCoTaskMemAnsi(targetFile);
                IntPtr link = Marshal.StringToCoTaskMemAnsi(linkName);
                CreateHardLinkLin((char*)target, (char*)link);
                Marshal.FreeCoTaskMem(target);
                Marshal.FreeCoTaskMem(link);
            }
            else if (OperatingSystem.Current == OperatingSystem.MacOS)
            {
                Process.Start("ln", $"\"{targetFile}\" \"{linkName}\"");
            }
            else
            {
                // Platform hardlinks not implemented.
            }
        }

        private static readonly TraceSource log = Log.CreateSource("Session");

        /// <summary> The number of files kept at a time. </summary>
        internal const int MaxNumberOfTraceFiles = 20;

        /// <summary> The maximally allowed size of trace files. </summary>
        internal static long MaxTotalSizeOfSessionLogFiles = 2_000_000_000L;

        /// <summary> The maximally allowed size of individual log files.</summary>
        internal static ulong LogFileMaxSize = 100_000_000;

        /// <summary>
        /// If two sessions needs the same log file name, an integer is added to the name. 
        /// This is the max number of times that we are going to test new names.
        /// </summary>
        const int maxNumberOfConcurrentSessions = MaxNumberOfTraceFiles;

        private static FileTraceListener traceListener;

        /// <summary>
        /// File path to the current log file. Path is updated upon UI launch.
        /// </summary>
        public static string GetSessionLogFilePath() => traceListener?.FileName ??  currentLogFile;

        /// <summary>
        /// The current log file base name. This might not be accurate if the log has rolled over due to size.
        /// </summary>
        static string currentLogFile;

        // This controls whether or not session logs should keep files locked
        private static bool NoExclusiveWriteLock = false;

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
        public static void Initialize(string logFileName)
        {
            // We can't just add this as a parameter with a default value because
            // it isn't backwards compatible with plugins compiled against older versions.
            // On the IL level, this would be equivalent to removing this method and adding
            // a new method with a different signature.
            Initialize(logFileName, NoExclusiveWriteLock);
        }
        internal static bool SkipStartupInfo { get; set; }
        /// <summary>
        /// Initializes the logging.
        /// </summary>
        /// <param name="logFileName">The name of the log file</param>
        /// <param name="noExclusiveWriteLock">
        /// Controls whether or not the file should have an exclusive write lock.
        /// If true, the log file may be deleted while it is in use, in which case
        /// session logs will be written into the void.
        /// </param>
        public static void Initialize(string logFileName, bool noExclusiveWriteLock)
        {
            NoExclusiveWriteLock = noExclusiveWriteLock;
            if (currentLogFile == null)
            {
                Rename(logFileName);
                SystemInfoTask = Task.Factory
                    // Ensure that the plugin manager is loaded before running SystemInfo.
                    // this ensures that System.Runtime.InteropServices.RuntimeInformation.dll is loaded. (issue #4000).
                    .StartNew(PluginManager.Load)
                    // then get the system info on a separate thread (it takes ~1s)
                    .ContinueWith(tsk =>
                    {
                        if(!SkipStartupInfo)
                            LogStartupInfo();
                    }); 

                AppDomain.CurrentDomain.ProcessExit += FlushOnExit;
                AppDomain.CurrentDomain.UnhandledException += FlushOnExit;
            }
            else
            {
                if (currentLogFile != logFileName)
                    Rename(logFileName);
            }

            currentLogFile = logFileName;

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

            static RecentFilesList()
            {
                var mutexName = "opentap_recent_logs_mutex";
                const int retries = 5;
                Exception ex = null;
                
                for (int i = 0; i < retries; i++)
                {
                    try
                    {
                        recentLock = new Mutex(false, mutexName);
                        break;
                    }
                    catch (Exception e)
                    {
                        ex = e;
                        Thread.Sleep(10);
                    }
                }

                if (recentLock == null)
                    log.Debug($"Error creating mutex '{mutexName}': {ex.Message}"); 
            }

            private static readonly Mutex recentLock;
            private static bool recentFilesValid => recentLock != null;
            
            public string[] GetRecent()
            {
                if (!recentFilesValid) 
                    return Array.Empty<string>();

                try
                {
                    recentLock.WaitOne();
                }
                catch (AbandonedMutexException)
                {
                    // recover from abandoned mutex
                }
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
                // Don't write to the file if we can't get the mutex
                if (!recentFilesValid) 
                    return;
                
                // Important to lock the file and to re-read if the file was changed since last checked.
                // otherwise there is a risk that a log file will be forgotten and never cleaned up.
                try
                {
                    recentLock.WaitOne();
                }
                catch (AbandonedMutexException)
                {
                    // recover from abandoned mutex
                }
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
        
        static readonly RecentFilesList recentSystemLogs = new RecentFilesList(getLogRecentFilesName());

        static void RemoveOldLogFiles()
        {
            List<string> recentFiles = recentSystemLogs.GetRecent().Where(File.Exists).ToList();

            long CalcTotalSize()
            {
                return recentFiles.Select(x => new FileInfo(x).Length).Sum();
            }
            
            bool ConditionViolated()
            {
                bool tooManyFiles = recentFiles.Count > MaxNumberOfTraceFiles;
                if (tooManyFiles) return true;

                if (recentFiles.Count <= 2) return false; // Do not remove the last couple of log files, event though they might exceed limits.
                
                var totalSize = CalcTotalSize();
                bool filesTooBig = totalSize > MaxTotalSizeOfSessionLogFiles;
                if (filesTooBig) return true;
                
                return false;
            }

            int ridx = 0;
            while (ConditionViolated() && ridx < recentFiles.Count)
            {
                try
                {
                    if (File.Exists(recentFiles[ridx]))
                    {
                        log.Debug("Deleting log file: {0}", recentFiles[ridx]);
                        File.Delete(recentFiles[ridx]);
                    }
                    recentFiles.RemoveAt(ridx);
                }
                catch (Exception)
                {
                    ridx++;
                }
            }
        }
        
        /// <summary>
        /// Renames a previously initialized temporary log file.
        /// </summary>
        static void rename(string path, bool newLogFile = false)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            
            string name = Path.GetFileNameWithoutExtension(path);
            string dir = Path.GetDirectoryName(path);
            string ext = Path.GetExtension(path);
            bool fileNameChanged = false;
            for (int idx = 0; idx < maxNumberOfConcurrentSessions; idx++)
            {
                try
                {
                    path = Path.Combine(dir, name + (idx == 0 ? "" : idx.ToString()) + ext);
                    if (traceListener == null)
                    {
                        if (string.IsNullOrWhiteSpace(dir) == false)
                            Directory.CreateDirectory(Path.GetDirectoryName(path));
                        if (NoExclusiveWriteLock)
                        {
                            // Initialize a stream where the underlying file can be deleted. If the file is deleted, writes just go into the void.
                            var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read | FileShare.Delete);
                            traceListener = new FileTraceListener(stream);
                        }
                        else
                        {
                            traceListener = new FileTraceListener(path);
                        }
                        
                        traceListener.FileSizeLimit = LogFileMaxSize;
                        traceListener.FileSizeLimitReached += TraceListener_FileSizeLimitReached;
                        Log.AddListener(traceListener);
                    }
                    else
                    {
                        traceListener.ChangeFileName(path, NoExclusiveWriteLock, startNewLog: newLogFile);
                        traceListener.FileSizeLimit = LogFileMaxSize;
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
                recentSystemLogs.AddRecent(Path.GetFullPath(path));
            }

            MakeLatest(path);
            RemoveOldLogFiles();
        }

        static void MakeLatest(string path)
        {
            // Create a hardlink to a file called 'Latest.txt' next to the file specified by 'path'
            try
            {
                var latestPath = Path.Combine(Path.GetDirectoryName(path), "Latest.txt");
                if (File.Exists(latestPath))
                    File.Delete(latestPath);
                CreateHardLink(path, latestPath);
            }
            catch
            {
                // Ignore in case of race conditions.
            }
        }

        static int sessionLogCount = 0;
        static readonly object sessionLogRotateLock = new object();
        static void TraceListener_FileSizeLimitReached(object sender, EventArgs e)
        {
            lock (sessionLogRotateLock)
            {
                var newName = currentLogFile.Replace("__" + sessionLogCount, "");
                sessionLogCount += 1;
                var nextFile = addLogRotateNumber(newName, sessionLogCount);
                ((FileTraceListener)sender).ChangeFileName(nextFile, false, true);
                
                log.Info("Switching log to file {0}", nextFile);
                recentSystemLogs.AddRecent(Path.GetFullPath(nextFile));
                MakeLatest(nextFile);
                RemoveOldLogFiles();
            }
            
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
        public static void Rename(string path) => Rename(path, false);
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="newLogFile">If a new log file should be created or we should rename the existing one.</param>
        internal static void Rename(string path, bool newLogFile)
        {
            try
            {
                rename(path, newLogFile: newLogFile);
            }
            catch (UnauthorizedAccessException e)
            {
                log.Warning("Unable to rename log file to {0} as permissions was denied.", path);
                log.Debug(e);
            }
            catch (IOException e)
            { // This could also be an error the the user does not have permissions. E.g OpenTAP installed in C:\\
                log.Warning("Unable to rename log file to {0} as the file could not be created.", path);
                log.Debug(e);
            }
        }

        static Task SystemInfoTask;
        private static void LogStartupInfo()
        {
            TapThread.Sleep(200);
            foreach (var td in TypeData.GetDerivedTypes<IStartupInfo>().Where(td => td.CanCreateInstance))
            {
                IStartupInfo si = null;
                try 
                {
                    si = td.CreateInstance() as IStartupInfo;
                    if (si == null)
                    {
                        log.Debug($"Failed to instantiate '{td.Name}'.");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    log.Debug($"Failed to instantiate '{td.Name}': {ex.Message}");
                    log.Debug(ex);
                    continue;
                }

                try
                {
                    si.LogStartupInfo();
                }
                catch (Exception ex)
                {
                    log.Debug($"Unhandled exception in '{td.Name}.{nameof(si.LogStartupInfo)}': {ex.Message}");
                    log.Debug(ex);
                }
            }
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
