//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenTap.Package
{
    /// <summary>
    /// Uniquely identifies a package in the TAP package system.
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
    
}
