//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using OpenTap;
using System;
using System.Diagnostics;
using System.Reflection;

namespace OpenTap
{
    /// <summary>
    /// This is used as a way to also check the license for the user.
    /// </summary>
    class VersionChecker
    {
        static OpenTap.TraceSource log = OpenTap.Log.CreateSource("OpenTAP");
        internal static string EmitVersion(string title)
        {
            //check whether OpenTAP application is 32-bit or 64-bit
            string tapPlatform = Environment.Is64BitProcess ? "64-bit" : "32-bit";
            var tapVersion = PluginManager.GetOpenTapAssembly().SemanticVersion;
            var initt = DateTime.Now - Process.GetCurrentProcess().StartTime;
            log.Info(initt, "{0} version '{1}' {2} initialized {3}", title, tapVersion, tapPlatform, DateTime.Now.ToString("MM/dd/yyyy"));
            if (tapVersion.PreRelease != null && tapVersion.PreRelease.StartsWith("alpha"))
                return tapVersion.ToString(5);
            return tapVersion.ToString(4);
        }
    }
}
