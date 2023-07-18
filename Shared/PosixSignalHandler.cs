using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OpenTap
{
    internal static class PosixSignalHandler
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

        // Currently, we are only interested in handling SIGTERM
        public const int SIGTERM = 15;
        public delegate void SignalCallback(int sig, int info);
        [DllImport("libc", EntryPoint = "signal")]
        private static extern void signal(int sig, SignalCallback callback);

        private static List<SignalCallback> GCHandles = new List<SignalCallback>();
        public static void Register(int s, SignalCallback cb)
        {
            // Explicitly keep at least one reference to the callback. Otherwise, it could be garbage collected after it has been handed off to the unmanaged code.
            GCHandles.Add(cb);
            signal(s, cb);
        }
    }

}
