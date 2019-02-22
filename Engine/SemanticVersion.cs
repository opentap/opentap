//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Collections.Generic;

namespace OpenTap
{
    /// <summary>
    /// Version object for TAP versions. Adheres to Semantic Version 2.0 formatting and behavior, see http://semver.org. 
    /// Supported formats:
    ///   Major.Minor.Patch
    ///   Major.Minor.Patch-PreRelease
    ///   Major.Minor.Patch+BuildMetadata.
    ///   Major.Minor.Patch-PreRelease+BuildMetadata.
    /// </summary>
    [Serializable]
    [DebuggerDisplay("{Major}.{Minor}.{Patch}-{PreRelease}+{BuildMetadata}")]
    public class SemanticVersion : IComparable
    {
        /// <summary>
        /// Major version. Incrementing this number signifies a backward incompatible change in the API.
        /// </summary>
        public readonly int Major;

        /// <summary>
        /// Minor version. Incrementing this number usually signifies a backward compatible addition to the API.
        /// </summary>
        public readonly int Minor;

        /// <summary>
        /// Patch version. Incrementing this number signifies a change that is both backward and forward compatible.
        /// </summary>
        public readonly int Patch;

        /// <summary>
        /// Optional build related metadata. Usually a short git commit hash (8 chars). Ignored when determining version presedence. Only ASCII alphanumeric characters and hyphen is allowed [0-9A-Za-z-]
        /// </summary>
        public readonly string BuildMetadata;

        /// <summary>
        /// Optional pre-release version, denoted by a -. Only ASCII alphanumeric characters and hyphen is allowed [0-9A-Za-z-]. 
        /// A pre-release version indicates that the version is unstable and might not satisfy the intended compatibility requirements as denoted by its associated normal version.
        /// </summary>
        public readonly string PreRelease;

        private static Regex semVerRegex = new Regex(@"^(?<major>\d+)\.(?<minor>\d+)(?:\.(?<patch>\d+))?(?:-(?<prerelease>[a-zA-Z0-9-.]+))?(?:\+(?<metadata>[a-zA-Z0-9-.]+))?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        private static Regex validChars = new Regex("^[a-zA-Z0-9-.]*$", RegexOptions.Compiled);

        /// <summary>
        /// Creates a new SemanticVersion instance
        /// </summary>
        /// <param name="major">Major version. Incrementing this number signifies a backward incompatible change in the API.</param>
        /// <param name="minor">Minor version. Incrementing this number usually signifies a backward compatible addition to the API.</param>
        /// <param name="patch">Patch version. Incrementing this number signifies a change that is both backward and forward compatible.</param>
        /// <param name="preRelease">Optional pre-release version, denoted by a -. Only ASCII alphanumeric characters and hyphen is allowed [0-9A-Za-z-]. </param>
        /// <param name="buildMetadata">Optional build related metadata. Usually a short git commit hash (8 chars). Ignored when determining version presedence. Only ASCII alphanumeric characters and hyphen is allowed [0-9A-Za-z-]</param>
        public SemanticVersion(int major, int minor, int patch, string preRelease, string buildMetadata)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            if (preRelease != null && !validChars.IsMatch(preRelease))
                throw new ArgumentException("Only ASCII alphanumeric characters, hyphen and dot is allowed (0-9, A-Z, a-z, '-' and '.').", "preRelease");
            PreRelease = preRelease;
            if (buildMetadata != null && !validChars.IsMatch(buildMetadata))
                throw new ArgumentException("Only ASCII alphanumeric characters, hyphen and dot is allowed (0-9, A-Z, a-z, '-' and '.').", "buildMetadata");
            BuildMetadata = buildMetadata;
        }
        
