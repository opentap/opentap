using System;
using System.Threading;
using OpenTap.Authentication;
using OpenTap.Cli;
namespace OpenTap
{
    /// <summary> Sets the authentication token for use with a given domain.  </summary>
    //[Display("login",Description: "Log in to a online service. Only supports setting the token directly using '--token'.")]
    // LoginAction currently disabled, but kept around in case we need it in the future.
    class LoginAction// : ICliAction
    {
        /// <summary>  Gets or sets the domain. </summary>
        [UnnamedCommandLineArgument("url")]
        [Display("url")]
        
        public string Url { get; set; }

        /// <summary>  Gets or sets the token. </summary>
        [CommandLineArgument("token")]
        public string Token { get; set; }
       
        static readonly TraceSource log = Log.CreateSource("login");
        
        /// <summary> Logs in to the url. </summary>
        public int Execute(CancellationToken cancellationToken)
        {
            // We currently only support by 'logging in' by setting the token directly
            // using --token. In the future we can add support for letting the server tell us how to 
            // log in through the browser.
            if (string.IsNullOrEmpty(Token))
            {
                log.Error("Currently only --token is a supported method of login");
                return -1;
            }
            var uri = new Uri(Url, UriKind.RelativeOrAbsolute);
            var authority = uri.IsAbsoluteUri ? uri.Authority : Url;
            log.Debug($"Selecting {authority} as authority.");
            
            AuthenticationSettings.Current.Tokens.RemoveIf(x => x.Domain == authority);
            AuthenticationSettings.Current.Tokens.Add(new TokenInfo(Token, "", authority));
            AuthenticationSettings.Current.Save();
            log.Info($"Successfully set the authentication token for {Url}.");
            return 0;
        }
    }
}
