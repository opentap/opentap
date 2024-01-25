//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap
{
    
    internal class ExecutorSubProcess : IDisposable
    {
        public class EnvVarNames
        {
            public static string TpmInteropPipeName = "TPM_PIPE";
            public static string ParentProcessExeDir = "TPM_PARENTPROCESSDIR";
            public static string OpenTapInitDirectory = "OPENTAP_INIT_DIRECTORY";
        }

        NamedPipeServerStream Pipe { get; set; }
        Process Process { get; set; }

        ProcessStartInfo start;

        public int WaitForExit()
        {
            if (Process != null)
            {
                Process.WaitForExit();
                return Process.ExitCode;
            }
            else
                throw new InvalidOperationException("Process has not been started");
        }

        public ExecutorSubProcess(ProcessStartInfo start)
        {
            this.start = start;
        }

        void ProcessPipe()
        {
            var token = tokenSource.Token;
            byte[] buffer = new byte[1024];

            while (true)
            {
                if (token.IsCancellationRequested)
                    return;
                var read = Pipe.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                var str = Encoding.UTF8.GetString(buffer, 0, read);
                if (MessageReceived != null)
                    MessageReceived(this, str);
                Array.Clear(buffer, 0, read);
            }
        }

        public event EventHandler<string> MessageReceived;


        CancellationTokenSource tokenSource = new CancellationTokenSource();

        public static ExecutorSubProcess Create(string name, string args, bool isolated = false)
        {
            var start = new ProcessStartInfo(name, args)
            {
                WorkingDirectory = Path.GetFullPath(Directory.GetCurrentDirectory()),
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (isolated)
            {
                start.Environment[EnvVarNames.ParentProcessExeDir] = ExecutorClient.ExeDir;
            }
            return new ExecutorSubProcess(start);
        }

        public static NamedPipeServerStream getStream(out string pipeName)
        {
            Start:
            string pipename = pipeName = Guid.NewGuid().ToString().Replace("-", "");
            try
            {
                return new NamedPipeServerStream(pipename, PipeDirection.In, 10);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(100);
                goto Start;
            }
        }

        public void Start()
        {
            Console.WriteLine("STARTING SUBPROCESS: tap.exe " + start.Arguments + " @ " + start.WorkingDirectory);
            string pipeName;
            Pipe = getStream(out pipeName);
            start.Environment[EnvVarNames.TpmInteropPipeName] = pipeName;

            var sem = new Semaphore(0, 1);
            new Thread(() =>
            {
                sem.WaitOne();
                Pipe.WaitForConnection();
                ProcessPipe();
            }).Start();

            Process = Process.Start(start);
            sem.Release();
            Process.EnableRaisingEvents = true;

            new Thread(() => RedirectOutput(Process.StandardOutput, Console.Out.Write)).Start();
            new Thread(() => RedirectOutput(Process.StandardError, Console.Error.Write)).Start();
        }

        void RedirectOutput(StreamReader reader, Action<string> callback)
        {
            char[] buffer = new char[256];
            int count;

            while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                callback(new string(buffer, 0, count));
            }
        }

        public void Dispose()
        {
            tokenSource.Cancel();
            if (Pipe != null)
                Pipe.Dispose();
            if (Process != null)
                Process.Dispose();
        }

        public void Env(string Name, string Value)
        {
            start.Environment[Name] = Value;
        }
    }


    internal class ExecutorClient : IDisposable
    {
        /// <summary>
        /// Is this process an isolated sub process of tap.exe
        /// </summary>
        public static bool IsRunningIsolated => Environment.GetEnvironmentVariable(ExecutorSubProcess.EnvVarNames.ParentProcessExeDir) != null;
        /// <summary>
        /// Is this process a sub process of tap.exe
        /// </summary>
        public static bool IsExecutorMode => Environment.GetEnvironmentVariable(ExecutorSubProcess.EnvVarNames.TpmInteropPipeName) != null;
        /// <summary>
        /// The directory containing the OpenTAP installation.
        /// This is usually the value of the environment variable OPENTAP_INIT_DIRECTORY set by tap.exe
        /// If this value is not set, use the location of OpenTap.dll instead
        /// In some cases, when running isolated this is that value but from the parent process.
        /// </summary>
        public static string ExeDir
        {
            get
            {
                if (IsRunningIsolated)
                    return Environment.GetEnvironmentVariable(ExecutorSubProcess.EnvVarNames.ParentProcessExeDir);
                else
                {
                    var exePath = Environment.GetEnvironmentVariable(ExecutorSubProcess.EnvVarNames.OpenTapInitDirectory);
                    if (exePath != null)
                        return exePath;

                    // Referencing OpenTap.dll causes the file to become locked.
                    // Ensure OpenTap.dll is only loaded if the environment variable is not set.
                    // This should only happen if OpenTAP was not loaded through tap.exe.
                    return GetOpenTapDllLocation();
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string GetOpenTapDllLocation() => Path.GetDirectoryName(typeof(PluginSearcher).Assembly.Location);

        Task pipeConnect;
        PipeStream pipe;

        public ExecutorClient()
        {
            var pipename = Environment.GetEnvironmentVariable(ExecutorSubProcess.EnvVarNames.TpmInteropPipeName);
            if (pipename != null)
            {
                var pipe2 = new NamedPipeClientStream(".", pipename, PipeDirection.Out, PipeOptions.WriteThrough);
                pipeConnect = pipe2.ConnectAsync();
                pipe = pipe2;
            }
        }

        public void Dispose()
        {
            pipe.Dispose();
        }

        internal void MessageServer(string newname)
        {
            if (!pipeConnect.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Isolated process failed to connect to host process within reasonable time.");
            }
            var toWrite = Encoding.UTF8.GetBytes(newname);
            pipe.Write(toWrite, 0, toWrite.Length);
            pipe.Flush();
        }
    }
}
