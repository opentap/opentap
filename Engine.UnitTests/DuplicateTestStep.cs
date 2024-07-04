using System;
using System.ComponentModel;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.TestTestSteps
{
    public class DuplicateCliAction : ICliAction
    {
        public int Execute(CancellationToken cancellationToken)
        {
            return 0;
        }
    }

    public class DuplicateTestStep : TestStep
    {
        public override void Run()
        {

        }
    }
}
