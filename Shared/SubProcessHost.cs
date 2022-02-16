using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using OpenTap.Diagnostic;

namespace OpenTap
{
    /// <summary>
    /// This is an abstraction for running child processes with support for elevation.
    /// It executes a test step (which can have child test steps) in a new process
    /// It supports subscribing to log events from the child process, and forwarding the logs directly.
    /// </summary>
    internal class SubProcessHost
    {
        private bool forwardLogs = false;
        public SubProcessHost(bool forwardLogs)
        {
            this.forwardLogs = forwardLogs;
        }

        public static bool IsAdmin()
        {
            if (OperatingSystem.Current == OperatingSystem.Windows)
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            else // assume UNIX
            {
                // id -u should print '0' if running as sudo or the current user is root
                var pInfo = new ProcessStartInfo()
                {
                    FileName = "id",
                    Arguments = "-u",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                };

                using (var p = Process.Start(pInfo))
                {
                    if (p == null) return false;
                    var output = p.StandardOutput.ReadToEnd().Trim();
                    if (int.TryParse(output, out var id) && id == 0)
                        return true;
                    return false;
                }
            }
        }

        private static string GetTap()
        {
            var currentProcess = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrWhiteSpace(currentProcess) == false &&
                string.Equals(Path.GetFileNameWithoutExtension(currentProcess), "tap",
                    StringComparison.CurrentCultureIgnoreCase))
                return currentProcess;

            if (OperatingSystem.Current == OperatingSystem.Windows)
                return Path.Combine(ExecutorClient.ExeDir, "tap.exe");
            return Path.Combine(ExecutorClient.ExeDir, "tap");
        }

        private static void LogLine(Event evt)
        {
            var childLog = Log.CreateSource("Subprocess: " + evt.Source);
            var message = evt.Message;

            switch (evt.EventType)
            {
                case 10:
                    childLog.Error(message);
                    break;
                case 20:
                    childLog.Warning(message);
                    break;
                case 30:
                    childLog.Info(message);
                    break;
                case 40:
                    childLog.Debug(message);
                    break;
            }
        }

        private static TraceSource log = Log.CreateSource(nameof(SubProcessHost));
        internal Process LastProcessHandle;

        private static readonly object StdoutLock = new object();
        private static bool stdoutSuspended;
        private static TextWriter originalOut;
        private static StringWriter tmpOut;
        private static void SuspendStdout()
        {
            lock(StdoutLock)
            {
                if (stdoutSuspended) return;
                originalOut = Console.Out;
                tmpOut = new StringWriter();
                Console.SetOut(tmpOut);
                originalOut.Flush();
                stdoutSuspended = true;
            }
        }
        private static void ResumeStdout()
        {
            lock(StdoutLock)
            {
                if (stdoutSuspended == false) return;
                // Restore stdout after the server has connected
                Console.SetOut(originalOut);
                var lines = tmpOut.ToString()
                    .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    Console.WriteLine(line);
                }

                stdoutSuspended = false;
                tmpOut.Dispose();
                tmpOut = null;
            }
        }

        public Verdict Run(ITestStep step, bool elevate, CancellationToken token)
        {
            var handle = Guid.NewGuid().ToString();
            var pInfo = new ProcessStartInfo(GetTap())
            {
                Arguments = $"{nameof(ProcessCliAction)} --PipeHandle \"{handle}\"",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            if (elevate)
            {
                if (OperatingSystem.Current == OperatingSystem.Linux)
                {
                    // -E preserves environment variables
                    pInfo.Arguments = $"-E \"{pInfo.FileName}\" {pInfo.Arguments}";
                    pInfo.FileName = "sudo";
                    if (SudoHelper.IsSudoAuthenticated() == false)
                        if (SudoHelper.Authenticate() == false)
                            throw new Exception($"User failed to authenticate as sudo.");
                }
                else
                {
                    pInfo.Verb = "runas";
                    pInfo.UseShellExecute = true;
                    pInfo.RedirectStandardOutput = false;
                    pInfo.RedirectStandardError = false;
                }
            }
            
            SuspendStdout();

            using var p = Process.Start(pInfo);
            LastProcessHandle = p ?? throw new Exception($"Failed to spawn process.");

            // Ensure the process is cleaned up
            TapThread.Current.AbortToken.Register(() =>
            {
                if (p.HasExited) return;

                try
                {
                    // process.Kill may throw if it has already exited.
                    p.Kill();
                }
                catch (Exception ex)
                {
                    log.Warning("Caught exception when killing process. {0}", ex.Message);
                }
            });

            try
            {
                var server = new NamedPipeServerStream(handle, PipeDirection.InOut, 1);
                server.WaitForConnectionAsync(token).GetAwaiter().GetResult();
                if (token.IsCancellationRequested)
                    throw new OperationCanceledException($"Process cancelled by the user.");
                // Resume stdout after the server has connected as we now know the application has launched
                ResumeStdout();

                server.WriteMessage(step);

                while (server.IsConnected && p.HasExited == false)
                {
                    if (token.IsCancellationRequested)
                        throw new OperationCanceledException($"Process cancelled by the user.");

                    var msgs = server.ReadMessage<Event[]>();
                    if (forwardLogs)
                    {
                        foreach (var evt in msgs)
                            LogLine(evt);
                    }
                }

                var processExitTask = Task.Run(() => p.WaitForExit(), token);
                var tokenCancelledTask = Task.Run(() => token.WaitHandle.WaitOne(), token);

                Task.WaitAny(processExitTask, tokenCancelledTask);
                while (server.IsConnected)
                {
                    if (token.IsCancellationRequested)
                        throw new OperationCanceledException($"Process cancelled by the user.");

                    var msgs = server.ReadMessage<Event[]>();
                    if (forwardLogs)
                    {
                        foreach (var evt in msgs)
                            LogLine(evt);
                    }
                }
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException($"Process cancelled by the user.");
                }

                return (Verdict)p.ExitCode;
            }
            finally
            {
                ResumeStdout();
                if (p.HasExited == false)
                {
                    p.Kill();
                }
            }
        }
    }
}
