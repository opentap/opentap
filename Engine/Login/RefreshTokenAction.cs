using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Login
{
    [Display("refresh-token", Group:"web")]
    public class RefreshTokenAction : ICliAction
    {
        [CommandLineArgument("site")] public string Site { get; set; }
        public string RefreshToken { get; set; }
        public TokenInfo Token { get; set; }
        
        private static TraceSource log = Log.CreateSource("web");

        public int Execute(CancellationToken cancellationToken)
        {

            if (Site == null)
            {
                log.Debug("Site not specified. Refreshing all tokens.");
                var refresh = LoginInfo.Current.RefreshTokens.ToArray();
                foreach (var r in refresh)
                {
                    Token = r;
                    Site = r.Site;
                    Execute(cancellationToken);
                }

                return 0;

            }

            if (Token == null)
                Token = LoginInfo.Current.RefreshTokens.FirstOrDefault(x => x.Site == Site);
            if (Token == null)
                throw new ArgumentNullException(nameof(Token));
            RefreshToken = Token.TokenData;
            var clientId = Token.GetClientId();
            var url = $"{Token.GetAuthUrl()}/protocol/openid-connect/token";
            
            var http = new HttpClient();
            using (var content =
                   new FormUrlEncodedContent(new Dictionary<string, string>
                   {
                       {"refresh_token", RefreshToken},
                       {"grant_type", "refresh_token"}, 
                       {"client_id", clientId}
                   }))
            {
                var response = http.PostAsync(url, content, cancellationToken).Result;
                if (!response.IsSuccessStatusCode)
                {
                    var error = response.Content.ReadAsStringAsync().Result;
                    throw new Exception("Unable to connect: " + response.StatusCode);
                }

                var responseString = response.Content.ReadAsStringAsync().Result;
                var json = System.Text.Json.JsonDocument.Parse(responseString);
                var accessExp = DateTime.Now.AddSeconds(300);
                var refreshExp = DateTime.Now.AddSeconds(1600);
                if(json.RootElement.TryGetProperty("expires_in", out var exp1Str) && exp1Str.TryGetInt32(out var accessAdd))
                    accessExp = DateTime.Now.AddSeconds(accessAdd - 5);
                if(json.RootElement.TryGetProperty("refresh_expires_in", out exp1Str) && exp1Str.TryGetInt32(out var refreshAdd))
                    refreshExp = DateTime.Now.AddSeconds(refreshAdd - 5 );
                
                var accessToken = json?.RootElement.GetProperty("access_token").GetString();
                if(accessToken != null)
                    LoginInfo.Current.RegisterAccessToken(Site, accessToken, accessExp);
                var refreshToken = json?.RootElement.GetProperty("refresh_token").GetString();
                if(refreshToken != null)
                    LoginInfo.Current.RegisterRefreshToken(Site, refreshToken, refreshExp);
                LoginInfo.Current.Save();
                log.Debug("Token refreshed.");
            }

            return 0;
        }
    }
}