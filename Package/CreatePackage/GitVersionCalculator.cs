//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Diagnostics;
using LibGit2Sharp;
using System;
using Tap.Shared;

namespace OpenTap.Package
{
    /// <summary>
    /// Calculates the version number of a commit in a git repository
    /// </summary>
    internal class GitVersionCalculator : IDisposable
    {
        private static readonly TraceSource log = Log.CreateSource("GitVersion");
        private readonly OpenTap.GitVersioning.GitVersionCalculatorCore calculator;

        /// <summary>
        /// Instanciates a new <see cref="GitVersionCalculator"/> to work on a specified git repository.
        /// </summary>
        /// <param name="repositoryDir">Path pointing to a directory inside the git repository to use.</param>
        public GitVersionCalculator(string repositoryDir)
        {
            calculator = new OpenTap.GitVersioning.GitVersionCalculatorCore(repositoryDir, Installation.Current.Directory,
                new OpenTap.GitVersioning.GitVersionCalculatorCore.GitVersionLog(
                    message => log.Debug(message),
                    message => log.Warning(message),
                    message => log.Error(message)));
        }

        public void Dispose()
        {
            calculator.Dispose();
        }

        /// <summary>
        /// Calculates the version number of the current HEAD of the git repository
        /// </summary>
        public SemanticVersion GetVersion()
        {
            return Convert(calculator.GetVersion());
        }

        /// <summary>
        /// Calculates the version number of a specific commit in the git repository
        /// </summary>
        public SemanticVersion GetVersion(string sha)
        {
            return Convert(calculator.GetVersion(sha));
        }

        /// <summary>
        /// Calculates the version number of a specific commit in the git repository
        /// </summary>
        public SemanticVersion GetVersion(Commit targetCommit)
        {
            return Convert(calculator.GetVersion(targetCommit));
        }

        private static SemanticVersion Convert(OpenTap.GitVersioning.GitVersionResult version)
        {
            return new SemanticVersion(version.Major, version.Minor, version.Patch, version.PreRelease, version.BuildMetadata);
        }
    }
}
