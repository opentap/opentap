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
        const int SIGTERM = 15;

        // Keep a list of registered callbacks to ensure they won't be garbage collected
        private static List<SignalCallback> callbacks = new List<SignalCallback>();

        public static event SignalCallback SigTerm
        {
            add
            {
                callbacks.Add(value);
                signal(SIGTERM, value);
            }
            remove { throw new NotSupportedException(); }
        }

        public static event SignalCallback SigInt
        {
            add
            {
                callbacks.Add(value);
                signal(SIGINT, value);
            }
            remove { throw new NotSupportedException(); }
        }

        public delegate void SignalCallback(int sig, int info);

        [DllImport("libc", EntryPoint = "signal")]
        private static extern void signal(int sig, SignalCallback callback);
    }
}