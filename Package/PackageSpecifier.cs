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
    /// Holds search parameters that specifies a range of packages in the OpenTAP package system.
    /// </summary>
    public class PackageSpecifier
    {
        /// <summary>
        /// Search for parameters that specifies a range of packages in the OpenTAP package system. Unset parameters will be treated as 'any'.
        /// </summary>
        public PackageSpecifier(string name = null, VersionSpecifier version = default(VersionSpecifier), CpuArchitecture architecture = CpuArchitecture.Unspecified, string os = null)
        {
            Name = name;
            Version = version ?? VersionSpecifier.Any;
            Architecture = architecture;
            OS = os;
        }

        /// <summary>
        /// Search parameters that specify an exact or a version compatible match to the given package/identifier.
        /// </summary>
        public PackageSpecifier(IPackageIdentifier package, VersionMatchBehavior versionMatchBehavior = VersionMatchBehavior.Exact) 
            : this(package.Name, new VersionSpecifier(package.Version, versionMatchBehavior),package.Architecture, package.OS)
        {
        }

        /// <summary>
        /// The name of the package. Can be null to indicate "any name".
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Specifying requirements to the version the package. Never null.
        /// </summary>
        public VersionSpecifier Version { get; }

        /// <summary>
        /// The CPU Architechture of the package. 
        /// </summary>
        public CpuArchitecture Architecture { get; }

        /// <summary>
        /// Comma seperated list of operating systems that this package can run on.
        /// </summary>
        public string OS { get; }
    }

    /// <summary>
    /// Specifies parts of a semantic version. This is used in <see cref="PackageSpecifier"/> to represent the part of a <see cref="SemanticVersion"/> to search for.
    /// E.g. the VersionSpecifier "9.0" may match the semantic version "9.0.4+abcdef" and also "9.1.x" if <see cref="MatchBehavior"/> is set to "Compatible".
    /// </summary>
    public class VersionSpecifier
    {
        /// <summary>
        /// The VersionSpecifier that will match any version. VersionSpecifier.Any.IsCompatible always returns true.
        /// </summary>
        public static readonly VersionSpecifier Any = new VersionSpecifier(null, null, null, null, null, VersionMatchBehavior.Compatible | VersionMatchBehavior.AnyPrerelease);

        /// <summary>
        /// Major version. When not null, <see cref="IsCompatible"/> will return false for <see cref="SemanticVersion"/>s with a Major version different from this.
        /// </summary>
        public readonly int? Major;
        /// <summary>
        /// Minor version. When not null, <see cref="IsCompatible"/> will return false for <see cref="SemanticVersion"/>s with a Minor version less than this (with <see cref="VersionMatchBehavior.Compatible"/>) or different from this (with <see cref="VersionMatchBehavior.Exact"/>).
        /// </summary>
        public readonly int? Minor;
        /// <summary>
        /// Patch version. When not null, <see cref="IsCompatible"/> will return false for <see cref="SemanticVersion"/>s with a Patch version different from this if <see cref="MatchBehavior"/> is <see cref="VersionMatchBehavior.Exact"/>.
        /// </summary>
        public readonly int? Patch;
        /// <summary>
        /// PreRelease identifier. <see cref="IsCompatible"/> will return false for <see cref="SemanticVersion"/>s with a PreRelease less than this (with <see cref="VersionMatchBehavior.Compatible"/>) or different from this (with <see cref="VersionMatchBehavior.Exact"/>).
        /// </summary>
        public readonly string PreRelease;
        /// <summary>
        /// BuildMetadata identifier. When not null, <see cref="IsCompatible"/> will return false for <see cref="SemanticVersion"/>s with a BuildMetadata different from this if <see cref="MatchBehavior"/> is <see cref="VersionMatchBehavior.Exact"/>.
        /// </summary>
        public readonly string BuildMetadata;

        /// <summary>
        /// The way matching is done. This affects the behavior of <see cref="IsCompatible(SemanticVersion)"/>.
        /// </summary>
        public readonly VersionMatchBehavior MatchBehavior;
        /// <summary>
        /// Specifies parts of a semantic version. Unset parameters will be treated as 'any'.
        /// </summary>
        public VersionSpecifier(int? major, int? minor, int? patch, string prerelease, string buildMetadata, VersionMatchBehavior matchBehavior)
        {
            if (major == null && minor != null)
                throw new ArgumentException();
            if (minor == null && patch != null)
                throw new ArgumentException();
            Major = major;
            Minor = minor;
            Patch = patch;
            PreRelease = prerelease;
            BuildMetadata = buildMetadata;
            MatchBehavior = matchBehavior;
        }

        /// <summary>
        /// Creates a VersionSpecifier from a <see cref="SemanticVersion"/>.
        /// </summary>
        public VersionSpecifier(SemanticVersion ver, VersionMatchBehavior matchBehavior) : this(ver?.Major, ver?.Minor, ver?.Patch, ver?.PreRelease, ver?.BuildMetadata, matchBehavior)
        {
        }

        static Regex parser = new Regex(@"^(?<compatible>\^)?((?<major>\d+)(\.(?<minor>\d+)(\.(?<patch>\d+))?)?)?(-(?<prerelease>([a-zA-Z0-9-\.]+)))?(\+(?<metadata>[a-zA-Z0-9-\.]+))?$", RegexOptions.Compiled);
        static Regex semVerPrereleaseChars = new Regex(@"^[a-zA-Z0-9-\.]+$", RegexOptions.Compiled);
        
        /// <summary>
        /// Parses a string as a VersionSpecifier.
        /// </summary>
        public static bool TryParse(string version, out VersionSpecifier ver)
        {
            if (version != null)
            {
                if (version.Equals("Any", StringComparison.OrdinalIgnoreCase))
                {
                    ver = VersionSpecifier.Any;
                    return true;
                }
                var m = parser.Match(version);
                if (m.Success)
                {
                    ver = new VersionSpecifier(
                        m.Groups["major"].Success ? (int?)int.Parse(m.Groups["major"].Value) : null,
                        m.Groups["minor"].Success ? (int?)int.Parse(m.Groups["minor"].Value) : null,
                        m.Groups["patch"].Success ? (int?)int.Parse(m.Groups["patch"].Value) : null,
                        m.Groups["prerelease"].Success ? m.Groups["prerelease"].Value : null,
                        m.Groups["metadata"].Success ? m.Groups["metadata"].Value : null,
                         m.Groups["compatible"].Success ? VersionMatchBehavior.Compatible : VersionMatchBehavior.Exact
                    );
                    return true;
                }
                else if (semVerPrereleaseChars.IsMatch(version))
                {
                    ver = new VersionSpecifier(null, null, null, version, null, VersionMatchBehavior.Compatible);
                    return true;
                }
            }
            ver = default(VersionSpecifier);
            return false;
        }

        /// <summary>
        /// Parses a string as a VersionSpecifier.
        /// </summary>
        /// <exception cref="FormatException">The string is not a valid version specifier.</exception>
        public static VersionSpecifier Parse(string version)
        {
            if (TryParse(version, out var ver))
                return ver;
            throw new FormatException($"The string '{version}' is not a valid version specifier.");
        }

        /// <summary>
        /// Converts this value to a string. This string can be parsed by <see cref="Parse(string)"/> and <see cref="TryParse(string, out VersionSpecifier)"/>.
        /// </summary>
        public override string ToString()
        {
            if (this == VersionSpecifier.Any)
                return "Any";
            StringBuilder sb = new StringBuilder();
            if (MatchBehavior.HasFlag(VersionMatchBehavior.Compatible))
                sb.Append("^");
            if (Major.HasValue)
                sb.Append(Major);
            if (Minor.HasValue)
            {
                sb.Append(".");
                sb.Append(Minor);
            }
            
            if (Patch.HasValue)
            {
                sb.Append(".");
                sb.Append(Patch);
            }
            if (!String.IsNullOrEmpty(PreRelease))
            {
                sb.Append("-");
                sb.Append(PreRelease);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Compatibility comparison that returns true if the given version can fulfil this specification. I.e. 'actualVersion' can replace 'this' in every respect.
        /// </summary>
        /// <param name="actualVersion"></param>
        /// <returns></returns>
        public bool IsCompatible(SemanticVersion actualVersion)
        {
            if (ReferenceEquals(this,VersionSpecifier.Any))
                return true; // this is just a small performance shortcut. The below logic would have given the same result.

            if (MatchBehavior == VersionMatchBehavior.Exact)
                return MatchExact(actualVersion);
            if (MatchBehavior.HasFlag(VersionMatchBehavior.Compatible))
                return MatchCompatible(actualVersion);
            
            return false;
        }

        private bool MatchExact(SemanticVersion actualVersion)
        {
            if (actualVersion == null)
                return false;
            if (Major.HasValue && Major.Value != actualVersion.Major)
                return false;
            if (Minor.HasValue && Minor.Value != actualVersion.Minor)
                return false;
            if (Patch.HasValue && Patch.Value != actualVersion.Patch)
                return false;
            if (MatchBehavior.HasFlag(VersionMatchBehavior.AnyPrerelease))
                return true;
            if (PreRelease != actualVersion.PreRelease)
                return false;
            if (string.IsNullOrEmpty(BuildMetadata) == false && BuildMetadata != actualVersion.BuildMetadata)
                return false;
            return true;
        }

        private bool MatchCompatible(SemanticVersion actualVersion)
        {
            if (actualVersion == null)
                return true;
            if (Major.HasValue && Major.Value != actualVersion.Major)
                return false;
            if (Minor.HasValue && Minor.Value > actualVersion.Minor)
                return false;

            if (MatchBehavior.HasFlag(VersionMatchBehavior.AnyPrerelease))
                return true;
            if (0 < new SemanticVersion(0, 0, 0, PreRelease, null).CompareTo(new SemanticVersion(0, 0, 0, actualVersion.PreRelease, null)))
                return false;
            return true;
        }

        /// <summary>
        /// Gets the hash code of this value.
        /// </summary>
        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        /// <summary>
        /// Compares this VersionSpecifier with another object.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is VersionSpecifier other)
            {
                if (Major != other.Major)
                    return false;
                if (Minor != other.Minor)
                    return false;
                if (Patch != other.Patch)
                    return false;
                if (PreRelease != other.PreRelease)
                    return false;
                if (MatchBehavior != other.MatchBehavior)
                    return false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Overloaded == operator that provides value equality (instead of the default reference equality)
        /// </summary>
        public static bool operator ==(VersionSpecifier a, VersionSpecifier b)
        {
            return Object.Equals(a, b);
        }

        /// <summary>
        /// Overloaded != operator that provides value equality (instead of the default reference equality)
        /// </summary>
        public static bool operator !=(VersionSpecifier a, VersionSpecifier b)
        {
            return !(a == b);
        }
    }

    /// <summary>
    /// Describes the behavior of <see cref="VersionSpecifier.IsCompatible(SemanticVersion)"/>.
    /// </summary>
    [Flags]
    public enum VersionMatchBehavior
    {
        /// <summary>
        /// The <see cref="SemanticVersion"/> must match all (non-null) fields in the specified in a <see cref="VersionSpecifier"/> for <see cref="VersionSpecifier.IsCompatible(SemanticVersion)"/> to return true.
        /// </summary>
        Exact = 1,
        /// <summary>
        /// The <see cref="SemanticVersion"/> must be compatible with the version specified in a <see cref="VersionSpecifier"/> for <see cref="VersionSpecifier.IsCompatible(SemanticVersion)"/> to return true.
        /// </summary>
        Compatible = 2,
        /// <summary>
        /// Prerelease property of <see cref="VersionSpecifier"/> is ignored when looking for matching packages.
        /// </summary>
        AnyPrerelease = 4,
    }
}
