using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Authentication
{
    /// <summary>
    /// Logs in to an OAuth provider host.
    /// </summary>
    [Display("login", Group: "auth")]
    public class LoginCliAction : ICliAction
    {
        static readonly TraceSource log = Log.CreateSource("web");
        
        /// <summary> The user name</summary>
        [CommandLineArgument("username")] public string UserName { get; set; }
        /// <summary> The password</summary>
        [CommandLineArgument("password")] public string Password { get; set; }
        /// <summary> The URL for the keycloak instance</summary>
        [CommandLineArgument("authority")] public string Authority { get; set; }
        /// <summary> the client ID </summary>
        [CommandLineArgument("client-id")] public string ClientId { get; set; }
        /// <summary> This can be used if the url does not contain the service site. </summary>
        [CommandLineArgument("domain")] public string Domain { get; set; }

        /// <summary> Executes the action. </summary>
        public int Execute(CancellationToken cancellationToken)
        {
            var url = $"{Authority}/protocol/openid-connect/token";
            var http = new HttpClient { };
            var contentDict = new Dictionary<string, string>
            {
                {"username", UserName},
                {"password", Password},
                {"grant_type", "password"},
                {"client_id", ClientId}
            };
            using (var content = new FormUrlEncodedContent(contentDict))
            {
                
                var response = http.PostAsync(url, content, cancellationToken).Result;
                if (!response.IsSuccessStatusCode)
                    throw new Exception("Unable to connect: " + response.StatusCode);
                
                Domain = Domain ?? new Uri(Authority).Host;
                var responseString = response.Content.ReadAsStringAsync().Result;
                TokenInfo.ParseTokens(responseString, Domain, out var access, out var refresh);
                LoginInfo.Current.RegisterTokens(access, refresh);
                LoginInfo.Current.Save();
                log.Debug("Login successful.");
            }

            return 0;
        }
    }
}