using System.Linq;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Authentication
{
    /// <summary> Lists current logins, active or otherwise. </summary>
    [Display("list-logins", Group: "auth", Description: "List the currently active logins.")]
    class ListLogins : ICliAction
    {
        private static readonly TraceSource log = Log.CreateSource("web");

        /// <summary> Executes the action.</summary>
        public int Execute(CancellationToken cancellationToken)
        {
            log.Debug("Listing logins");
            var tokens = LoginInfo.Current.AccessTokens.Concat(LoginInfo.Current.RefreshTokens).ToArray();
            foreach (var token in tokens)
                log.Info("{2}: {0}  (Expires: {1}{3})", token.Domain, token.Expiration, token.Type, token.Expired ? " - expired" : "");

            return 0;
        }
    }
}