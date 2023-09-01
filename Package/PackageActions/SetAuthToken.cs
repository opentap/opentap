using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using OpenTap.Authentication;
using OpenTap.Cli;
namespace OpenTap.Package
{
    /// <summary> Sets the authentication token for use with a given domain.  </summary>
    [Display("set-auth-token", Group: "package", Description: "Sets the authentication token for a given domain.")]
    public class SetAuthToken : ICliAction
    {
        /// <summary>  Gets or sets the domain. </summary>
        [UnnamedCommandLineArgument("url")]
        public string Url { get; set; }

        /// <summary>  Gets or sets the token. </summary>
        [UnnamedCommandLineArgument("token")]
        public string Token { get; set; }
        
        /// <summary>  Gets or sets update - if existing settings should be updated. </summary>
        [CommandLineArgument("update", ShortName = "u", Description = "Sets if existing settings should be updated.")]
        public bool Update { get; set; }

        static readonly TraceSource log = Log.CreateSource("set-auth-token");
        
        public int Execute(CancellationToken cancellationToken)
        {
            var uri = new Uri(Url, UriKind.RelativeOrAbsolute);
            var authority = uri.IsAbsoluteUri ? uri.Authority : Url;
            log.Debug($"Selecting {authority} as authority.");
            if (AuthenticationSettings.Current.Tokens.Any(x => x.Domain == authority))
            {
                if (!Update)
                {
                    log.Error($"Settings already contains a token for {authority}. Use --update to overwrite it.");
                    return -1;
                }
            }
            
            AuthenticationSettings.Current.Tokens.RemoveIf(x => x.Domain == authority);
            AuthenticationSettings.Current.Tokens.Add(new TokenInfo(Token, "", authority));
            AuthenticationSettings.Current.Save();
            log.Info($"Successfully set the authentication token for {Url}.");
            return 0;
        }
    }
}
