//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.IO;
using System.Runtime.InteropServices;

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

        static OperatingSystem getCurrent()
        {

            if (Path.DirectorySeparatorChar == '\\')
            {
                return OperatingSystem.Windows;
            }
            else
            {
                if (Directory.Exists("/proc/"))
                {
                    return OperatingSystem.Linux;
                }
                else if (Directory.Exists("/opt")) // If /opt exists and /proc does not we are on mac
                {
                    return OperatingSystem.MacOS;
                }
            }
            return OperatingSystem.Unsupported;
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

    /// <summary> Detection of the specific linux variant. </summary>
    class LinuxVariant
    {
        public string Name { get; }
        public static readonly LinuxVariant Debian = new LinuxVariant("debian");
        public static readonly LinuxVariant Ubuntu = new LinuxVariant("ubuntu");
        public static readonly LinuxVariant RedHat = new LinuxVariant("redhat");
        public static readonly LinuxVariant Unknown = new LinuxVariant("linux-x64");

        static LinuxVariant()
        {
            if (OperatingSystem.Current == OperatingSystem.Linux)
            {
                var os_release_file = new FileInfo("/etc/os-release");
                if (os_release_file.Exists)
                {
                    Current = Unknown;
                    using (var str = new StreamReader(os_release_file.OpenRead()))
                    {
                        
                        string line;
                        while ((line = str.ReadLine()) != null)
                        {
                            if(line.Contains("NAME=\"Debian GNU/Linux\""))
                            {
                                Current = Debian;
                                return;
                            }
                            if(line.Contains("NAME=\"Ubuntu\""))
                            {
                                Current = Ubuntu;
                                return;
                            }
                            
                            if (line.Contains("NAME=\"Red Hat"))
                            {
                                Current = RedHat;
                                return;
                            }

                            if (line.Contains("NAME=\"CentOS Linux\""))
                            {
                                // pretend CentOS is Red Hat for simplicity.
                                Current = RedHat;
                                return;
                            }
                        }
                    }
                }
            }
        }
        
        
        public static LinuxVariant Current { get; }

        public LinuxVariant(string name) => Name = name;

    }
    
}
