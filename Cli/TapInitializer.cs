//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using OpenTap.Diagnostic;

namespace OpenTap
{
    internal static class TapInitializer
    {
        internal static bool CanAcquireInstallationLock()
        {
            // Check if the lock for the current installation can be acquired
            var target = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var lockfile = Path.Combine(target, ".lock");
            FileSystemHelper.EnsureDirectoryOf(lockfile);
            const int limit = 30;

            for (int i = 0; i < limit; i++)
            {
                using var fileLock = FileLock.Create(lockfile);
                try
                {
                    if (fileLock.WaitOne(TimeSpan.FromSeconds(1)))
                        return true;
                }
                catch (AbandonedMutexException)
                {
                    // ignore -- this happens if the mutex is disposed in another process.
                    // In this case we will most likely acquire the lock in the next iteration.
                    continue;
                }
                Console.WriteLine($"{target} is locked by a locking package action. Waiting for it to become unlocked... ({i + 1} / {limit})");
            }

            return false;
        }

        public class InitTraceListener : ILogListener {
            public readonly List<Event> AllEvents = new List<Event>();
            public void EventsLogged(IEnumerable<Event> events)
            {
                lock(AllEvents)
                    AllEvents.AddRange(events);
            }
            public void Flush(){

            }
            public static readonly InitTraceListener Instance = new InitTraceListener();  
        }

        static string OpenTapLocation
        {
            get
            {
                var loc = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                return Path.Combine(loc, "OpenTap.dll");
            }
        }

        internal static void Initialize()
        {
            // This current assembly looks for the opentap DLL in the wrong location.
            // we know that we are going to load it, so let's just load it as the first thing.
            if(File.Exists(OpenTapLocation))
                Assembly.LoadFrom(OpenTapLocation);
            
            ContinueInitialization();
        }

        internal static void ContinueInitialization()
        {
            // We only needed the resolver to get into this method (requires OpenTAP, which requires netstandard)
            // Remove so we avoid race condition with OpenTap AssemblyResolver.
            OpenTap.Log.AddListener(InitTraceListener.Instance);
            PluginManager.Search();
            OpenTap.Log.RemoveListener(InitTraceListener.Instance);
        }
    }

}
