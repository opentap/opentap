using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Authentication
{
    /// <summary> Refreshes tokens. </summary>
    [Display("refresh-token", Group:"auth", Description: "Refresh one or more tokens.")]
    [Browsable(false)]
    class RefreshTokenAction : ICliAction
    {
        static readonly TraceSource log = Log.CreateSource("web");
        
        /// <summary> The domain that the refresh token applies to.</summary>
        [CommandLineArgument("domain", Description = "The site to refresh tokens for. If null, all tokens will be refreshed.")] 
        public string Domain { get; set; }
        /// <summary> The token. Set this if using this class as an API. </summary>
        public TokenInfo Token { get; set; }
        
        /// <summary> Executed the action. </summary>
        public int Execute(CancellationToken cancellationToken)
        {
            if (Domain == null)
            {
                log.Debug("Site not specified. Refreshing all tokens.");
                var refresh = LoginInfo.Current.RefreshTokens.ToArray();
                foreach (var r in refresh)
                {
                    Token = r;
                    Domain = r.Domain;
                    Execute(cancellationToken);
                }
                return 0;
            }

            return 0;
        }
    }
}