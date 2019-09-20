//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap;
using OpenTap.Cli;
using System;
using System.IO;
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

        public override int Execute(CancellationToken cancellationToken)
        {
            if (Directory.Exists(output) == false)
                Directory.CreateDirectory(output);

            using (var reader = new StreamReader(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("OpenTap.Sdk.New.Resources.csprojTemplate.txt")))
            {
                var content = ReplaceInTemplate(reader.ReadToEnd(), Name);
                WriteFile(Path.Combine(output ?? Directory.GetCurrentDirectory(), Name + ".csproj"), content);
            }

            new GenerateTestStep() { Name = "MyFirstTestStep", output = Path.Combine(output ?? Directory.GetCurrentDirectory(), "MyFirstTestStep.cs") }.Execute(cancellationToken);
            new GenerateTestPlan() { Name = "MyFirstTestPlan", output = Path.Combine(output ?? Directory.GetCurrentDirectory(), "MyFirstTestPlan.cs") }.Execute(cancellationToken);
            new GeneratePackageXml() { Name = this.Name, output = Path.Combine(output ?? Directory.GetCurrentDirectory(), "package.xml") }.Execute(cancellationToken);

            return 0;
        }
    }
}
