using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Authentication
{
    /// <summary> Refreshes tokens. </summary>
    [Display("refresh-token", Group:"auth", Description: "Refresh one or more tokens.")]
    public class RefreshTokenAction : ICliAction
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

            if (Token == null)
                Token = LoginInfo.Current.RefreshTokens.FirstOrDefault(x => x.Domain == Domain);
            if (Token == null)
                throw new Exception("No refresh token found matching domain: " + Domain);
            var refreshToken = Token.TokenData;
            var clientId = Token.GetClientId();
            var url = $"{Token.GetAuthority()}/protocol/openid-connect/token";
            
            var http = new HttpClient();
            using (var content =
                   new FormUrlEncodedContent(new Dictionary<string, string>
                   {
                       {"refresh_token", refreshToken},
                       {"grant_type", "refresh_token"}, 
                       {"client_id", clientId}
                   }))
            {
                var response = http.PostAsync(url, content, cancellationToken).Result;
                if (!response.IsSuccessStatusCode)
                    throw new Exception("Unable to connect: " + response.StatusCode);

                var responseString = response.Content.ReadAsStringAsync().Result;
                TokenInfo.ParseTokens(responseString, Domain, out var access, out var refresh);
                if (access == null) throw new Exception("No access token in response.");
                if (refresh == null) throw new Exception("No refresh token in response.");
                LoginInfo.Current.RegisterTokens(access, refresh);
                LoginInfo.Current.Save();
                log.Debug("Token refreshed.");
            }

            return 0;
        }
    }
}