using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace OpenTap.Package.PackageInstallHelpers
{
    internal interface IFileLock : IDisposable
    {
        bool WaitOne();
        bool WaitOne(TimeSpan timeout);
        bool WaitOne(int ms);
        WaitHandle WaitHandle { get; }
        void Release();
    }

    internal static class FileLock
    {
        public static IFileLock Create(string file)
        {
            if (OperatingSystem.Current == OperatingSystem.Windows) return new Win32FileLock(file);
            if (OperatingSystem.Current == OperatingSystem.MacOS) return new MacOSFileLock(file);
            return new PosixFileLock(file);
        }
    }

    /// <summary>
    /// Note that this implementation is not thread-safe, unlike the other implementations
    /// </summary>
    class MacOSFileLock : IFileLock
    {
        private readonly ManualResetEvent _waitHandle;
        private readonly string name;

        public MacOSFileLock(string file)
        {
            _waitHandle = new ManualResetEvent(false);
            name = file;
        }

        public void Dispose()
        {
            Release();
        }

        public bool WaitOne()
        {
            while (true)
            {
                // Keep retrying waiting with a timeout until it succeeds
                if (WaitOne(1000)) return true;
            }
        }

        public bool WaitOne(TimeSpan timeout)
        {
            // If the fileLock is not null, we are already holding this mutex.
            if (fileLock != null) return true;
            var sw = Stopwatch.StartNew();
            do
            {
                // File exists -- the named mutex is locked
                if (File.Exists(name))
                {
                    var remaining = timeout - sw.Elapsed;
                    if (remaining.TotalMilliseconds > 1)
                        TapThread.Sleep(1);
                    else Thread.Yield();
                }
                // Otherwise, create the file, thereby claiming the mutex
                else
                {
                    fileLock = File.Create(name, 0, FileOptions.DeleteOnClose);
                    _waitHandle.Set();
                    return true;
                }
            } while (sw.Elapsed < timeout);

            return false;
        }

        public bool WaitOne(int ms)
        {
            return WaitOne(TimeSpan.FromMilliseconds(ms));
        }

        public WaitHandle WaitHandle => _waitHandle;
        public FileStream fileLock { get; set; }

        public void Release()
        {
            try
            {
                fileLock.Dispose();
                fileLock = null;
                _waitHandle.Reset();
            }
            catch
            {
                // this is okay
            }
        }
    }

    /// <summary> Locks a file using flock on linux. This essentially works as a named mutex.  </summary>
    class PosixFileLock : IFileLock
    {
        int fileDescriptor;
        private readonly ManualResetEvent _waitHandle;

        public PosixFileLock(string file)
        {
            // Open 'file' in read/write + append mode. If the file does not exist it will be created with the
            // most permissive access settings possible
            fileDescriptor =
                PosixNative.open(file, PosixNative.O_RDWR | PosixNative.O_APPEND | PosixNative.O_CREAT, PosixNative.ALL_READ_WRITE);
            if (fileDescriptor == -1) throw new IOException($"Failed create file lock on {file}");
            _waitHandle = new ManualResetEvent(false);
        }

        /// <summary>
        /// Request an exclusive lock on the open file handle
        /// This call wil block until the lock is acquired
        /// </summary>
        private void Take()
        {
            PosixNative.flock(fileDescriptor, PosixNative.LOCK_EX);
        }

        public void Release()
        {
            if (fileDescriptor >= 0)
            {
                PosixNative.flock(fileDescriptor, PosixNative.LOCK_UN);
                _waitHandle.Reset();
            }
        }

        public void Dispose()
        {
            if (fileDescriptor >= 0 && _waitHandle.WaitOne(0))
            {
                Release();
            }

            PosixNative.close(fileDescriptor);
            fileDescriptor = -1;
        }

        public bool WaitOne()
        {
            Take();
            _waitHandle.Set();
            return true;
        }

        public bool WaitOne(TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            do
            {
                var @lock = PosixNative.flock(fileDescriptor, PosixNative.LOCK_NB | PosixNative.LOCK_EX);
                if (@lock == 0)
                {
                    _waitHandle.Set();
                    return true;
                }

                var remaining = timeout - sw.Elapsed;
                if (remaining.TotalMilliseconds > 1)
                    TapThread.Sleep(1);
                else Thread.Yield();
            } while (sw.Elapsed < timeout);

            return false;
        }

        public bool WaitOne(int ms) => WaitOne(TimeSpan.FromMilliseconds(ms));
        public WaitHandle WaitHandle => _waitHandle;
    }

    class Win32FileLock : IFileLock
    {
        private Mutex _mutex;

        public Win32FileLock(string name)
        {
            // Having backslashes in the mutex name seems to cause issues for some reason. Replace them with slashes.
            _mutex = new Mutex(false, name.Replace("\\", "/") + "_opentap_named_mutex_");
        }

        public void Dispose()
        {
            try
            {
                if (_mutex?.WaitOne(0) == true)
                    _mutex?.ReleaseMutex();
            }
            catch (AbandonedMutexException)
            {
                // this is fine
            }

            _mutex?.Dispose();
            _mutex = null;
        }

        public bool WaitOne() => _mutex.WaitOne();
        public bool WaitOne(TimeSpan timeout) => _mutex.WaitOne(timeout);
        public bool WaitOne(int ms) => _mutex.WaitOne(ms);

        public WaitHandle WaitHandle => _mutex;
        public void Release() => _mutex.ReleaseMutex();
    }

    static class PosixNative
    {
        [DllImport("libc")]
        public static extern int open(string pathname, int flags, int mode);

        [DllImport("libc")]
        public static extern int close(int fd);

        [DllImport("libc")]
        public static extern int flock(int fd, int operation);

        public const int O_CREAT = 64; //00000100;
        public const int O_TRUNC = 512; //00001000;
        public const int O_APPEND = 1024; //00002000;
        public const int O_RDWR = 2; //00000002;
        /// <summary>
        /// Place a shared lock. More than one process may hold a shared lock for a given file at a given time. 
        /// </summary>
        public const int LOCK_SH = 1;
        /// <summary>
        /// Place an exclusive lock. Only one process may hold an exclusive lock for a given file at a given time. 
        /// </summary>
        public const int LOCK_EX = 2;
        /// <summary>
        /// Return an error instead of blocking when the lock is taken
        /// </summary>
        public const int LOCK_NB = 4;
        /// <summary>
        /// Release the lock
        /// </summary>
        public const int LOCK_UN = 8;

        public const int S_IRUSR = 256; //00000400
        public const int S_IWUSR = 128; //00000200

        public const int S_IRGRP = 32; //00000040
        public const int S_IWGRP = 16; //00000020

        public const int S_IROTH = 4; //00000004
        public const int S_IWOTH = 2; //00000002

        public const int ALL_READ_WRITE = S_IRUSR | S_IWUSR | S_IRGRP | S_IWGRP | S_IROTH | S_IWOTH;
    }
}