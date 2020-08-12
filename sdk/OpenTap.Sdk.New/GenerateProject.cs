//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using Newtonsoft.Json.Linq;
using OpenTap.Cli;
using OpenTap.Package;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace OpenTap.Sdk.New
{
    [Display("project", "OpenTAP C# Project (.csproj). Including a new TestStep, TestPlan and package.xml.", Groups: new[] { "sdk", "new" })]
    public class GenerateProject : GenerateType
    {
        [CommandLineArgument("out", ShortName = "o", Description = "Destination directory for generated files.")]
        public override string output { get => base.output; set => base.output = value; }

        [UnnamedCommandLineArgument("name", Required = true)]
        public string Name { get; set; }

        private SemanticVersion GetOpenTapVersion()
        {
            var version = NugetInterop.GetLatestNugetVersionOldestFirst()?.LastOrDefault();
            if(version == null)
            {
                log.Warning("Unable to get an OpenTAP version from Nuget, using the local version instead.");
                // cannot get opentap version. Try something else..
                var v2 = new Installation(Path.GetDirectoryName(typeof(ITestStep).Assembly.Location)).GetOpenTapPackage()?.Version;
                if(v2 == null) // panic?
                    v2 = new SemanticVersion(9, 8, 3, null, null);
                version = new SemanticVersion(v2.Major, v2.Minor, v2.Patch, null, null);
            }

            return version;
        }

        private void CreateSolutionFile(DirectoryInfo dest)
        {
            var slnFile = dest.EnumerateFiles("*.sln").FirstOrDefault();
            bool newProject = slnFile == null;
            if (newProject)
            {
                var slnFileName = Path.Combine(dest.FullName, Name + ".sln");
                using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenTap.Sdk.New.Resources.newProjectSlnTemplate.txt")))
                {
                    var content = ReplaceInTemplate(reader.ReadToEnd(), Name, Guid.NewGuid().ToString().ToUpper());
                    WriteFile(slnFileName, content);
                    log.Info("The solution file keeps track of all the projects in this directory.");
                    log.Info("When you create new projects in this directory with the 'tap sdk new project' command, they are automatically added to the solution.\n");
                }
                
                if (dest.EnumerateFiles("Directory.Build.props").Any() == false)
                {
                    using (var reader = new StreamReader(Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream("OpenTap.Sdk.New.Resources.Directory.Build.props.txt")))
                    {
                        var version = GetOpenTapVersion();
                        var content = ReplaceInTemplate(reader.ReadToEnd(), version.ToString());
                        WriteFile(Path.Combine(dest.FullName, "Directory.Build.props"), content);
                        log.Info("'Directory.Build.props' contains solution-wide settings. " +
                                 "Its primary purpose is to ensure that all your projects use the same version of OpenTAP, and build into the same directory.\n");
                    }
                }
            }
            else
            {
                AddProjectToSolutionFile(Name, slnFile);
                log.Info($"Added project {Name} to solution {slnFile.FullName}");
            }
            
        }

        public override int Execute(CancellationToken cancellationToken)
        {
            var dest = string.IsNullOrWhiteSpace(output) ? new DirectoryInfo(WorkingDirectory) : new DirectoryInfo(output);
            
            if (!dest.Exists)
            {
                log.Debug($"Creating '{dest.FullName}' as it does not exist.");
                dest = Directory.CreateDirectory(dest.FullName);
            }

            var newProject = dest.EnumerateFiles("*.sln").Any() == false;

            if (newProject && string.IsNullOrWhiteSpace(output))
            {
                var exists = dest.EnumerateDirectories().FirstOrDefault(x => x.Name == Name);
                dest = exists ?? dest.CreateSubdirectory(Name);
            }

            if (dest.EnumerateDirectories().Any(x => x.Name == Name))
                throw new Exception($"Project {Name} already exists in this solution.");

            CreateSolutionFile(dest);

            dest = dest.CreateSubdirectory(Name);
            
            var csprojFile = Path.Combine(dest.FullName, Name + ".csproj");

            using (var reader = new StreamReader(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("OpenTap.Sdk.New.Resources.csprojTemplate.txt")))
            {
                var content = reader.ReadToEnd();
                WriteFile(csprojFile, content);
            }
            log.Info("The '.csproj' file contains the configuration specific to this project.\n");
            
            new GenerateTestStep() { Name = Name, WorkingDirectory = dest.FullName, output = Path.Combine(dest.FullName, $"{Name}.cs") }.Execute(cancellationToken);
            log.Info("This is a basic TestStep.\n");
            var testPlanName = Path.Combine(dest.FullName, $"{Name}.TapPlan");
            new GenerateTestPlan() { Name = Name, WorkingDirectory = dest.FullName, output = testPlanName }.Execute(cancellationToken);
            log.Info($"This is a basic test plan that executes the above TestStep.");
            log.Info($"Execute this test plan with 'tap run {testPlanName}'.\n");
            new GeneratePackageXml() { Name = Name, WorkingDirectory = dest.FullName, output = Path.Combine(dest.FullName, "package.xml") }.Execute(cancellationToken);
            log.Info("The 'package.xml' file tells OpenTAP which files to package when creating a '.TapPackage'. ");
            log.Info("When building in release mode, OpenTAP will attempt to build a TapPackage according to the contents of this file. ");
            log.Info("Alternatively, you can build a package manually with 'tap package create path/to/package.xml'.\n");

            if (newProject)
            {
                log.Info($"After you build this project, OpenTAP will be installed in '{(Path.Combine(dest.Parent.FullName, "bin", "Debug"))}'");
                log.Info("Try building the project and running the generated test plan!");
            }
            
            return 0;
        }

        private void AddProjectToSolutionFile(string name, FileInfo slnFile)
        {
            var newGuid = Guid.NewGuid().ToString().ToUpper();
            
            string solution = "";
                
            using (var reader = new StreamReader(slnFile.FullName))
            {
                solution = reader.ReadToEnd();
            }
            
            // First add the project to the solution
            var projectLine =
                $"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{Name}\", \"{Name}/{Name}.csproj\", \"{{{newGuid}}}\"\nEndProject\n";

            // Insert the new project before the EndProject tag
            var lastProjectLoc = solution.IndexOf("Global");
            solution = solution.Substring(0, lastProjectLoc) + projectLine + solution.Substring(lastProjectLoc, solution.Length - lastProjectLoc);
            
            // Then add the project to the existing build configurations
            var configurationBlockRE = new Regex(@" +GlobalSection\(ProjectConfigurationPlatforms\) += +postSolution");
            var lines = solution.Split('\n').Select(x => x.TrimEnd()).ToList();
            int index = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (configurationBlockRE.Match(line).Success)
                {
                    index = i + 1; // Configurations start on the next line
                    break;
                }
            }

            // Skip configuration generation if no configuration block is found
            if (index != -1)
            {
                var configurations = new List<string>();

                while (lines[index].Contains("EndGlobalSection") == false && index < lines.Count)
                {
                    var line = lines[index];
                    var configStart = line.IndexOf('}') + 1;
                    var config = line.Substring(configStart);
                    configurations.Add(config.Trim());
                    index++;
                }

                configurations.Reverse();
                
                foreach (var configuration in configurations)
                {
                    var padding = lines[index - 1].IndexOf('{');
                    
                    lines.Insert(index, "".PadLeft(padding) + "{" + newGuid + "}" + configuration);
                }

                solution = string.Join(Environment.NewLine, lines);
            }

            using (var writer = new StreamWriter(slnFile.FullName))
            {
                writer.Write(solution);
            }
        }
    }

    class NugetInterop
    {
        /// <summary> Gets all the available OpenTAP versions from Nuget (api.nuget.org) or returns null on a failure (unable to connect). It never throws. </summary> 
        public static IEnumerable<SemanticVersion> GetLatestNugetVersionOldestFirst()
        {
            var log = Log.CreateSource("Nuget");
            
            try
            {
                using (var http = new HttpClient())
                {
                    var sw = Stopwatch.StartNew();
                    var stringTask = http.GetAsync("https://api.nuget.org/v3/registration4/opentap/index.json");
                    log.Debug("Getting the latest OpenTAP Nuget package version. This can be changed in the project settings.");
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
                    List<SemanticVersion> tapVersions = new List<SemanticVersion>();
                    foreach (var tapitem in j["items"] )
                    {
                        foreach (var tapitem2 in tapitem["items"])
                        {
                            var version = tapitem2["catalogEntry"]["version"].Value<string>();
                            if(SemanticVersion.TryParse(version, out SemanticVersion r))
                                tapVersions.Add(r);
                        }
                    }
                    tapVersions.Sort();
                    log.Debug("Found OpenTap Versions: {0}", string.Join(", ", tapVersions));
                    return tapVersions;
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
