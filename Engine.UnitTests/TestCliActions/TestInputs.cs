using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
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

        [Display("Validate Prompt")]
        class SubmitValidatePrompt
        {
            public double Delay { get; set; }
            
            [Layout(LayoutMode.FloatBottom)]
            [Display("Normal Input")]
            [Submit(nameof(Validate))] 
            public string Response { get; set; } = "";

            public async Task Validate()
            {
                await Task.Delay(TimeSpan.FromSeconds(Delay));
                
                if (string.IsNullOrEmpty(Response))
                    throw new UserInputRetryException("Response not set", new string[]{});
            }
        }
        
        [CommandLineArgument("secure")]
        public bool Secure { get; set; }
        
        [CommandLineArgument("validate")]
        public bool Validate { get; set; }
        
        public int Execute(CancellationToken cancellationToken)
        {
            string written;
            if (Secure)
            {
                var x = new PasswordPrompt();
                UserInput.Request(x);
                written = x.Response.ConvertToUnsecureString();
            }
            else if (Validate)
            {
                var x = new SubmitValidatePrompt();
                UserInput.Request(x);
                written = x.Response;
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