//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using Newtonsoft.Json.Linq;
using OpenTap.Cli;
using OpenTap.Package;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
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

        public override int Execute(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(output) == false && Directory.Exists(output) == false)
                Directory.CreateDirectory(output);

            using (var reader = new StreamReader(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("OpenTap.Sdk.New.Resources.csprojTemplate.txt")))
            {
                var version = NugetInterop.GetLatestNugetVersionOldestFirst()?.LastOrDefault();
                if(version == null)
                {
                    log.Info("Unable to get an OpenTAP version from Nuget, using the local version instead.");
                    // cannot get opentap version. Try something else..
                    var v2 = new Installation(Path.GetDirectoryName(typeof(ITestStep).Assembly.Location)).GetOpenTapPackage()?.Version;
                    if(v2 == null) // panic?
                        v2 = new SemanticVersion(9, 4, 2, null, null);
                    version = new SemanticVersion(v2.Major, v2.Minor, v2.Patch, null, null);
                }
                else
                    log.Info("Using OpenTAP version {0} from Nuget.", version);
                var content = ReplaceInTemplate(reader.ReadToEnd(), version.ToString());

                WriteFile(Path.Combine(output ?? Directory.GetCurrentDirectory(), Name + ".csproj"), content);
            }

            new GenerateTestStep() { Name = "MyFirstTestStep", output = Path.Combine(output ?? Directory.GetCurrentDirectory(), "MyFirstTestStep.cs") }.Execute(cancellationToken);
            new GenerateTestPlan() { Name = "MyFirstTestPlan", output = Path.Combine(output ?? Directory.GetCurrentDirectory(), "MyFirstTestPlan.TapPlan") }.Execute(cancellationToken);
            new GeneratePackageXml() { Name = this.Name, output = Path.Combine(output ?? Directory.GetCurrentDirectory(), "package.xml") }.Execute(cancellationToken);

            return 0;
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
                    log.Info("Getting the latest OpenTAP Nuget package version. This can be changed in the project settings...");
                    while (!stringTask.Wait(1000, TapThread.Current.AbortToken))
                    {
                        if (sw.ElapsedMilliseconds > 10000)
                            return null;
                        log.Info("Waiting for response from nuget... ");
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
