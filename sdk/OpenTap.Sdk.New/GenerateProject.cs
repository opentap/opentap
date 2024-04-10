//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using OpenTap.Cli;
using OpenTap.Package;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace OpenTap.Sdk.New
{
    [Display("project", "OpenTAP C# Project (.csproj). Including a new TestStep, solution file (.sln) and package.xml.", Groups: new[] { "sdk", "new" })]
    public class GenerateProject : GenerateType
    {
        enum InstallTemplateResponse
        {
            No,
            Yes,
        }

        [Display("Install OpenTAP Dotnet Templates?")]
        class InstallTemplateQuestion
        {

            [Browsable(true)]
            [Layout(LayoutMode.FullRow)]
            public string Message => "OpenTAP dotnet new templates are not installed. Do you wish to install the templates?";
            [Layout(LayoutMode.FullRow | LayoutMode.FloatBottom)]
            [Submit] public InstallTemplateResponse Response { get; set; } = InstallTemplateResponse.Yes;
        }

        #region ExitCodes
        // Exit codes are documented here: https://github.com/dotnet/templating/wiki/Exit-Codes
        private const int TemplateNotFound = 103;
        #endregion

        [CommandLineArgument("out", ShortName = "o", Description = "Destination directory for generated files.")]
        public override string output { get => base.output; set => base.output = value; }

        [UnnamedCommandLineArgument("name", Required = true)]
        public string Name { get; set; }

        public override int Execute(CancellationToken cancellationToken)
        {
            if (!Validate(name: Name, allowWhiteSpace: false, allowLeadingNumbers: false, allowAlphaNumericOnly: true))
            {
                return (int)ExitCodes.ArgumentError;
            }

            int exitCode = DotnetNew(Name, output, cancellationToken);
            if (exitCode == TemplateNotFound)
            {
                var question = new InstallTemplateQuestion();
                UserInput.Request(question, true);
                if (question.Response == InstallTemplateResponse.Yes)
                {
                    exitCode = InstallTemplate(cancellationToken);
                    if (exitCode == 0)
                        exitCode = DotnetNew(Name, output, cancellationToken);
                }
            }
            return exitCode;
        }

        private static void WaitForExit(Process p, CancellationToken token)
        {
            var evt = new ManualResetEventSlim(false);
            var trd = TapThread.Start(() =>
                    {
                        while (!p.WaitForExit(100) && !token.IsCancellationRequested)
                        {
                            // wait
                        }

                        evt.Set();
                    });

            WaitHandle.WaitAny(new[] { token.WaitHandle, evt.WaitHandle });
            token.ThrowIfCancellationRequested();
        }

        private static int InstallTemplate(CancellationToken token)
        {
            var sdk = Installation.Current.FindPackage("SDK");
            var template = sdk.Files.First(f => Path.GetExtension(f.FileName) == ".nupkg");
            var fn = Path.Combine(Installation.Current.Directory, template.FileName);

            var si = new ProcessStartInfo("dotnet", $"new --install \"{fn}\"")
            {
                UseShellExecute = true,
            };

            var p = new Process() { StartInfo = si };
            p.Start();
            WaitForExit(p, token);
            return p.ExitCode;
        }

        private static int DotnetNew(string projectName, string directory, CancellationToken token)
        {
            var args = $"new opentap --name \"{projectName}\"";
            if (!string.IsNullOrWhiteSpace(directory))
                args += $" --output \"{directory}\"";
            var si = new ProcessStartInfo("dotnet", args)
            {
                UseShellExecute = true,
            };

            var p = new Process() { StartInfo = si };
            p.Start();

            WaitForExit(p, token);

            return p.ExitCode;
        }
    }
}
