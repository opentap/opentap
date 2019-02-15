//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.IO;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Package
{
    [Display("gitversion", Group: "sdk", Description: "Calculates a semantic version number for a specific git commit.")]
    public class GitVersionAction : OpenTap.Cli.ICliAction
    {
        [CommandLineArgument("dir", Description = "Directory containing git repository to calculate the version number from.")]
        public string RepoPath { get; set; }

        [UnnamedCommandLineArgument("ref", Required = false)]
        public string Sha { get; set; }

        public GitVersionAction()
        {
            RepoPath = Directory.GetCurrentDirectory();
        }

        public int Execute(CancellationToken cancellationToken)
        {
            using (GitVersionCalulator calc = new GitVersionCalulator(RepoPath))
            {
                if (String.IsNullOrEmpty(Sha))
                    Console.WriteLine(calc.GetVersion());
                else
                    Console.WriteLine(calc.GetVersion(Sha));
            }
            return 0;
        }
    }
}
