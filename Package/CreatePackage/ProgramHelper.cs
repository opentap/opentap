using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace OpenTap.Package
{
    internal static class ProgramHelper
    {
        private static OpenTap.TraceSource log = Log.CreateSource("Program");

        internal static int RunProgram(string program, string arguments)
        {
            StringBuilder stdOut = new StringBuilder();
            StringBuilder stdErr = new StringBuilder();

            stdOut.AppendLine($"Running '{program}' with '{arguments}'");

            ProcessStartInfo pi = new ProcessStartInfo(program, arguments);
            pi.CreateNoWindow = true;
            pi.UseShellExecute = false;
            pi.RedirectStandardError = true;
            pi.RedirectStandardOutput = true;

            var p = Process.Start(pi);

            p.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) stdErr.AppendLine(e.Data); };
            p.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) stdOut.AppendLine(e.Data); };

            p.BeginErrorReadLine();
            p.BeginOutputReadLine();

            p.WaitForExit();

            try
            {
                if (p.ExitCode != 0)
                {
                    var ex = new Exception("Error during run program: " + p.ExitCode);

                    ex.Data["StdOut"] = stdOut.ToString();
                    ex.Data["StdErr"] = stdErr.ToString();

                    throw ex;
                }

                return p.ExitCode;
            }
            finally
            {
                p.Close();
            }
        }

        internal static void FileCopy(string source, string destination)
        {
            const int RetryTimeoutMS = 10;

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    File.Copy(source, destination, true);
                    break;
                }
                catch
                {
                    System.Threading.Thread.Sleep(RetryTimeoutMS);
                    if (i == 9)
                        throw;
                }
            }

        }
    }
}
