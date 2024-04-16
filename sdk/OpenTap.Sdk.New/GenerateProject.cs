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
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

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
            public string Message =>
                "OpenTAP dotnet new templates are not installed. Do you wish to install the templates?";

            [Layout(LayoutMode.FullRow | LayoutMode.FloatBottom)]
            [Submit]
            public InstallTemplateResponse Response { get; set; } = InstallTemplateResponse.Yes;
        }

        #region ExitCodes

        // Exit codes are documented here: https://github.com/dotnet/templating/wiki/Exit-Codes
        private const int TemplateNotFound = 103;

        #endregion

        [CommandLineArgument("out", ShortName = "o", Description = "Destination directory for generated files.")]
        public override string output
        {
            get => base.output;
            set => base.output = value;
        }

        [UnnamedCommandLineArgument("name", Required = true)]
        public string Name { get; set; }

        private static bool IsAncestor(DirectoryInfo root, DirectoryInfo dest)
        {
            while (dest != null)
            {
                if (root.FullName.Equals(dest.FullName))
                    return true;
                dest = dest.Parent;
            }

            return false;
        }

        public override int Execute(CancellationToken cancellationToken)
        {
            if (!Validate(name: Name, allowWhiteSpace: false, allowLeadingNumbers: false, allowAlphaNumericOnly: true))
            {
                return (int)ExitCodes.ArgumentError;
            }

            var dest = string.IsNullOrWhiteSpace(output)
                ? new DirectoryInfo(WorkingDirectory)
                : new DirectoryInfo(output);
            if (!dest.Exists) dest.Create();
            var sln = dest.EnumerateFiles("*.sln").FirstOrDefault();

            if (sln == null)
            {
                // Check if there is a solution file in the working directory, and if the working directory is an ancestor of the destination
                var workingDirectory = new DirectoryInfo(WorkingDirectory);
                if (IsAncestor(workingDirectory, dest))
                    sln = workingDirectory.EnumerateFiles("*.sln").FirstOrDefault();
            }

            bool newSolution = sln == null;
            if (sln == null)
            {
                // create solution file.
                // If an output folder was specified, put the solution file there.
                // If no output folder was specified, create a directory and put the solution there.
                if (string.IsNullOrWhiteSpace(output))
                    dest = dest.CreateSubdirectory(Name);

                int result = DotnetNew(Name, dest, "sln", cancellationToken);
                sln = dest.EnumerateFiles("*.sln").FirstOrDefault();
                if (result != 0 || sln == null || !sln.Exists)
                {
                    return result;
                } 

                // Create a Directory.Build.Props file for the new solution
                if (dest.EnumerateFiles("Directory.Build.props").Any() == false)
                {
                    using var reader = new StreamReader(Assembly.GetExecutingAssembly()!
                        .GetManifestResourceStream("OpenTap.Sdk.New.Resources.Directory.Build.props.txt")!);
                    var version = GetOpenTapVersion();
                    var content = ReplaceInTemplate(reader.ReadToEnd(), version.ToString());
                    WriteFile(Path.Combine(dest.FullName, "Directory.Build.props"), content);
                    log.Info("'Directory.Build.props' contains solution-wide settings. " +
                             "Its primary purpose is to ensure that all your projects use the same version of OpenTAP, and build into the same directory.\n");
                }
            } 

            // BUG: creating new project with --out location causes
            // solution and project to be created in the same directory 
            
            // If no output directory was requested, or if we just created a new solution in the requested directory,
            // put the project in a directory of the same name in the solution directory
            if (string.IsNullOrWhiteSpace(output) || newSolution)
                dest = sln.Directory.CreateSubdirectory(Name);

            // Create the new project
            int exitCode = DotnetNew(Name, dest, "opentap", cancellationToken);
            if (exitCode == TemplateNotFound)
            {
                var question = new InstallTemplateQuestion();
                UserInput.Request(question, true);
                if (question.Response == InstallTemplateResponse.Yes)
                {
                    exitCode = InstallTemplate(cancellationToken);
                    if (exitCode == 0)
                        exitCode = DotnetNew(Name, dest, "opentap", cancellationToken);
                }
            }

            if (exitCode != 0)
                return exitCode;

            var csproj = dest.EnumerateFiles("*.csproj").First();
            
            // Update the OpenTAP version in the .csproj if a Directory.Build.props file exists.
            if (sln.Directory.EnumerateFiles("Directory.Build.props").Any())
                ReplaceVersion(csproj);
            
            
            // add the new project to the solution
            exitCode = DotnetSlnAdd(sln, csproj, cancellationToken);

            return exitCode;
        }

        private static void ReplaceVersion(FileInfo csproj)
        {
            // The nuget .csproj templates specify an OpenTAP version. The OpenTAP version is already controlled
            // from the build props file. Update OpenTAP versions from the .csproj to use the variable instead.
            var doc = XElement.Load(csproj.FullName);
            var itemgroups = doc.Elements("ItemGroup").ToArray();
            foreach (var grp in itemgroups)
            {
                var refs = grp.Elements("PackageReference").ToArray();
                foreach (var r in refs)
                {
                    if (r?.Attribute("Include")?.Value == "OpenTAP")
                    { 
                        r.SetAttributeValue("Version", "$(OpenTapVersion)");
                    }
                }
            }
            doc.Save(csproj.FullName);
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

        private static int RunDotnet(string args, CancellationToken token)
        {
            var si = new ProcessStartInfo("dotnet", args)
            {
                UseShellExecute = true,
            };

            log.Info($"Running dotnet {args}");

            var p = new Process() { StartInfo = si };
            p.Start();

            WaitForExit(p, token);

            return p.ExitCode;

        }

        private static int DotnetNew(string projectName, DirectoryInfo directory, string template,
            CancellationToken token)
        {
            var args = $"new {template} --name \"{projectName}\" --output \"{directory.FullName}\"";
            return RunDotnet(args, token);
        }

        private static int DotnetSlnAdd(FileInfo sln, FileInfo csproj, CancellationToken token)
        {
            // dotnet sln <SLN_FILE> add [<PROJECT_PATH>...] [options]
            var args = $"sln \"{sln.FullName}\" add \"{csproj.FullName}\"";
            return RunDotnet(args, token);
        }

        private static SemanticVersion _opentapVersion = null;
        private SemanticVersion GetOpenTapVersion()
        {
            if (_opentapVersion == null)
                _opentapVersion = NugetInterop.GetLatestNugetVersion();
            
            if (_opentapVersion == null)
            {
                log.Warning("Unable to get an OpenTAP version from Nuget, using the local version instead.");
                // cannot get opentap version. Try something else..
                var v2 = new Installation(Path.GetDirectoryName(typeof(ITestStep).Assembly.Location))
                    .GetOpenTapPackage()?.Version;
                if (v2 == null) // panic?
                    v2 = new SemanticVersion(9, 24, 0, null, null);
                _opentapVersion = new SemanticVersion(v2.Major, v2.Minor, v2.Patch, null, null);
            }

            return _opentapVersion;
        }
    }

    class NugetInterop
    {
        /// <summary> Gets all the available OpenTAP versions from Nuget (api.nuget.org) or returns null on a failure (unable to connect). It never throws. </summary> 
        public static SemanticVersion GetLatestNugetVersion()
        {
            var log = Log.CreateSource("Nuget");

            try
            {
                using (var http = new HttpClient())
                {
                    var sw = Stopwatch.StartNew();
                    var stringTask =
                        http.GetAsync("https://api-v2v3search-0.nuget.org/query?q=packageid:opentap&prerelease=false");
                    log.Debug(
                        "Getting the latest OpenTAP Nuget package version. This can be changed in the project settings.");
                    while (!stringTask.Wait(1000, TapThread.Current.AbortToken))
                    {
                        if (sw.ElapsedMilliseconds > 10000)
                            return null;
                        log.Debug("Waiting for response from nuget... ");
                    }

                    var str = stringTask.Result;

                    log.Debug(sw, "Got response from server");
                    var content = str.Content.ReadAsStringAsync().Result;

                    var j = JObject.Parse(content);
                    log.Debug("Parsed JSON content");
                    if (SemanticVersion.TryParse(j["data"][0]["version"].Value<string>(), out SemanticVersion v))
                        return v;
                    return null;
                }
            }
            catch
            {
                log.Warning("Failed to get a response from Nuget server.");
                return null;
            }
        }
    }
}