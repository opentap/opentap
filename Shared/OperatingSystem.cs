//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System.Diagnostics;
using System.IO;

namespace OpenTap
{
    /// <summary> Detects which operating system is used. </summary>
    class OperatingSystem
    {
        public static readonly OperatingSystem Windows = new OperatingSystem(nameof(Windows));
        public static readonly OperatingSystem Linux = new OperatingSystem(nameof(Linux));
        public static readonly OperatingSystem MacOS = new OperatingSystem(nameof(MacOS));
        public static readonly OperatingSystem Unsupported = new OperatingSystem(nameof(Unsupported));
        public override string ToString() => Name;
        public string Name { get; }
        OperatingSystem(string name)
        {
            Name = name;
        }

        private static TraceSource log = Log.CreateSource(nameof(OperatingSystem));
        static OperatingSystem getCurrent()
        {

            if (Path.DirectorySeparatorChar == '\\')
            {
                log.Debug($"OS is windows: Path.DirectorySeparatorChar == '{Path.DirectorySeparatorChar}'");
                return OperatingSystem.Windows;
            }
            else
            {
                if (isMacOs())
                {
                    log.Debug("OS is mac (uname)");
                    return OperatingSystem.MacOS;
                }
                else if (Directory.Exists("/proc/"))
                {
                    log.Debug("Os is Linux: /proc/ exists.");
                    return OperatingSystem.Linux;
                }
            }
            log.Debug("OS not detected.");
            return OperatingSystem.Unsupported;
        }
        
        static bool isMacOs()
        {
            try
            {
                var startInfo = new ProcessStartInfo("uname");
                startInfo.RedirectStandardOutput = true;
                var process = Process.Start(startInfo);
                process.WaitForExit(1000);
                var uname = process.StandardOutput.ReadToEnd();
                return uname.ToLowerInvariant().Contains("darwin");
            }
            catch
            {
                // ignored
            }

            return false;
        }

        static OperatingSystem current;
        public static OperatingSystem Current
        {
            get
            {
                if (current == null)
                {
                    current = getCurrent();
                }
                return current;
            }
        }
    }
    
    class MacOsArchitecture
    {
        public string Architecture { get; }
        public static readonly MacOsArchitecture Intel = new MacOsArchitecture("x64");
        public static readonly MacOsArchitecture Apple = new MacOsArchitecture("arm64");
        public static MacOsArchitecture Current { get; }
        static MacOsArchitecture()
        {
            try
            {
                var startInfo = new ProcessStartInfo("uname", "-m");
                startInfo.RedirectStandardOutput = true;
                var process = Process.Start(startInfo);
                process.WaitForExit(1000);
                var uname = process?.StandardOutput.ReadToEnd();
                Current = uname.Contains("arm64") ? Apple : Intel;
            }
            catch
            {
                // ignored
            }
        }
        public MacOsArchitecture(string architecture) => Architecture = architecture;
    }
    class LinuxArchitecture
    {
        public string Architecture { get; }
        public static readonly LinuxArchitecture x64 = new LinuxArchitecture("x64");
        public static readonly LinuxArchitecture arm = new LinuxArchitecture("arm");
        public static readonly LinuxArchitecture arm64 = new LinuxArchitecture("arm64");
        public static LinuxArchitecture Current { get; }
        static LinuxArchitecture()
        {
            try
            {
                var startInfo = new ProcessStartInfo("uname", "-m");
                startInfo.RedirectStandardOutput = true;
                var process = Process.Start(startInfo);
                process.WaitForExit(1000);
                var uname = process?.StandardOutput.ReadToEnd();
                if (uname.Contains("armv7"))
                    Current = arm;
                else if (uname.Contains("arm64"))
                    Current = arm64;
                else
                    Current = x64;
            }
            catch
            {
                // ignored
            }
        }
        public LinuxArchitecture(string architecture) => Architecture = architecture;
    }
}
