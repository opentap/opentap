using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using OpenTap.Authentication;

namespace OpenTap.Cli
{
    /// <summary> Refreshes tokens. </summary>
    [Display("refresh-token", Group:"auth", Description: "Refresh one or more tokens.")]
    [Browsable(false)]
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
                var refresh = AuthenticationSettings.Current.RefreshTokens.ToArray();
                foreach (var r in refresh)
                {
                    Token = r;
                    Domain = r.Domain;
                    Execute(cancellationToken);
                }
                return 0;
            }

            if (Token == null)
                Token = AuthenticationSettings.Current.RefreshTokens.FirstOrDefault(x => x.Domain == Domain);
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
                {
                    var responseString2 = response.Content.ReadAsStringAsync().Result;
                    throw new Exception("Unable to connect: " + response.StatusCode + " reason: " + responseString2);
                }

                var responseString = response.Content.ReadAsStringAsync().Result;
                var tokens = TokenInfo.ParseTokens(responseString, Domain);
                var access = tokens.FirstOrDefault(x => x.Type == TokenType.AccessToken);
                var refresh = tokens.FirstOrDefault(x => x.Type == TokenType.RefreshToken);
                if (access == null) throw new Exception("No access token in response.");
                if (refresh == null) throw new Exception("No refresh token in response.");
                AuthenticationSettings.Current.RegisterTokens(access, refresh);
                AuthenticationSettings.Current.Save();
                log.Debug("Token refreshed.");
            }
            return 0;
        }
    }

    static class RequireExtensions
    {
        public static void MustBeDefined(this ICliAction obj, string propertyName)
        {
            if (null == TypeData.GetTypeData(obj).GetMember(propertyName).GetValue(obj))
                throw new ArgumentException(propertyName + " must be defined.");
        }
    }
}