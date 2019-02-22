//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.ComponentModel;
using System.Threading;
using System.Xml.Serialization;
using OpenTap;

namespace OpenTap.Plugins.BasicSteps
{
    [AllowAnyChild]
    [Display("Time Guard", Group:"Basic Steps", Description: "Tries to forcibly end the execution of a number of child steps after a specified timeout. This is not guaranteed to happen depending on the construction of the child steps.")]
    public class TimeGuardStep : TestStep
    {
        double timeout = 30;
        [Unit("s")]
        [Display("Timeout", Description: "The timeout to end executing after.")]
        public double Timeout
        {
            get { return timeout; }
            set
            {
                if (value >= 0)
                    timeout = value;
                else throw new Exception("Timeout must be positive");
            }
        }
        
        [Display("Stop Test Plan On Timeout", Description: "If set the test plan will stop instead of continuing to the next step on timeout.", Order: 1)]
        public bool StopOnTimeout { get; set; }
        
        public override void Run()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Exception exception = null;
            SemaphoreSlim sem = new SemaphoreSlim(0);
            SemaphoreSlim semStarted = new SemaphoreSlim(0);
            TapThread thread = TapThread.Start(() =>
            {
                semStarted.Release();
                try
                {
                    RunChildSteps();
                }
                catch (Exception e)
                {
                    exception = e;
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
                    timedOut = true;
                    thread.Abort();
                    sem.Wait();
                    break;
                }
            }
            

            // If timeout occured and we stop on timeout.
            if (timedOut && StopOnTimeout)
            {
                throw new OperationCanceledException("The Time Guard step reached the timeout and aborted the test plan.");
            }
            // If time out did not occur, but an exception was thrown inside one of the child steps.
            if (timedOut == false && exception != null)
            {
                if (exception is OperationCanceledException)
                {
                    UpgradeVerdict(Verdict.Aborted);
                    throw new OperationCanceledException("A step inside the Time Guard was aborted.");
                }
                else
                    throw new AggregateException(exception);
            }
        }

    }
}
