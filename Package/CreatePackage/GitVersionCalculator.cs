//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Diagnostics;
using LibGit2Sharp;
using System;
using System.IO;
using Tap.Shared;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OpenTap.Package
{
    /// <summary>
    /// Calculates the version number of a commit in a git repository
    /// </summary>
    internal class GitVersionCalculator : IDisposable
    {
        private static readonly TraceSource log = Log.CreateSource("GitVersion");
        private const string GIT_HASH = "b7bad55";
        private readonly OpenTap.GitVersioning.GitVersionCalculatorCore calculator;

        static class GlibCHelper
        {
            [DllImport("libc")]
            public static extern IntPtr gnu_get_libc_version();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool IsGlibc()
        {
            try 
            {
                return GlibCHelper.gnu_get_libc_version() != IntPtr.Zero;
            }
            catch 
            {
                return false;
            }
        }
        
        void ensureLibgit2Present()
        {
            string libgit2name;

            if (OperatingSystem.Current == OperatingSystem.Windows)
                libgit2name = $"git2-{GIT_HASH}.dll";
            else if (OperatingSystem.Current == OperatingSystem.Linux)
                libgit2name = $"libgit2-{GIT_HASH}.so";
            else if (OperatingSystem.Current == OperatingSystem.MacOS)
                libgit2name = $"libgit2-{GIT_HASH}.dylib";
            else
            {
                log.Error($"Unsupported platform.");
                return;
            }
            
            var requiredFile = Path.Combine(PathUtils.OpenTapDir, libgit2name);
            if (File.Exists(requiredFile))
                return;

            string sourceFile = Path.Combine(PathUtils.OpenTapDir, "Dependencies/LibGit2Sharp.0.27.0.0/", libgit2name);
            if (OperatingSystem.Current == OperatingSystem.Windows)
                sourceFile += $".{(Environment.Is64BitProcess ? CpuArchitecture.x64 : CpuArchitecture.x86)}";
            if (OperatingSystem.Current == OperatingSystem.MacOS)
                sourceFile += $".{MacOsArchitecture.Current.Architecture}";
            if (OperatingSystem.Current == OperatingSystem.Linux && IsGlibc())
                sourceFile += $".{LinuxArchitecture.Current.Architecture}";
            else if (OperatingSystem.Current == OperatingSystem.Linux)
                sourceFile += $".musl.{LinuxArchitecture.Current.Architecture}";

            try
            {
                File.Copy(sourceFile, requiredFile, true);
            }
            catch (Exception e)
            {
                if (OperatingSystem.Current == OperatingSystem.Windows)
                {
                    var opentapArch = Installation.Current.GetOpenTapPackage()?.Architecture;
                    var processArch = Environment.Is64BitProcess ? CpuArchitecture.x64 : CpuArchitecture.x86;
                    if (opentapArch != processArch)
                        throw new PlatformNotSupportedException($"Unable to find the correct 'libgit2-{GIT_HASH}' because the process architecture '{processArch}' does not match the installed OpenTAP architecture '{opentapArch}'", e);
                }

                throw new PlatformNotSupportedException($"Unable to copy 'libgit2-{GIT_HASH}': {e.Message}.", e);
            }
        }

        /// <summary>
        /// Instanciates a new <see cref="GitVersionCalculator"/> to work on a specified git repository.
        /// </summary>
        /// <param name="repositoryDir">Path pointing to a directory inside the git repository to use.</param>
        public GitVersionCalculator(string repositoryDir)
        {
            ensureLibgit2Present();
            calculator = new OpenTap.GitVersioning.GitVersionCalculatorCore(repositoryDir,
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
