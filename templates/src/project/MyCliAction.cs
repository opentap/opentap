using System;
using System.Threading;
using OpenTap;
using OpenTap.Cli;

namespace ProjectName
{
    [Display("MyCliAction", "Description of action.", "ProjectName")]
    public class MyCliAction : ICliAction
    {
        public int Execute(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}