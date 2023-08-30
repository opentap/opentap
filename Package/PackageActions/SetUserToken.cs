using System.ComponentModel;
using System.Linq;
using System.Threading;
using OpenTap.Authentication;
using OpenTap.Cli;
namespace OpenTap.Package
{
    /// <summary> Sets the authentication token for use with a given domain.  </summary>
    [Display("set-auth-token", Group: "package", Description: "Sets the authentication token for a given domain.")]
    [Browsable(false)]
    public class SetUserToken : ICliAction
    {
        /// <summary>  Gets or sets the domain. </summary>
        [UnnamedCommandLineArgument("Domain")]
        public string Domain { get; set; }

        /// <summary>  Gets or sets the token. </summary>
        [UnnamedCommandLineArgument("Token")]
        public string Token { get; set; }
        
        /// <summary>  Gets or sets update - if existing settings should be updated. </summary>
        [CommandLineArgument("update", ShortName = "u", Description = "Sets if existing settings should be updated.")]
        public bool Update { get; set; }

        static readonly TraceSource log = Log.CreateSource("set-auto-token");
        
        public int Execute(CancellationToken cancellationToken)
        {
            if (AuthenticationSettings.Current.Tokens.Any(x => x.Domain == Domain))
            {
                if (!Update)
                {
                    log.Error($"Settings already contains a token for {Domain}. Use --update to overwrite it.");
                    return -1;
                }
            }
            
            AuthenticationSettings.Current.Tokens.RemoveIf(x => x.Domain == Domain);
            AuthenticationSettings.Current.Tokens.Add(new TokenInfo(Token, "", Domain));
            AuthenticationSettings.Current.Save();
            log.Info($"Successfully set the authentication token for {Domain}.");
            return 0;
        }
    }
}
