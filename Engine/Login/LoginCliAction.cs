using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Login
{
    /// <summary>
    /// Logs in to an Keycloak instance.
    /// </summary>
    [Display("login", Group: "web")]
    public class LoginCliAction : ICliAction
    {
        /// <summary> The user name</summary>
        [CommandLineArgument("username")] public string UserName { get; set; }
        /// <summary> The password</summary>
        [CommandLineArgument("password")] public string Password { get; set; }
        /// <summary> The URL for the keycloak instance</summary>
        [CommandLineArgument("url")] public string Url { get; set; }
        /// <summary> the client ID </summary>
        [CommandLineArgument("client-id")] public string ClientId { get; set; }
        /// <summary> The realm. </summary>
        [CommandLineArgument("realm")] public string Realm { get; set; }

        /// <summary> This can be used if the url does not contain the service site. </summary>
        [CommandLineArgument("site")] public string Site { get; set; }

        private static readonly TraceSource log = Log.CreateSource("web");

        /// <summary> Executes the action. </summary>
        public int Execute(CancellationToken cancellationToken)
        {
            var url = $"https://{Url}/auth/realms/{Realm}/protocol/openid-connect/token";
            var http = new HttpClient { };
            using (var content =
                   new FormUrlEncodedContent(new Dictionary<string, string>
                   {
                       {"username", UserName},
                       {"password", Password}, {"grant_type", "password"}, {"client_id", ClientId}
                   }))
            {
                var response = http.PostAsync(url, content, cancellationToken).Result;
                if (!response.IsSuccessStatusCode)
                {
                    var error = response.Content.ReadAsStringAsync().Result;
                    throw new Exception("Unable to connect: " + response.StatusCode);
                }

                Site = Site ?? Url.Replace("keycloak.", "");
                var responseString = response.Content.ReadAsStringAsync().Result;
                var json = System.Text.Json.JsonDocument.Parse(responseString);
                //"expires_in":300,"refresh_expires_in":1800
                var accessExp = DateTime.Now.AddSeconds(300);
                var refreshExp = DateTime.Now.AddSeconds(1600);
                if(json.RootElement.TryGetProperty("expires_in", out var exp1Str) && exp1Str.TryGetInt32(out var accessAdd))
                    accessExp = DateTime.Now.AddSeconds(accessAdd - 5);
                if(json.RootElement.TryGetProperty("refresh_expires_in", out exp1Str) && exp1Str.TryGetInt32(out var refreshAdd))
                    refreshExp = DateTime.Now.AddSeconds(refreshAdd - 5 );
                
                var access_token = json?.RootElement.GetProperty("access_token").GetString() ?? "";
                LoginInfo.Current.RegisterAccessToken(Site, access_token, accessExp);
                var refresh_token = json?.RootElement.GetProperty("refresh_token").GetString() ?? "";
                LoginInfo.Current.RegisterRefreshToken(Site, refresh_token, refreshExp);
                LoginInfo.Current.Save();
                log.Debug("Login successful.");
            }

            return 0;
        }
    }
}