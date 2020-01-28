//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap;
using OpenTap.Cli;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace OpenTap.Sdk.New
{
    [Display("dut", "C# template for a DUT plugin. Requires a project.", Groups: new[] { "sdk", "new" })]
    public class GenerateDut : GenerateType
    {
        [UnnamedCommandLineArgument("name", Required = true)]
        public string Name { get; set; }

        public override int Execute(CancellationToken cancellationToken)
        {
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("OpenTap.Sdk.New.Resources.DutTemplate.txt")))
            {
                var content = ReplaceInTemplate(reader.ReadToEnd(), TryGetNamespace(), Name);
                WriteFile(output ?? Path.Combine(Directory.GetCurrentDirectory(), Name + ".cs"), content);
            }

            return 0;
        }
    }
    [Display("instrument", "C# template for a Instrument plugin. Requires a project.", Groups: new[] { "sdk", "new" })]
    public class GenerateInstrument : GenerateType
    {
        [UnnamedCommandLineArgument("name", Required = true)]
        public string Name { get; set; }

        public override int Execute(CancellationToken cancellationToken)
        {
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("OpenTap.Sdk.New.Resources.InstrumentTemplate.txt")))
            {
                var content = ReplaceInTemplate(reader.ReadToEnd(), TryGetNamespace(), Name);
                WriteFile(output ?? Path.Combine(Directory.GetCurrentDirectory(), Name + ".cs"), content);
            }

            return 0;
        }
    }
    [Display("resultlistener", "C# template for a ResultListener plugin. Requires a project.", Groups: new[] { "sdk", "new" })]
    public class GenerateResultListener : GenerateType
    {
        [UnnamedCommandLineArgument("name", Required = true)]
        public string Name { get; set; }

        public override int Execute(CancellationToken cancellationToken)
        {
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("OpenTap.Sdk.New.Resources.ResultListenerTemplate.txt")))
            {
                var content = ReplaceInTemplate(reader.ReadToEnd(), TryGetNamespace(), Name);
                WriteFile(output ?? Path.Combine(Directory.GetCurrentDirectory(), Name + ".cs"), content);
            }

            return 0;
        }
    }
    [Display("settings", "C# template for a ComponentSetting plugin. Requires a project.", Groups: new[] { "sdk", "new" })]
    public class GenerateSetting : GenerateType
    {
        [UnnamedCommandLineArgument("name", Required = true)]
        public string Name { get; set; }

        public override int Execute(CancellationToken cancellationToken)
        {
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("OpenTap.Sdk.New.Resources.SettingsTemplate.txt")))
            {
                var content = ReplaceInTemplate(reader.ReadToEnd(), TryGetNamespace(), Name);
                WriteFile(output ?? Path.Combine(Directory.GetCurrentDirectory(), Name + ".cs"), content);
            }

            return 0;
        }
    }
    [Display("teststep", "C# template for a TestStep plugin. Requires a project.", Groups: new[] { "sdk", "new" })]
    public class GenerateTestStep : GenerateType
    {
        [UnnamedCommandLineArgument("name", Required = true)]
        public string Name { get; set; }

        public override int Execute(CancellationToken cancellationToken)
        {
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("OpenTap.Sdk.New.Resources.TestStepTemplate.txt")))
            {
                var content = ReplaceInTemplate(reader.ReadToEnd(), TryGetNamespace(), Name);
                WriteFile(output ?? Path.Combine(Directory.GetCurrentDirectory(), Name + ".cs"), content);
            }

            return 0;
        }
    }
    [Display("testplan", "Creates a TestPlan (.TapPlan) containing all TestSteps types defined in this project.", Groups: new[] { "sdk", "new" })]
    public class GenerateTestPlan : GenerateType
    {
        [UnnamedCommandLineArgument("name", Required = true)]
        public string Name { get; set; }

        public override int Execute(CancellationToken cancellationToken)
        {
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("OpenTap.Sdk.New.Resources.TapPlanTemplate.txt")))
            {
                StringBuilder steps = new StringBuilder("\n");
                var ns = TryGetNamespace();
                var csFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.cs", SearchOption.TopDirectoryOnly);
                foreach (var file in csFiles)
                {
                    var text = File.ReadAllText(file);
                    var match = Regex.Match(text, "public class (.*?) : I?TestStep");
                    if (match.Success)
                        steps.AppendLine($"    <TestStep type=\"{ns}.{match.Groups[1].Value}\"></TestStep>");
                }

                var content = ReplaceInTemplate(reader.ReadToEnd(), steps.ToString());
                WriteFile(output ?? Path.Combine(Directory.GetCurrentDirectory(), Name + ".TapPlan"), content);
            }

            return 0;
        }
    }
    [Display("cliaction", "C# template for a CliAction plugin. Requires a project.", Groups: new[] { "sdk", "new" })]
    public class GenerateCliAction : GenerateType
    {
        [UnnamedCommandLineArgument("name", Required = true)]
        public string Name { get; set; }

        public override int Execute(CancellationToken cancellationToken)
        {
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("OpenTap.Sdk.New.Resources.CliActionTemplate.txt")))
            {
                var content = ReplaceInTemplate(reader.ReadToEnd(), TryGetNamespace(), Name);
                WriteFile(output ?? Path.Combine(Directory.GetCurrentDirectory(), Name + ".cs"), content);
            }

            return 0;
        }
    }
    [Display("packagexml", "Package Definition file (package.xml).", Groups: new[] { "sdk", "new" })]
    public class GeneratePackageXml : GenerateType
    {
        [UnnamedCommandLineArgument("package name", Required = true)]
        public string Name { get; set; }

        public override int Execute(CancellationToken cancellationToken)
        {
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("OpenTap.Sdk.New.Resources.PackageXmlTemplate.txt")))
            {
                var content = ReplaceInTemplate(reader.ReadToEnd(), TryGetNamespace(), Name);
                WriteFile(output ?? Path.Combine(Directory.GetCurrentDirectory(), "package.xml"), content);
            }

            return 0;
        }
    }

    // SerializerPlugin
}
