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

    internal class SharedLibrary {

        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibrary(string filename);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procname);


        [DllImport("libdl.so")]
        static extern IntPtr dlopen(string filename, int flags);

        const int RTLD_GLOBAL = 0x00100;
        delegate IntPtr loadLibType(string name);
        static loadLibType LoadLib;

        static SharedLibrary(){
            
            if(OperatingSystem.Current == OperatingSystem.Linux)
            {
                LoadLib = (name) => dlopen(name, RTLD_GLOBAL);
            }
            else{
                LoadLib = LoadLibrary;
            }

        }

        IntPtr handle;
        public SharedLibrary(IntPtr handle){
            this.handle = handle;
        }
        public static SharedLibrary Load(string name)
        {
            var handle = LoadLib(name);
            if(handle == null) return null;

            return new SharedLibrary(handle);
        }

    }

}
