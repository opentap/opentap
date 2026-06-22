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
        bool WaitOne(TimeSpan timeout, CancellationToken cancellationToken);
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

            // Ensure the lock file exists with sensible permissions before opening it with libc.
            //
            // We deliberately do NOT pass O_CREAT (and a mode) to the libc open() below: open() is a
            // variadic function (int open(const char*, int, ...)) and the mode is a variadic argument.
            // On some ABIs (notably Apple ARM64) variadic arguments are passed on the stack while the
            // p/invoke marshaller passes the mode in a register, so the kernel reads a garbage mode.
            // This previously created the lock file with broken permissions (e.g. 0055, no owner access),
            // after which any subsequent open() of the same file failed with EACCES. Creating the file
            // up front via .NET avoids relying on open()'s variadic mode argument entirely.
            if (!File.Exists(file))
            {
                try
                {
                    using (File.Open(file, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite)) { }
                }
                catch (IOException)
                {
                    // Another thread/process created it first - that is fine.
                }
            }

            // Open an existing file (no O_CREAT) purely to obtain a descriptor to flock on.
            fileDescriptor = PosixNative.Open(file);

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
            int status = PosixNative.flock(fileDescriptor, PosixNative.LOCK_EX);
            if (status != 0) throw new Exception($"Failed to lock {fileName}: {PosixNative.StrError()}");
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

        public bool WaitOne(TimeSpan timeout) => WaitOne(timeout, CancellationToken.None);

        public bool WaitOne(TimeSpan timeout, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                var @lock = PosixNative.flock(fileDescriptor, PosixNative.LOCK_NB | PosixNative.LOCK_EX);
                if (@lock == 0)
                {
                    _waitHandle.Set();
                    return true;
                }

                var remaining = timeout - sw.Elapsed;
                if (remaining.TotalMilliseconds > 1)
                {
                    double sleep_ms = Math.Min(remaining.TotalMilliseconds, 5);
                    // Sleep, but wake up immediately if cancellation is requested.
                    if (cancellationToken.WaitHandle.WaitOne((int)sleep_ms))
                        cancellationToken.ThrowIfCancellationRequested();
                }
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

        public bool WaitOne(TimeSpan timeout, CancellationToken cancellationToken)
        {
            switch (WaitHandle.WaitAny([_mutex, cancellationToken.WaitHandle], timeout))
            {
                case 0: // Acquired the mutex.
                    return true;
                case 1: // Cancellation token was signaled.
                    cancellationToken.ThrowIfCancellationRequested();
                    return false;
                default: // WaitHandle.WaitTimeout
                    return false;
            }
        }

        public WaitHandle WaitHandle => _mutex;
        public void Release() => _mutex.ReleaseMutex();
    }

    static class PosixNative
    {
        // Note: open() is variadic - 'int open(const char*, int oflag, ...)' - where the file mode is a
        // variadic argument only used with O_CREAT. We never pass O_CREAT (the lock file is created via
        // .NET beforehand), so we declare the simple two-argument form and never pass a mode. Declaring a
        // fixed third 'mode' argument is unsafe on ABIs that pass variadic arguments differently from
        // fixed ones (e.g. Apple ARM64), where it results in a garbage mode being applied to the file.
        [DllImport("libc", SetLastError = true)]
        private static extern int open(string pathname, int flags);

        public static int Open(string pathname) => open(pathname, O_RDONLY | O_APPEND);

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
