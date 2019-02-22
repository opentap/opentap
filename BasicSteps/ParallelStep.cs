//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Linq;
using System.Reflection;
using System.Threading;

namespace OpenTap.Plugins.BasicSteps
{
    [Display("Parallel", Group: "Flow Control", Description: "Runs its child steps in parallel in separate threads.")]
    [AllowAnyChild]
    [Obfuscation(Exclude = true)] //Dotfuscator seems to create wrong code in this method.
    public class ParallelStep : TestStep
    {
        public override void Run()
        {
            var steps = EnabledChildSteps.ToArray();
            
            SemaphoreSlim sem = new SemaphoreSlim(0);

            Log.Info("Starting {0} child steps in separate threads.", steps.Length);
            foreach(var _step in steps)
            {
                var step = _step;
                TapThread.Start(() =>
                {
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
                    }
                });
            }

            for (int waits = 0; waits < steps.Length; waits++)
                sem.Wait();
            
        }
    }
}
