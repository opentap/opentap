//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenTap.Package
{
    /// <summary>
    /// Uniquely identifies a package in the OpenTAP package system.
    /// </summary>
    public interface IPackageIdentifier
    {
        /// <summary>
        /// The name of the package.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The Semantic Version compliant version of the package. 
        /// </summary>
        SemanticVersion Version { get; }

        /// <summary>
        /// The CPU Architechture of the package. 
        /// </summary>
        CpuArchitecture Architecture { get; }

        /// <summary>
        /// Comma seperated list of operating systems that this package can run on.
        /// </summary>
        string OS { get; }
    }

    public static class IPackageIdentifierExtensions
    {
        /// <summary>
        /// True if this package is compatible (can be installed on) the specified operating system and architecture
        /// </summary>
        /// <param name="pkg"></param>
        /// <param name="selectedArch">Specifies a CPU architecture. If unspecified, the current host architecture is used.</param>
        /// <param name="selectedOS">Specifies an operating system. If null, the current host operating system is used.</param>
        /// <returns></returns>
        public static bool IsPlatformCompatible(this IPackageIdentifier pkg, CpuArchitecture selectedArch = CpuArchitecture.Unspecified, string selectedOS = null)
        {
            var cpu = selectedArch == CpuArchitecture.Unspecified ? ArchitectureHelper.HostArchitecture : selectedArch;
            var os = selectedOS ?? RuntimeInformation.OSDescription;

            if (ArchitectureHelper.CompatibleWith(cpu, pkg.Architecture) == false)
                return false;

            if (IsOsCompatible(pkg,os) == false)
                return false;

            return true;
        }

        private static bool IsOsCompatible(IPackageIdentifier pkg, string os)
        {
            return string.IsNullOrWhiteSpace(pkg.OS) || string.IsNullOrWhiteSpace(os) || pkg.OS.ToLower().Split(',').Any(os.ToLower().Contains) || os.Split(',').Intersect(pkg.OS.Split(','), StringComparer.OrdinalIgnoreCase).Any();
        }

    }

}
