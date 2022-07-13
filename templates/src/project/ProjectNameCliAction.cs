using System;
using System.Threading;
using OpenTap;
using OpenTap.Cli;

namespace ProjectName
{
    [Display("ProjectNameCliAction", "Description of action.", "ProjectName")]
    public class ProjectNameCliAction : ICliAction
    {
        public int Execute(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}