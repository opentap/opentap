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

namespace OpenTap.Package.Ipc
{
    /// <summary>
    /// Used for sharing state information between processes. The data generated here is cleaned up automatically when all the processes uses it stops.
    /// </summary>
    internal class SharedState : IDisposable
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
    
}
