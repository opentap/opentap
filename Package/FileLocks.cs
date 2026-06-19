using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace OpenTap
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
            return new PosixFileLock(file);
        }
    }

    /// <summary> Locks a file using flock on linux and mac. This essentially works as a named mutex.  </summary>
    class PosixFileLock : IFileLock
    {
        int fileDescriptor;
        string fileName;
        private readonly ManualResetEvent _waitHandle;

        public PosixFileLock(string file)
        {
            this.fileName = file;
            // Open 'file' in read/write + append mode. If the file does not exist it will be created with the
            // most permissive access settings possible
            fileDescriptor =
                PosixNative.open(file, PosixNative.O_RDONLY | PosixNative.O_APPEND | PosixNative.O_CREAT, PosixNative.ALL_READ_WRITE);

            if (fileDescriptor == -1)
            {
                throw new IOException($"Failed create file lock on {file}: {PosixNative.StrError()}");
            }
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
            try 
            {
                File.Delete(this.fileName);
            }
            catch
            {
                // suppress
            }
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
                    Thread.Sleep(1);
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
        [DllImport("libc", SetLastError = true)]
        public static extern int open(string pathname, int flags, int mode);

        [DllImport("libc", SetLastError = true)]
        public static extern int close(int fd);

        [DllImport("libc", SetLastError = true)]
        public static extern int flock(int fd, int operation);

        [DllImport("libc", EntryPoint = "strerror")] 
        private static extern IntPtr strerror(int errnum);

        public static string StrError() 
        {
            int errno = Marshal.GetLastWin32Error();
            return Marshal.PtrToStringAnsi(strerror(errno)) ?? $"errno {errno}";
        }


        static PosixNative()
        {
            /* MacOS and Linux use different bits for certain file flags.
             * These flags are defined in <fcntl.h>
             * It should be safe to hardcode them because it would be catastrophic
             * for both MacOS and Linux to change them, since it would break every binary in existence. */
            if (OperatingSystem.Current == OperatingSystem.Linux)
            {
                O_CREAT = 0x40; 
                O_APPEND = 0x400;
                O_TRUNC = 0x1000;
            }
            else
            {
                O_CREAT = 0x200; 
                O_APPEND = 0x8;
                O_TRUNC = 0x400;
            }
        }

        public static int O_CREAT;
        public static int O_TRUNC;
        public static int O_APPEND;

        public const int O_RDONLY = 0; //00000000;
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
