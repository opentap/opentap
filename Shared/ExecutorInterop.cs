//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace OpenTap
{
    internal class ExecutorClient
    {
        public static string OpenTapInitDirectory = "OPENTAP_INIT_DIRECTORY";
        /// <summary>
        /// The directory containing the OpenTAP installation.
        /// This is usually the value of the environment variable OPENTAP_INIT_DIRECTORY set by tap.exe
        /// If this value is not set, use the location of OpenTap.dll instead
        /// </summary>
        public static string ExeDir
        {
            get
            {
                var exePath = Environment.GetEnvironmentVariable(OpenTapInitDirectory);
                if (exePath != null)
                    return exePath;

                // Referencing OpenTap.dll causes the file to become locked.
                // Ensure OpenTap.dll is only loaded if the environment variable is not set.
                // This should only happen if OpenTAP was not loaded through tap.exe.
                return GetOpenTapDllLocation();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string GetOpenTapDllLocation() => Path.GetDirectoryName(typeof(PluginManager).Assembly.GetLocation());
    }
}
