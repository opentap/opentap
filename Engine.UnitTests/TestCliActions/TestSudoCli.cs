using System;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.UnitTests
{
    [Display("sudo")]
    
    public class TestSudoCli : ICliAction
    {
        private TraceSource log = Log.CreateSource("sudo");
        public int Execute(CancellationToken cancellationToken)
        {
            if (OperatingSystem.Current == OperatingSystem.Windows)
            {
                throw new Exception("This does not work on windows.");
            }
            
            if (SudoHelper.IsSudoAuthenticated())
            {
                log.Info("Already authenticated.");
                return 0;
            }
            if (SudoHelper.Authenticate())
            {
                log.Info("Authenticated!");
            }
            else
            {
                log.Info("Authentication failed");
                return 1;
            }
                
            return 0;
        }
    }
}