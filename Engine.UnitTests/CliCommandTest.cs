using NUnit.Framework;
using OpenTap.Cli;
using System;
using System.Collections.Generic;
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
        public void test()
        {
            var root = new CliActionTree();
            Assert.IsTrue(root.GetSubCommand(Array.Empty<string>()) == null);
            Assert.IsTrue(root.GetSubCommand("".Split(' ')) == null);
            Assert.IsTrue(root.GetSubCommand("test".Split(' ')).Name == "test");
            Assert.IsTrue(root.GetSubCommand("test action".Split(' ')).Name == "action");
            Assert.IsTrue(root.GetSubCommand("test action testaction".Split(' ')).Name == "testaction");
            Assert.IsTrue(root.GetSubCommand("test action testaction arg".Split(' ')).Name == "testaction");
        }
    }

    [Display("testaction", Groups: new[] { "test", "action" }, Description:"Runs TestAction")]
    public class TestAction : ICliAction
    {
        [UnnamedCommandLineArgument("notrequired", Required = false)]
        public string NotRequiredArgument { get; set; }
        public int Execute(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
    
    [Display("action", Groups: new[] { "test" }, Description:"Runs TestAction2")]
    public class TestAction2 : ICliAction
    {
        public int Execute(CancellationToken cancellationToken)
        {
            Console.WriteLine("Executed action 2");
            return 0;
        }
    }
}
