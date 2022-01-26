//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace OpenTap.Package
{
    /// <summary>
    /// Holds search parameters that specifies a range of packages in the OpenTAP package system.
    /// </summary>
    [DebuggerDisplay("{Name} ({Version.ToString()})")]
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
            : this(package.Name, new VersionSpecifier(package.Version, versionMatchBehavior), package.Architecture, package.OS)
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
    public class VersionSpecifier : IComparable
    {
        /// <summary>
        /// The VersionSpecifier that will match any version. VersionSpecifier.Any.IsCompatible always returns true.
        /// </summary>
        public static readonly VersionSpecifier Any = new VersionSpecifier(null, null, null, null, null, VersionMatchBehavior.Compatible | VersionMatchBehavior.AnyPrerelease);

        /// <summary>
        /// Major version. When not null, <see cref="SemanticVersion.IsCompatible"/> will return false for <see cref="SemanticVersion"/>s with a Major version different from this.
        /// </summary>
        public readonly int? Major;
        /// <summary>
        /// Minor version. When not null, <see cref="SemanticVersion.IsCompatible"/> will return false for <see cref="SemanticVersion"/>s with a Minor version less than this (with <see cref="VersionMatchBehavior.Compatible"/>) or different from this (with <see cref="VersionMatchBehavior.Exact"/>).
        /// </summary>
        public readonly int? Minor;
        /// <summary>
        /// Patch version. When not null, <see cref="SemanticVersion.IsCompatible"/> will return false for <see cref="SemanticVersion"/>s with a Patch version different from this if <see cref="MatchBehavior"/> is <see cref="VersionMatchBehavior.Exact"/>.
        /// </summary>
        public readonly int? Patch;
        /// <summary>
        /// PreRelease identifier. <see cref="SemanticVersion.IsCompatible"/> will return false for <see cref="SemanticVersion"/>s with a PreRelease less than this (with <see cref="VersionMatchBehavior.Compatible"/>) or different from this (with <see cref="VersionMatchBehavior.Exact"/>).
        /// </summary>
        public readonly string PreRelease;
        /// <summary>
        /// BuildMetadata identifier. When not null, <see cref="SemanticVersion.IsCompatible"/> will return false for <see cref="SemanticVersion"/>s with a BuildMetadata different from this if <see cref="MatchBehavior"/> is <see cref="VersionMatchBehavior.Exact"/>.
        /// </summary>
        public readonly string BuildMetadata;

        /// <summary>
        /// The way matching is done. This affects the behavior of <see cref="SemanticVersion.IsCompatible(SemanticVersion)"/>.
        /// </summary>
        public readonly VersionMatchBehavior MatchBehavior;
        /// <summary>
        /// Specifies parts of a semantic version. Unset parameters will be treated as 'any'.
        /// </summary>
        /// 
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
        static Regex semVerPrereleaseRegex = new Regex(@"^(?<compatible>\^)?(?<prerelease>([a-zA-Z0-9-\.]+))$", RegexOptions.Compiled);

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

                var prerelease = semVerPrereleaseRegex.Match(version);
                if (prerelease.Success)
                {
                    var matchBehaviour = prerelease.Groups["compatible"].Success ? VersionMatchBehavior.Compatible : VersionMatchBehavior.Exact;
                    var pre = prerelease.Groups["prerelease"].Success ? prerelease.Groups["prerelease"].Value : null;
                    ver = new VersionSpecifier(null, null, null, pre, null, matchBehaviour);
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

            var formatter = versionFormatter.Value;
            formatter.Clear();

            if (MatchBehavior.HasFlag(VersionMatchBehavior.Compatible))
                formatter.Append('^');
            if (Major.HasValue)
                formatter.Append(Major);
            if (Minor.HasValue)
            {
                formatter.Append('.');
                formatter.Append(Minor);
            }

            if (Patch.HasValue)
            {
                formatter.Append('.');
                formatter.Append(Patch);
            }
            if (!string.IsNullOrEmpty(PreRelease))
            {
                if (formatter.Length != 0)
                    formatter.Append('-');
                formatter.Append(PreRelease);
            }
            if (!string.IsNullOrEmpty(BuildMetadata))
            {
                formatter.Append('+');
                formatter.Append(BuildMetadata);
            }

            return formatter.ToString();
        }

        static ThreadLocal<StringBuilder> versionFormatter = new ThreadLocal<StringBuilder>(() => new StringBuilder(), false);

        /// <summary>
        /// Prints the string in version format. It should be parsable from the same string.
        /// </summary>
        /// <param name="fieldCount">Number of values to return. Must be 1, 2, 4 or 5.</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <returns></returns>
        public string ToString(int fieldCount)
        {
            if (fieldCount < 1 || fieldCount > 5)
                throw new ArgumentOutOfRangeException();

            var formatter = versionFormatter.Value;
            formatter.Clear();

            if (this == VersionSpecifier.Any)
                return "Any";
            if (MatchBehavior.HasFlag(VersionMatchBehavior.Compatible))
                formatter.Append('^');

            if (Major.HasValue)
                formatter.Append(Major);

            if (Minor.HasValue && fieldCount >= 2)
            {
                formatter.Append('.');
                formatter.Append(Minor);
            }
            if (Patch.HasValue && fieldCount >= 3)
            {
                formatter.Append('.');
                formatter.Append(Patch);
            }
            if (!string.IsNullOrWhiteSpace(PreRelease) && fieldCount >= 4)
            {
                formatter.Append('-');
                formatter.Append(PreRelease);
            }
            if (!string.IsNullOrWhiteSpace(BuildMetadata) && fieldCount == 5)
            {
                formatter.Append('+');
                formatter.Append(BuildMetadata);
            }

            return formatter.ToString();
        }

        /// <summary>
        /// Compatibility comparison that returns true if the given version can fulfil this specification. I.e. 'actualVersion' can replace 'this' in every respect.
        /// </summary>
        /// <param name="actualVersion"></param>
        /// <returns></returns>
        public bool IsCompatible(SemanticVersion actualVersion)
        {
            if (ReferenceEquals(this, VersionSpecifier.Any))
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
            {
                if (PreRelease is null || actualVersion.PreRelease is null)
                    return false;

                string[] actualPreReleaseIdentifiers = actualVersion.PreRelease.Split('.');
                string[] preReleaseIdentifiers = PreRelease.Split('.');

                if (actualPreReleaseIdentifiers.Length < preReleaseIdentifiers.Length)
                    return false;

                for (int i = 0; i < preReleaseIdentifiers.Length; i++)
                    if (!actualPreReleaseIdentifiers[i].Equals(preReleaseIdentifiers[i]))
                        return false;
            }
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
            if (Minor.HasValue && Minor.Value == actualVersion.Minor)
                if (Patch.HasValue && Patch.Value > actualVersion.Patch)
                    return false;


            if (MatchBehavior.HasFlag(VersionMatchBehavior.AnyPrerelease))
                return true;

            // We want ^1.0.0 to accept 1.0.1-beta or 1.1.0-beta as compatible versions
            if(PreRelease == null && actualVersion.PreRelease != null)
            {
                if (Minor.HasValue && Minor.Value < actualVersion.Minor)
                    return true;
                if (Minor.HasValue && Minor.Value == actualVersion.Minor)
                    if (Patch.HasValue && Patch.Value < actualVersion.Patch)
                        return true;
            }

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
        /// Returns -1 if obj is greater than this version, 0 if they are the same, and 1 if this is greater than obj
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public int CompareTo(object obj)
        {
            if (!(obj is VersionSpecifier))
                throw new ArgumentException("Object is not a TapVersion");

            VersionSpecifier other = (VersionSpecifier)obj;
            if (Major > other.Major) return 1;
            if (Major < other.Major) return -1;
            if (Minor.HasValue && !other.Minor.HasValue || Minor > other.Minor) return 1;
            if (!Minor.HasValue && other.Minor.HasValue || Minor < other.Minor) return -1;
            if (Patch.HasValue && !other.Patch.HasValue || Patch > other.Patch) return 1;
            if (!Patch.HasValue && other.Patch.HasValue || Patch < other.Patch) return -1;

            return ComparePreRelease(PreRelease, other.PreRelease);
        }

        private static int ComparePreRelease(string p1, string p2)
        {
            if (p1 == p2) return 0;

            if (string.IsNullOrEmpty(p1) && string.IsNullOrEmpty(p2)) return 0;
            if (string.IsNullOrEmpty(p1)) return 1;
            if (string.IsNullOrEmpty(p2)) return -1;

            var identifiers1 = p1.Split('.');
            var identifiers2 = p2.Split('.');

            for (int i = 0; i < Math.Min(identifiers1.Length, identifiers2.Length); i++)
            {
                var id1 = identifiers1[i];
                var id2 = identifiers2[i];

                int v1, v2;

                if (int.TryParse(id1, out v1) && int.TryParse(id2, out v2))
                {
                    if (v1 != v2)
                        return v1.CompareTo(v2);
                }
                else
                {
                    var res = string.Compare(id1, id2);

                    if (res != 0)
                        return res;
                }
            }

            if (identifiers1.Length > identifiers2.Length) return 1;
            if (identifiers1.Length < identifiers2.Length) return -1;

            return 0;
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
        AnyPrerelease = 4
    }
}
