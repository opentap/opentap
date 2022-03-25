using System;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Login
{
    /// <summary> Logout action. </summary>
    [Display("logout", "Logs out of a Keycloak instance.", Group: "web")]
    public class LogoutAction : ICliAction
    {
        /// <summary> The URL for the keycloak instance</summary>
        [CommandLineArgument("url")] public string Url { get; set; }
        
        private static readonly TraceSource log = Log.CreateSource("web");
        /// <summary>  Executes the action. </summary>
        public int Execute(CancellationToken cancellationToken)
        {
            if (Url == null)
                throw new ArgumentException("Not set", nameof(Url));
            LoginInfo.Current.UnregisterAccessToken(Url.Replace("keycloak.", ""));
            LoginInfo.Current.UnregisterRefreshToken(Url.Replace("keycloak.", ""));
            LoginInfo.Current.Save();
            log.Debug("Logout successful.");
            return 0;
        }
    }
}