using System;
using System.Linq;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Login
{
    [Display("list-login", Group: "web", Description: "List the currently active logins.")]
    public class ListLogins : ICliAction
    {
        private static TraceSource log = Log.CreateSource("web");

        public int Execute(CancellationToken cancellationToken)
        {
            log.Debug("Listing logins");
            var tokens = LoginInfo.Current.AccessTokens.Concat(LoginInfo.Current.RefreshTokens).ToArray();
            foreach (var token in tokens)
            {
                //if ()
                //    continue;
                log.Info("{2}: {0}  (Expires: {1}{3})", token.Site, token.Expiration, token.Type, token.Expired ? " - expired" : "");
            }

            return 0;
        }
    }
}