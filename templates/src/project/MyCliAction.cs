using System;
using System.Threading;
using OpenTap;
using OpenTap.Cli;

namespace ProjectName
{
    [Display("MyCliAction", Description: "Insert a description here", Group: "ProjectName")]
    public class MyCliAction : ICliAction
    {
        public int Execute(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}