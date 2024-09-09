//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Keysight.OpenTap.Sdk.MSBuild
{

    /// <summary>
    /// MSBuild Task to help version assemblies. 
    /// </summary>
    [Serializable]
    public class SetVersionPropertiesFromGit : Task
    {
        /// <summary>
        /// The build directory containing 'tap.exe' and 'OpenTAP.dll'
        /// </summary>
        public string TapDir { get; set; }

        [Output] 
        public string Version { get; set; }
        [Output] 
        public string AssemblyVersion { get; set; }
        [Output] 
        public string InformationalVersion { get; set; }
        [Output] 
        public string FileVersion { get; set; }

        private void SetVersionProperties()
        {
            global::OpenTap.PluginManager.Search();

            var calc = new global::OpenTap.Package.GitVersionCalulator(TapDir);
            var semver = calc.GetVersion();
            Version = semver.ToString(3);
            AssemblyVersion = Version;
            FileVersion = $"{Version}.0";
            InformationalVersion = semver.ToString();
        }

        public override bool Execute()
        {
            using (OpenTapContext.Create(TapDir))
                SetVersionProperties();
            return true;
        }
    }
}
