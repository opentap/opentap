using NUnit.Framework;
using OpenTap.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class CliCommandTest
    {
        [Test]
        public void CliActionTreeTest()
        {
            var root = new CliActionTree();
            Assert.IsTrue(root.GetSubCommand(Array.Empty<string>()) == null);
            Assert.IsTrue(root.GetSubCommand("".Split(' ')) == null);
            Assert.IsTrue(root.GetSubCommand("test".Split(' ')).First().Name == "test");
            Assert.IsTrue(root.GetSubCommand("test action".Split(' ')).First().Name == "action");
            Assert.IsTrue(root.GetSubCommand("test action testaction".Split(' ')).First().Name == "testaction");
            Assert.IsTrue(root.GetSubCommand("test action testaction arg".Split(' ')).First().Name == "testaction");
        }
    }

    [Display("testaction", Groups: new[] { "test", "action" }, Description: "Runs TestAction")]
    public class TestAction : ICliAction
    {
        [UnnamedCommandLineArgument("notrequired", Required = false)]
        public string NotRequiredArgument { get; set; }
        public int Execute(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    [Display("action2", Groups: new[] { "test" }, Description:"Runs TestAction2")]
    public class TestAction2 : ICliAction
    {
        [CommandLineArgument("other", Description = "Some option which does not override a common option", ShortName = "o")]
        public bool Other { get; set; }
        [CommandLineArgument("quiet", Description = "Some option which overrides a common option", ShortName = "h")]
        public string Quiet { get; set; }
        public int Execute(CancellationToken cancellationToken)
        {
            Console.WriteLine($"Executed action 2 with 'Quiet = {Quiet}'.");
            return (int)ExitCodes.Success;
        }
    }

    [Display("action3", Groups: new[] { "test" }, Description:"Cannot create instance")]
    public class TestAction3 : ICliAction
    {
        public TestAction3()
        {
            throw new LicenseException(GetType(), null, "No license installed!");
        }
        public int Execute(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
    
    [Display("testargaction", Groups: new[] { "test", "action" }, Description:"Runs TestAction")]
    public class TestArgCliACtion  : ICliAction
    {
        [CommandLineArgument("a")] public uint a { get; set; } = 0;
        [CommandLineArgument("b")] public ulong b { get; set; } = 0;
        // c overridden by --color - does not work.
        [CommandLineArgument("c")] public float c { get; set; } = 0;
        [CommandLineArgument("d")] public float d { get; set; } = 0;
        [CommandLineArgument("e")] public double e { get; set; } = 0;
        [CommandLineArgument("f")] public long f { get; set; } = 0;
        // aliased by --help
        [CommandLineArgument("h")] public int[] h { get; set; } = Array.Empty<int>();
        [CommandLineArgument("i")] public int[] i { get; set; } = Array.Empty<int>();

        [CommandLineArgument("verdict")] public Verdict[] Verdict { get; set; } = Array.Empty<Verdict>();
        
        public int Execute(CancellationToken cancellationToken)
        {
            Console.WriteLine("{0} {1} {2} {3} {4} {5} [{6}]", a, b, c, d, e, f, string.Join(" ", i));
            Console.WriteLine("Verdicts: {0} ", string.Join(" ", Verdict));
            return 0;
        }
    }
    
}
