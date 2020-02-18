//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Threading;

namespace OpenTap.Plugins.BasicSteps
{
    [AllowAnyChild]
    [Display("Time Guard", Group:"Basic Steps", Description: "Tries to end the execution of a number of child steps after a specified timeout. This assumes the test steps comply with the thread abort request issued.")]
    public class TimeGuardStep : TestStep
    {
        double timeout = 30;
        [Unit("s")]
        [Display("Timeout", Description: "The timeout to end executing after.")]
        public double Timeout
        {
            get => timeout;
            set
            {
                if (value >= 0)
                    timeout = value;
                else throw new Exception("Timeout must be positive");
            }
        }
        
        [Display("Abort Test Plan On Timeout", Description: "If set the test plan will be aborted instead of continuing to the next step on timeout.", Order: 1)]
        public bool StopOnTimeout { get; set; }

        [Display("Timeout Verdict", Description: "The verdict assigned to this step if a timeout occurs.", Order: 2)]
        public Verdict TimeoutVerdict { get; set; } = Verdict.Error;
        
        public override void Run()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            SemaphoreSlim sem = new SemaphoreSlim(0);
            SemaphoreSlim semStarted = new SemaphoreSlim(0);
            Exception ex = null;
            TapThread thread = TapThread.Start(() =>
            {
                semStarted.Release();
                try
                {
                    RunChildSteps();
                }
                catch (Exception e)
                {
                    ex = e;
                }
                finally
                {
                    sem.Release();
                }
                    
            });

            bool timedOut = false;
            bool isInBreak = false;

            var plan = GetParent<TestPlan>();
            semStarted.Wait();
            while (!sem.Wait(10))
            {
                bool breakStatus = plan.IsInBreak;
                if (isInBreak && !breakStatus)
                    sw.Start();
                else if ((!isInBreak) && breakStatus)
                    sw.Stop();
                isInBreak = breakStatus;

                if (sw.Elapsed.TotalSeconds > this.Timeout && plan.IsInBreak == false)
                {
                    Log.Debug("Timeout occured aborting thread.");
                    timedOut = true;
                    thread.Abort();
                    sem.Wait();
                    break;
                }
            }

            // If timeout occured and we stop on timeout.
            if (timedOut && StopOnTimeout)
                PlanRun.MainThread.Abort();

            if (timedOut)
                Verdict = TimeoutVerdict;
            else if (ex != null) 
                throw ex;
        }
    }
}
