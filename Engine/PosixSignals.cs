using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OpenTap
{
    internal static class PosixSignals
    {
        // Signal implementation and numbering can vary between POSIX systems. I have checked some of the major resources, and at least SIGTERM seems to be consistent.

        // GNU Linux: see the section 'Signal numbering for standard signals'
        // https://www.man7.org/linux/man-pages/man7/signal.7.html

        // MacOS
        // https://opensource.apple.com/source/xnu/xnu-344/bsd/sys/signal.h

        // FreeBSD:
        // https://raw.githubusercontent.com/freebsd/freebsd-src/master/sys/sys/signal.h

        // Fedora: (glibc 2.37 at least)
        // https://elixir.bootlin.com/glibc/glibc-2.37/source/bits/signum-generic.h#L53

        // Currently, we are only interested in handling SIGTERM and SIGINT
        const int SIGINT = 2;
        const int SIGALARM = 14;
        const int SIGTERM = 15;

        private static readonly TimeSpan ShutdownGracePeriod = TimeSpan.FromSeconds(2);
        private static SignalCallback OnSigInt = (_, _) => { };
        private static SignalCallback OnSigTerm = (_, _) => { };

        private static void OnSigAlarm(int sig, int info)
        {
            Console.WriteLine($"Application did not respond to previous signal. Shutting down forcefully.");
            Environment.Exit(SIGALARM); 
        }
        private static void OnSignal(int sig, int info)
        {
            // re-enable this signal.
            // Signals are disabled when they are raised, so subsequent signals will not be caught otherwise.
            signal(sig, OnSignal);
            
            if (sig == SIGTERM)
            {
                OnSigTerm(sig, info);
            }
            else
            {
                OnSigInt(sig, info);
            }
            
            {
                // trigger an alarm if the process hasn't exited within a reasonable amount of time
                signal(SIGALARM, OnSigAlarm);
                alarm((uint)ShutdownGracePeriod.Seconds);
            } 
        }

        internal static void SetSigTerm(SignalCallback callback)
        {
            OnSigTerm = callback;
            signal(SIGTERM, OnSignal);
        }
        internal static void SetSigInt(SignalCallback callback)
        {
            OnSigInt = callback;
            signal(SIGINT, OnSignal);
        }

        public delegate void SignalCallback(int sig, int info);

        [DllImport("libc", EntryPoint = "alarm")]
        private static extern void alarm(uint seconds);
         
        [DllImport("libc", EntryPoint = "signal")]
        private static extern void signal(int sig, SignalCallback callback);
    }
}