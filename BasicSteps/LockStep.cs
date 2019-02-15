//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Threading;
using OpenTap;

namespace OpenTap.Plugins.BasicSteps
{
    [Display("Lock", Group: "Flow Control", Description: "Locks the execution of child steps based on a specified mutex.")]
    [AllowAnyChild]
    public class LockStep : TestStep
    {
        public LockStep()
        {
            LockName = "TAP_Mutex1";
            SystemWide = true;
            Rules.Add(() => LockName.Contains("\\") == false, "Lock Name does not support '\\'", "LockName");
        }
        [Display("Lock Name", Description: "The name identifying the lock, used for sharing.")]
        public string LockName { get; set; }
        
        [Display("System Wide", Description: "Use a system-wide named mutex, that can be shared across processes.")]
        public bool SystemWide { get; set; }
        static ConcurrentDictionary<string, Mutex> localMutexes = new ConcurrentDictionary<string, Mutex>();

        Mutex getMutex(string name, bool systemwide)
        {
            if (systemwide)
                return new Mutex(false, name);
            return localMutexes.GetOrAdd(name, s => new Mutex());
        }

        public override void Run()
        {
            Mutex mutex = getMutex(LockName, SystemWide);
            mutex.WaitOne();
            try
            {
                RunChildSteps();
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    }
}
