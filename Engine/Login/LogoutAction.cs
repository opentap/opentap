using System;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Authentication
{
    /// <summary> Logout action. </summary>
    [Display("logout", "Logs out of an OAuth domain.", Group: "auth")]
    public class LogoutAction : ICliAction
    {
        static readonly TraceSource log = Log.CreateSource("web");
        
        /// <summary> The domain to log out of.</summary>
        [CommandLineArgument("domain")] public string Domain { get; set; }
        
        /// <summary> Executes the action. </summary>
        public int Execute(CancellationToken cancellationToken)
        {
            this.MustBeDefined(nameof(Domain));
            LoginInfo.Current.UnregisterAccessToken(Domain);
            LoginInfo.Current.UnregisterRefreshToken(Domain);
            LoginInfo.Current.Save();
            log.Debug("Logout successful.");
            return 0;
        }
    }
}