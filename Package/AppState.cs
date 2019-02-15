//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    /// <summary>
    /// Used for sharing state information between processes. The data generated here is cleaned up automatically when all the processes uses it stops.
    /// </summary>
    public class SharedState : IDisposable
    {
        protected MemoryMappedFile share;
        private Mutex appstateMutex;

        private string filename;

        public SharedState(string name, string dir)
        {
            if (dir == null)
                throw new ArgumentNullException(nameof(dir));

            filename = Path.GetFullPath(Path.Combine(dir, name)).Replace('\\', '/');

            var hasher = SHA256.Create();
            var hash = hasher.ComputeHash(Encoding.UTF8.GetBytes(filename));

            string mutexName = "OpenTap.Package " + BitConverter.ToString(hash).Replace("-", "");
            appstateMutex = new Mutex(true, mutexName, out bool mutexCreated);
            // if mutexCreated is true, it means that this was the first application to open it. 
            // Mutexes are automatically deleted when no application uses them anymore.
            // when this happens, the state file is cleared by writing 0's to it.

            // For AppState, this means that PID is 0 for the first application using it.
            // the PID is used to block other applications from using it, for example by opening two package managers.

            if (!mutexCreated)
                appstateMutex.WaitOne();

            var stream = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            share = MemoryMappedFile.CreateFromFile(stream, null, 1024, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);

            if (mutexCreated)
                using (var view = share.CreateViewAccessor())
                    view.WriteArray(0, new byte[1024], 0, 1024);

            appstateMutex.ReleaseMutex();
        }

        public void Dispose()
        {
            if (share != null)
                share.Dispose();
            share = null;

            if (appstateMutex != null)
                appstateMutex.Dispose();
            appstateMutex = null;
        }
        protected T Read<T>(long position) where T : struct
        {
            T result = default(T);
            using (var access = share.CreateViewAccessor())
                access.Read(position, out result);
            return result;
        }

        protected void Write<T>(long position, T value) where T : struct
        {
            using (var access = share.CreateViewAccessor())
                access.Write<T>(position, ref value);
        }
    }

    /// <summary>
    /// AppState for the plugin package manager. Used for ensuring that only plugin package manager runs from the same directory.
    /// Also used to focus on type and bringing it to front if someone starts it multiple times.
    /// It relies on the SharedState to be cleaned up automatically by the OS when all plugin package manager applications stops.
    /// </summary>
    public class AppState : SharedState
    {
        public AppState() : base(".plugin_package_manager", Path.GetDirectoryName(typeof(SharedState).Assembly.Location))
        {
        }
        public AppState(string dir) : base(".plugin_package_manager", dir)
        {
        }

        /// <summary>
        /// If Pid != 0 it means that a plugin package manager is running and a new process should not start.
        /// </summary>
        public int Pid
        {
            get { return Read<int>(0); }
            set { Write<int>(0, value); }
        }

        /// <summary>
        /// Gets or sets whether the open PackageManager (GUI) should take focus.
        /// </summary>
        public bool Focus
        {
            get { return Read<bool>(4); }
            set { Write<bool>(4, value); }
        }

        /// <summary> Gets or sets that a given type should be focused in the package manager gui. 
        /// Note, this has to be a string as the assembly is not loaded. </summary>
        public string FocusType
        {
            get
            {
                int length = Read<int>(2 * sizeof(int) + sizeof(bool));
                byte[] data = new byte[length];
                using (var access = share.CreateViewAccessor())
                    access.ReadArray(3 * sizeof(int) + sizeof(bool), data, 0, length);
                return Encoding.UTF8.GetString(data);
            }
            set
            {
                int offset = 2 * sizeof(int) + sizeof(bool);
                var bytes = Encoding.UTF8.GetBytes(value);
                Write(offset, bytes.Length);
                using (var access = share.CreateViewAccessor())
                    access.WriteArray(3 * sizeof(int) + sizeof(bool), bytes, 0, bytes.Length);
            }
        }
    }

    /// <summary>
    /// Maintains a running number that increments whenever a plugin is installed.
    /// </summary>
    public class ChangeId : SharedState
    {
        public ChangeId(string dir) : base(".package_definitions_change_ID", dir)
        {

        }

        public long GetChangeId()
        {
            return Read<long>(0);
        }

        public void SetChangeId(long value)
        {
            Write(0, value);
        }

        public static async Task WaitForChange()
        {
            var changeId = new ChangeId(Path.GetDirectoryName(typeof(SharedState).Assembly.Location));
            var id = changeId.GetChangeId();
            while (changeId.GetChangeId() == id)
                await Task.Delay(500);
        }

        public static void WaitForChangeBlocking()
        {
            var changeId = new ChangeId(Path.GetDirectoryName(typeof(SharedState).Assembly.Location));
            var id = changeId.GetChangeId();
            while (changeId.GetChangeId() == id)
                Thread.Sleep(500);
        }
    }
}
