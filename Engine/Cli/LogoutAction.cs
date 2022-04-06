using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using OpenTap.Authentication;

namespace OpenTap.Cli
{
    /// <summary> Logout action. </summary>
    [Display("logout", "Logs out of an OAuth domain.", Group: "auth")]
    [Browsable(false)]
    public class LogoutAction : ICliAction
    {
        static readonly TraceSource log = Log.CreateSource("web");
        
        /// <summary> The domain to log out of.</summary>
        [CommandLineArgument("domain")] public string Domain { get; set; }
        
        /// <summary> Executes the action. </summary>
        public int Execute(CancellationToken cancellationToken)
        {
            this.MustBeDefined(nameof(Domain));
            var token = AuthenticationSettings.Current.RefreshTokens.FirstOrDefault(x => x.Domain == Domain);
            if(token != null){
                var refreshToken = token.TokenData;
                var clientId = token.GetClientId();
                var url = $"{token.GetAuthority()}/protocol/openid-connect/logout";
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

                }
            }
            //AuthenticationSettings.Current.UnregisterAccessToken(Domain);
            //AuthenticationSettings.Current.UnregisterRefreshToken(Domain);
            //AuthenticationSettings.Current.Save();
            log.Debug("Logout successful.");
            return 0;
        }
    }
}