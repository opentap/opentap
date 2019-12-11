//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Text.RegularExpressions;

namespace OpenTap.Package
{
    /// <summary> This interface specifies how to convert from a version string to a SemanticVersion. </summary>
    public interface IVersionConverter : ITapPlugin
    {
        /// <summary> Try to convert 'versionString' to a SemanticVersion. Will throw on exceptions. </summary>
        SemanticVersion Convert(string versionString);
    }

    /// <summary> Plugin type like IVersionConverter, that can TryConvert, which does will not throw an exception. </summary>
    public interface IVersionTryConverter : ITapPlugin
    {
        /// <summary>  Try to convert 'versionString' to a SemanticVersion. Returns false on failure.  </summary>
        bool TryConvert(string versionString, out SemanticVersion version);
    }

    [Display("ConvertMajorMinorBuildRevision", 
        "Supports a four value number (x.x.x.x) which will be interpreted as Major.Minor.BuildMetadata.Patch. This is compatible with Microsofts definition of version numbers (e.g. for .NET assemblies), see https://docs.microsoft.com/en-us/dotnet/api/system.version",
        Order: 2)]
    internal class MajorMinorBuildRevisionVersionConverter : IVersionTryConverter
    {
        public SemanticVersion Convert(string versionString)
        {
            string[] parts = versionString.Split('.');
            if (parts.Length != 4)
                throw new ArgumentException("Version number must have 4 values.");
            return new SemanticVersion(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[3]),null,parts[2]);
        }

        public bool TryConvert(string versionString, out SemanticVersion version)
        {
            version = null;
            string[] parts = versionString.Split('.');
            if (parts.Length != 4)
                return false;

            if(int.TryParse(parts[0], out var maj) && int.TryParse(parts[1], out var min) && int.TryParse(parts[3], out var rev))
            {
                version = new SemanticVersion(maj, min, rev, null, parts[2]);
                return true;
            }

            return false;
        }
    }

    [Display("ConvertFourValue", 
        "Supports a four value number (x.y.z.w) which will be converted to the semantic version number x.y.z+w.",
        Order: 1)]
    internal class FourValueVersionConverter : IVersionTryConverter
    {
        public SemanticVersion Convert(string versionString)
        {
            string[] parts = versionString.Split('.');
            if (parts.Length != 4)
                throw new ArgumentException("Version number must have 4 values.");
            return new SemanticVersion(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), null, parts[3]);
        }

        public bool TryConvert(string versionString, out SemanticVersion version)
        {
            version = null;
            string[] parts = versionString.Split('.');
            if (parts.Length != 4)
                return false;

            if (int.TryParse(parts[0], out var maj) && int.TryParse(parts[1], out var min) && int.TryParse(parts[2], out var rev))
            {
                version = new SemanticVersion(maj, min, rev, null, parts[3]);
                return true;
            }

            return false;
        }
    }

    [Display("Compatibility",
    "For compatibility with TAP 8.x parsing.",
    Order: 1)]
    internal class Tap8CompatibilityVersionConverter : IVersionConverter
    {
        public SemanticVersion Convert(string versionString)
        {
            int major, minor, build = 0;
            // Otherwise fall back to legacy code.
            var full_parts = versionString.Trim().Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            if (full_parts.Length == 0)
                return new SemanticVersion(0,0,0,null,null);
            var version_string = full_parts[0];

            var parts = version_string.Split('.');
            bool isok = int.TryParse(parts[0], out major);
            if (!isok) return new SemanticVersion(0, 0, 0, null, null);
            if (parts.Length == 1) return new SemanticVersion(major, 0, 0, null, null); ;
            isok = int.TryParse(parts[1], out minor);

            if (!isok)
            {
                if (parts[1].Length > 0 && parts.Length == 2)
                {
                    return new SemanticVersion(major, 0, 0, null, parts[1]);
                }
                return new SemanticVersion(major, 0, 0, null, null);
            }
            if (parts.Length > 2)
                isok = int.TryParse(parts[2], out build);
            else if (full_parts.Length > 1)
            {
                // When developing and using a OpenTAP version that is built on the local machine
                // (not by pushing to git and have the CI system do the build), we would like
                // for OpenTAP not to complain about incompatible build versions.
                string type = full_parts[1];
                if (type == "Development")
                    build = int.MaxValue;
            }
            string commit = null;
            string prerelease = null;
            if (parts.Length > 3)
                commit = parts[3];
            if (!isok)
            {
                var match = Regex.Match(version_string, ".*?-(.*?)(\\+|$)");
                if (match.Success && match.Groups.Count > 1 && match.Groups[1].Value != "")
                    prerelease = match.Groups[1].Value;

                match = Regex.Match(parts[2], ".*?\\+(.*)");
                if (match.Success && match.Groups.Count > 1)
                    commit = match.Groups[1].Value;

                version_string = parts[2];
                version_string = version_string.Split('-')[0];
                version_string = version_string.Split('+')[0];

                isok = int.TryParse(version_string, out build);
                if (isok == false)
                    build = 0;
            }

            return new SemanticVersion(major,minor,build,prerelease,commit);
        }
    }

}
