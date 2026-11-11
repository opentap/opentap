//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using GitVersionCalculator;

namespace OpenTap.Package
{
    /// <summary>
    /// Calculates the version number of a commit in a git repository.
    /// </summary>
    internal class GitVersionCalulator
    {
        private static readonly TraceSource log = Log.CreateSource("GitVersion");
        private readonly VersionCalculator calculator;

        /// <summary>
        /// Instantiates a calculator for a directory inside a git repository.
        /// </summary>
        public GitVersionCalulator(string repositoryDir)
        {
            calculator = new VersionCalculator(repositoryDir)
            {
                Warning = message => log.Warning(message)
            };
        }

        /// <summary>Calculates the version number of the current HEAD.</summary>
        public SemanticVersion GetVersion() => GetVersion("HEAD");

        /// <summary>Calculates the version number of a commit, branch or tag.</summary>
        public SemanticVersion GetVersion(string sha)
        {
            var version = calculator.GetVersion(sha);
            return new SemanticVersion(version.Major, version.Minor, version.Patch,
                version.PreRelease, version.BuildMetadata);
        }
    }
}
