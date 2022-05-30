using System.Security;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.UnitTests
{
    [Display("input", Group: "test")]
    public class TestInputs : ICliAction
    {
        [Display("Sudo Password Prompt")]
        class PasswordPrompt
        {
            [Layout(LayoutMode.FloatBottom)]
            [Display("Sudo Password")]
            [Submit] public SecureString Response { get; set; } = new SecureString();
        }
        
        [Display("Normal Prompt")]
        class NormalPrompt
        {
            [Layout(LayoutMode.FloatBottom)]
            [Display("Normal Input")]
            [Submit] public string Response { get; set; } = "";
        }

        
        [CommandLineArgument("secure")]
        public bool Secure { get; set; }
        
        public int Execute(CancellationToken cancellationToken)
        {
            string written;
            if (Secure)
            {
                var x = new PasswordPrompt();
                UserInput.Request(x);
                written = x.Response.ConvertToUnsecureString();
            }
            else
            {
                var x = new NormalPrompt();
                UserInput.Request(x);
                written = x.Response;
            }

            Log.CreateSource("CLI").Info("Written: '{0}'", written);
            
            return 0;
        }
    }
}