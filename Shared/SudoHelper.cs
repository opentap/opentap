using System;
using System.Diagnostics;
using System.Security;

namespace OpenTap
{
    [Display("Sudo Password Prompt")]
    internal class PasswordPrompt
    {
        [Layout(LayoutMode.FloatBottom)]
        [Display("Sudo Password")]
        [Submit] public SecureString Response { get; set; } = new SecureString();
    }
    
    /// <summary>
    /// This class contains some basic helpers to authenticate using the 'sudo' program
    /// </summary>
    internal class SudoHelper
    {
        public static bool IsSudoAuthenticated()
        {
            var p = Process.Start(new ProcessStartInfo("sudo")
            {
                Arguments = "-vn",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (p == null) throw new Exception($"Sudo is not installed.");
            p.WaitForExit();
            return p.ExitCode == 0;
        }

        public static bool Authenticate()
        {
            var passwordQuestion = new PasswordPrompt();
            try
            {
                UserInput.Request(passwordQuestion, TimeSpan.FromMinutes(2), true);
            }
            catch(TimeoutException)
            {
                throw new TimeoutException("Request timed while waiting for password input.");
            }

            if (string.IsNullOrWhiteSpace(passwordQuestion.Response.ConvertToUnsecureString()))
                return false;
            var p = Process.Start(new ProcessStartInfo("sudo")
            {
                Arguments = "-vS",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (p == null) throw new Exception($"Sudo is not installed.");
            p.StandardInput.WriteLine(passwordQuestion.Response.ConvertToUnsecureString());
            p.WaitForExit(100);
            if (p.HasExited == false) p.Kill();
            return p.HasExited && p.ExitCode == 0;
        }
    }
}