//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Xml.Serialization;

namespace OpenTap.Package
{
    /// <summary>
    /// Uniquely identifies a package in the TAP package system.
    /// </summary>
    public class PackageIdentifier : IPackageIdentifier
    {
        /// <summary>
        /// Name of the package to which this object reffers.
        /// </summary>
        [XmlAttribute]
        public string Name { get; set; }

        /// <summary>
        /// The Semantic Version compliant version of the package. 
        /// </summary>
        [XmlAttribute]
        public SemanticVersion Version { get; set; }

        /// <summary>
        /// CPU Architecture
        /// </summary>
        [XmlAttribute]
        public CpuArchitecture Architecture { get; set; }

        /// <summary>
        /// The operating system that this package supports
        /// </summary>
        [XmlAttribute]
        public string OS { get; set; }

        /// <summary>
        /// Creates a package identifier
        /// </summary>
        /// <param name="packageName">Name of the package.</param>
        /// <param name="version">Version of the package. This should be semver 2.0.0 compliant.</param>
        /// <param name="architecture">CPU architechture supported by the package.</param>
        /// <param name="os">Operating System supported by this package.</param>
        public PackageIdentifier(string packageName, string version, CpuArchitecture architecture, string os)
        {
            Name = packageName;
            if (version != null)
                Version = SemanticVersion.Parse(version);
            Architecture = architecture;
            OS = os;
        }

        /// <summary>
        /// Creates a package identifier
        /// </summary>
        /// <param name="packageName">Name of the package.</param>
        /// <param name="version">Version of the package.</param>
        /// <param name="architecture">CPU architechture supported by the package.</param>
        /// <param name="os">Operating System supported by this package.</param>
        public PackageIdentifier(string packageName, SemanticVersion version, CpuArchitecture architecture, string os)
        {
            Name = packageName;
            Version = version;
            Architecture = architecture;
            OS = os;
        }

        /// <summary>
        /// Creates a package identifier from another <see cref="IPackageIdentifier"/>
        /// </summary>
        public PackageIdentifier(IPackageIdentifier packageIdentifier) : this(packageIdentifier.Name, packageIdentifier.Version, packageIdentifier.Architecture, packageIdentifier.OS)
        {
            
        }

        internal PackageIdentifier()
        {
            
        }
        
        //override GetHashCode and Equals so PackageReference can be properly compared/distincted.
        public override int GetHashCode()
        {
            int hash = 0;
            if (Name != null)
                hash ^= Name.GetHashCode();
            if (Version != null)
                hash ^= Version.GetHashCode();
            if (OS != null)
                hash ^= OS.GetHashCode();
            return hash ^ Architecture.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var other = obj as IPackageIdentifier;
            if (other == null) return false;
            return other.Name == Name && other.Version == Version && other.OS == OS && other.Architecture == Architecture;
        }
    }
}
