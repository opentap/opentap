//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LibGit2Sharp;
using OpenTap.Cli;

namespace OpenTap.Package
{
    /// <summary>
    /// CLI sub command `tap sdk gitversion` that can calculate a version number based on the git history and a .gitversion file.
    /// </summary>
    [Display("gitversion", Group: "sdk",
        Description: "Calculate the semantic version number for a specific git commit.")]
    public class GitVersionAction : OpenTap.Cli.ICliAction
    {
        private static readonly TraceSource log = Log.CreateSource("GitVersion");

        /// <summary>
        /// Represents the --gitlog command line argument which prints git log for the last n commits including version numbers for each commit.
        /// </summary>
        [CommandLineArgument("gitlog",
            Description = "Print the git log for the last <arg> commits including their semantic version number.")]
        public string PrintLog { get; set; }

        /// <summary>
        /// Represents an unnamed command line argument which specifies for which git ref a version should be calculated.
        /// </summary>
        [UnnamedCommandLineArgument("ref", Required = false)]
        public string Sha { get; set; }

        /// <summary>
        /// Represents the --replace command line argument which causes this command to replace all occurrences of $(GitVersion) in the specified file. Cannot be used together with --gitlog.
        /// </summary>
        [CommandLineArgument("replace",
            Description =
                "Replace all occurrences of $(GitVersion) in the specified file\nwith the calculated semantic version number. It cannot be used with --gitlog.")]
        public string ReplaceFile { get; set; }

        /// <summary>
        /// Represents the --fields command line argument which specifies the number of version fields to print/replace.
        /// </summary>
        [CommandLineArgument("fields", Description =
            "Number of version fields to print/replace. The fields are: major, minor, patch,\n" +
            "pre-release, and build metadata. E.g., --fields=2 results in a version number\n" +
            "containing only the major and minor field. The default is 5 (all fields).")]
        public int FieldCount { get; set; }

        /// <summary>
        /// Represents the --dir command line argument which specifies the directory in which the git repository to use is located.
        /// </summary>
        [CommandLineArgument("dir",
            Description = "Directory containing the git repository to calculate the version number from.")]
        public string RepoPath { get; set; }

        /// <summary>
        /// Constructs new action with default values for arguments.
        /// </summary>
        public GitVersionAction()
        {
            RepoPath = Directory.GetCurrentDirectory();
            FieldCount = 5;
        }

        private static bool ranOnce = false;

        /// <summary>
        /// Executes this action.
        /// </summary>
        /// <returns>Returns 0 to indicate success.</returns>
        public int Execute(CancellationToken cancellationToken)
        {
            if (ranOnce)
                throw new Exception(
                    $"Failed to install SDK plugin. Please manually install the SDK version {requiredVersion} or greater.");
            ranOnce = true;

            var requiredVersion = Installation.Current.GetOpenTapPackage().Version;
            var sdk = Installation.Current.GetPackages().FirstOrDefault(p => p.Name == "SDK");
            if (sdk != null && requiredVersion.IsCompatible(sdk.Version))
                throw new Exception($"Cannot calculate gitversion. Please reinstall the SDK plugin.");

            var ins = new PackageInstallAction()
            {
                Packages = new[] { "SDK" },
                Repository = new[] { "https://packages.opentap.io" },
                Version = $"^{requiredVersion}",
            };

            var success = ins.Execute(cancellationToken);
            if (success != 0)
                throw new Exception(
                    ($"Failed to install SDK plugin. Please manually install the SDK version {requiredVersion} or greater."));

            PluginManager.Search();
            return CliActionExecutor.Execute(Environment.GetCommandLineArgs().Skip(1).ToArray());
        }
    }

    /// <summary>
    /// Defines the UseVersion XML element that can be used as a child element to the File element in package.xml 
    /// to indicate that a package should take its version from the AssemblyInfo in that file.
    /// </summary>
    [Display("UseVersion")]
    public class UseVersionData : ICustomPackageData
    {
    }
}