        /// <summary>
        /// Tries to parse a SemanticVersion from string. Input must strictly adhere to  http://semver.org for this method to return true.
        /// </summary>
        /// <returns>True if the string was sucessfully parsed.</returns>
        public static bool TryParse(string version, out SemanticVersion result)
        {
            if (version != null)
            {
                // Do real parsing of semantic versioning style versions.
                var match = semVerRegex.Match(version);
                if (match.Success)
                {
                    result = new SemanticVersion(
                        int.Parse(match.Groups["major"].Value),
                        int.Parse(match.Groups["minor"].Value),
                        match.Groups["patch"].Success ? int.Parse(match.Groups["patch"].Value) : 0,
                        match.Groups["prerelease"].Success ? match.Groups["prerelease"].Value : null,
                        match.Groups["metadata"].Success ? match.Groups["metadata"].Value : null);
                    return true;
                }
            }
            result = default(SemanticVersion);
            return false;
        }

        /// <summary>
        /// Parses a SemanticVersion from string. In addition to the http://semver.org format, this also supports a four value number (x.x.x.x) which will be interpreted as Major.Minor.BuildMetadata.Patch.
        /// The non semver format is supported to be compatible with Microsofts definition of version numbers (e.g. for .NET assemblies), see https://docs.microsoft.com/en-us/dotnet/api/system.version
        /// </summary>
        /// <exception cref="FormatException"></exception>
        /// <returns></returns>
        public static SemanticVersion Parse(string version)
        {
            if(TryParse(version, out SemanticVersion semver))
            {
                return semver;
            }
            throw new FormatException("The version string is not in a Semantic Version compliant format.");
        }
        
        static ThreadLocal<StringBuilder> versionFormatter = new ThreadLocal<StringBuilder>(() => new StringBuilder(), false);

        /// <summary>
        /// Prints the string in version format. It should be parsable from the same string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var formatter = versionFormatter.Value;

            formatter.Clear();

            formatter.Append(Major);
            formatter.Append('.');
            formatter.Append(Minor);

            if (Patch != int.MaxValue)
            {
                formatter.Append('.');
                formatter.Append(Patch);
            }
            if (false == string.IsNullOrWhiteSpace(PreRelease))
            {
                formatter.Append('-');
                formatter.Append(PreRelease);
            }
            if (false == string.IsNullOrWhiteSpace(BuildMetadata))
            {
                formatter.Append('+');
                formatter.Append(BuildMetadata);
            }

            return formatter.ToString();
        }

        /// <summary>
        /// Prints the string in version format. It should be parsable from the same string.
        /// </summary>
        /// <param name="fieldCount">Number of values to return. Must be 1, 2 or 3.</param>
        /// <returns></returns>
        public string ToString(int fieldCount)
        {
            switch(fieldCount)
            {
                case 1:
                    return Major.ToString();
                case 2:
                    return $"{Major}.{Minor}";
                case 3:
                    return $"{Major}.{Minor}.{Patch}";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Returns true if the given version is backwards compatible with this. Meaning that 'other' can replace 'this' in every respect. 
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool IsCompatible(SemanticVersion other)
        {
            if (other == null)
                throw new ArgumentNullException("other");
            if (other.Major != Major) return false;
            if (other.Minor < Minor) return false;
            return true;
        }

        /// <summary>
        /// Returns -1 if obj is greater than this version, 0 if they are the same, and 1 if this is grater than obj
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public int CompareTo(object obj)
        {
            if (!(obj is SemanticVersion))
                throw new ArgumentException("Object is not a TapVersion");

            SemanticVersion other = (SemanticVersion)obj;
            if (Major > other.Major) return 1;
            if (Major < other.Major) return -1;
            if (Minor > other.Minor) return 1;
            if (Minor < other.Minor) return -1;
            if (Patch > other.Patch) return 1;
            if (Patch < other.Patch) return -1;

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
        /// Returns true if the two versions are equal.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if(obj is SemanticVersion)
                return this.CompareTo(obj) == 0;
            return false;
        }

        /// <summary>
        /// Returns the hashcode for the version.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }

        /// <summary>
        /// Overloaded == operator that provides value equality (instead of the default reference equality)
        /// </summary>
        public static bool operator ==(SemanticVersion a, SemanticVersion b)
        {
            return Object.Equals(a, b);
        }

        /// <summary>
        /// Overloaded != operator that provides value equality (instead of the default reference equality)
        /// </summary>
        public static bool operator !=(SemanticVersion a, SemanticVersion b)
        {
            return !(a == b);
        }
    }
}
