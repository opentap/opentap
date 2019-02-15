//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace OpenTap.Plugins.BasicSteps
{
    [Display("Parallel", Group:"Flow Control", Description: "Runs its child steps in parallel in separate threads.")]
    [AllowAnyChild]
    [Obfuscation(Exclude = true)] //Dotfuscator seems to create wrong code in this method.
    public class ParallelStep : TestStep
    {
        public override void Run()
        {
            var steps = EnabledChildSteps.ToArray();
            SemaphoreSlim sem = new SemaphoreSlim(0,steps.Length);
            
            Log.Info("Starting {0} child steps in separate threads.", steps.Length);
            Thread[] threads = new Thread[steps.Length];
            object[] locks = new object[steps.Length];
            for(int i = 0; i < steps.Length; i++)
            {
                locks[i] = new object();
                var step = steps[i];
                int j = i;
                TapThread.Start(() =>
                {
                    threads[j] = Thread.CurrentThread;
                    try
                    {
                        RunChildStep(step);
                    }
                    catch
                    {
                        // no need to do anything. This thread will end now 
                    }
                    finally
                    {
                        sem.Release();
                        lock (locks[j])                        
                            threads[j] = null;
                    }
                });
            }

            int waits = 0;
            try
            {
                for (; waits < steps.Length; waits++)
                    sem.Wait();
            }
            catch 
            {
                for(int i = 0; i < threads.Length; i++)
                {
                    lock (locks[i])
                    {
                        var trd = threads[i];
                        if (trd == null) continue;
                        trd.Abort();
                    }
                }
                for (; waits < steps.Length; waits++)
                    sem.Wait();

                throw;
            }
        }
    }
}
