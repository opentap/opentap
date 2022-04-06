using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using OpenTap.Authentication;

namespace OpenTap.Cli
{
    /// <summary>
    /// Logs in to an OAuth provider host.
    /// </summary>
    [Display("login", Group: "auth", Description: "Logs in to an OAuth provider.")]
    public class LoginCliAction : ICliAction
    {
        static readonly TraceSource log = Log.CreateSource("web");
        
        /// <summary> The user name</summary>
        [CommandLineArgument("username", Description = "The username to log in with.")] public string UserName { get; set; }
        /// <summary> The password</summary>
        [CommandLineArgument("password", Description="The password used to log in with.")] public string Password { get; set; }
        /// <summary> The URL for the keycloak instance</summary>
        [CommandLineArgument("authority", Description = "HTTPS url to the OAuth authority provider. ")] public string Authority { get; set; }
        /// <summary> the client ID </summary>
        [CommandLineArgument("client-id", Description = "The client ID for the application.")] public string ClientId { get; set; }
        /// <summary> This can be used if the url does not contain the service site. </summary>
        [CommandLineArgument("domain", Description="The domain to log into. " +
                                                   "Be default, this is the same as 'authority', but these will often be different services.")] 
        public string Domain { get; set; }

        /// <summary> Executes the action. </summary>
        public int Execute(CancellationToken cancellationToken)
        {
            this.MustBeDefined(nameof(Authority));
            this.MustBeDefined(nameof(UserName));
            this.MustBeDefined(nameof(Password));
            this.MustBeDefined(nameof(ClientId));
            
            Domain ??= new Uri(Authority).Host;
            
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
                
                
                var responseString = response.Content.ReadAsStringAsync().Result;
                var tokens = TokenInfo.ParseTokens(responseString, Domain);
                AuthenticationSettings.Current.RegisterTokens(tokens.ToArray());
                AuthenticationSettings.Current.Save();
                log.Debug("Login successful.");
            }

            return 0;
        }
    }
}