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

        /// <summary>
        /// the number of files kept at a time.
        /// </summary>
        const int maxNumberOfTraceFiles = 10;

        /// <summary>
        /// If two sessions needs the same log file name, an integer is added to the name. 
        /// This is the max number of times that we are going to test new names.
        /// </summary>
        const int maxNumberOfConcurrentSessions = 10;

        private static FileTraceListener traceListener;

        /// <summary>
        /// File path to the current log file. Path is updated upon UI launch.
        /// </summary>
        public static string GetSessionLogFilePath()
        {
            return CurrentLogFile;
        }

        private static string CurrentLogFile;

        /// <summary>
        /// Initializes the logging. Uses the following file name formatting: SessionLogs\\[Application Name]\\[Application Name] [yyyy-MM-dd HH-mm-ss].txt.
        /// </summary>
        public static void Initialize()
        {
            if (CurrentLogFile != null) return;
            var timestamp = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToString("yyyy-MM-dd HH-mm-ss");

            // Path example: <TapDir>/SessionLogs/SessionLog <timestamp>.txt
            string pathEnding = $"SessionLog {timestamp}";

            if (Assembly.GetEntryAssembly() != null && !String.IsNullOrWhiteSpace(Assembly.GetEntryAssembly().Location))
            {
                string exeName = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
                // Path example: <TapDir>/SessionLogs/tap/tap <timestamp>.txt
                pathEnding = $"{exeName}/{exeName} {timestamp}";
            }

            Initialize($"{FileSystemHelper.GetCurrentInstallationDirectory()}/SessionLogs/{pathEnding}.txt");
        }
        
        /// <summary>
        /// Initializes the logging. 
        /// </summary>
        public static void Initialize(string tempLogFileName)
        {
            if (CurrentLogFile == null)
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
                if (CurrentLogFile != tempLogFileName)
                    Rename(tempLogFileName);
            }

            CurrentLogFile = tempLogFileName;
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
        /// Class for managing a hidden file of lines.
        /// </summary>
        class LineFile
        {
            string name;
            public LineFile(string filename)
            {
                if (Assembly.GetEntryAssembly() != null)
                    this.name = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), filename);
                else
                    this.name = filename;

            }
            void waitForFileUnlock()
            {
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        using (File.Open(name, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)) { }
                        break;
                    }
                    catch (Exception)
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                }
            }

            void setHidden(string name)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    File.SetAttributes(name, FileAttributes.Hidden | FileAttributes.Archive);
            }

            void ensureFileExists()
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

                    }
                }
                waitForFileUnlock();
            }

            public List<string> GetRecent()
            {
                ensureFileExists();
                return File.ReadAllLines(name).ToList();
            }

            public void SetRecent(List<string> names)
            {
                ensureFileExists();
                using (var f = File.Open(name, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    f.SetLength(0);
                    using (var sw = new StreamWriter(f))
                    {
                        names.ForEach(sw.WriteLine);
                    }
                }
            }
        }

        static LineFile recentSystemlogs = new LineFile(".recent_logs");

        /// <summary>
        /// Renames a previously initialized temporary log file.
        /// </summary>
        static void rename(string path, bool newFile = false)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var recentFiles = recentSystemlogs.GetRecent().Where(File.Exists).ToList();

            int ridx = 0;
            while ((recentFiles.Count + 1) > maxNumberOfTraceFiles && ridx < recentFiles.Count)
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
                log.Debug("Unable to rename log file. Continuing with log '{0}'.", CurrentLogFile);
            }
            else
            {
                CurrentLogFile = path;
                recentFiles.Add(path);
                log.Debug(sw, "Session log loaded as '{0}'.", CurrentLogFile);
            }

            recentSystemlogs.SetRecent(recentFiles.DistinctLast().Where(File.Exists).ToList());
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
                    string newname = CurrentLogFile.Replace("__" + sessionLogCount.ToString(), "");

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
