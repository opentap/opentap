using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace OpenTap
{
    [Display("Sudo Password Prompt")]
    internal class PasswordPrompt
    {
        [Layout(LayoutMode.FloatBottom)]
        [Display("Sudo Password")]
        [Submit] public string Response { get; set; } = "";
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
            UserInput.Request(passwordQuestion, true);
            if (string.IsNullOrWhiteSpace(passwordQuestion.Response))
                return false;
            var p = Process.Start(new ProcessStartInfo("sudo")
            {
                Arguments = "-vS",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (p == null) throw new Exception($"Sudo is not installed.");
            p.StandardInput.WriteLine(passwordQuestion.Response);
            p.WaitForExit(100);
            if (p.HasExited == false) p.Kill();
            return p.HasExited && p.ExitCode == 0;
        }
    }
}