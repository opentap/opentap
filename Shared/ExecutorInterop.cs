//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
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

        void pipeConnected()
        {
            
            byte[] buffer = new byte[1024];

            void readMessage()
            {
                Pipe.ReadAsync(buffer, 0, buffer.Length, tokenSource.Token)
                    .ContinueWith(tsk =>
                    {
                        if ((tsk.IsCanceled || tsk.IsFaulted) == false)
                            gotMessage(tsk.Result);
                    });
            }

            void gotMessage(int cnt)
            {
                if (tokenSource.IsCancellationRequested)
                    return;
                var str = Encoding.UTF8.GetString(buffer, 0, cnt);
                pushMessage(str);
                Array.Clear(buffer, 0, cnt);
                readMessage();
            }
            readMessage();
        }

        public event EventHandler<string> MessageReceived;

        void pushMessage(string msg)
        {
            if (MessageReceived != null)
                MessageReceived(this, msg);
        }

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
                return new NamedPipeServerStream(pipename, PipeDirection.In, 10, PipeTransmissionMode.Message, PipeOptions.WriteThrough);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(100);
                goto Start;
            }
        }

        public void Start()
        {
            string pipeName;
            Pipe = getStream(out pipeName);
            start.Environment[EnvVarNames.TpmInteropPipeName] = pipeName;
            Pipe.WaitForConnectionAsync().ContinueWith(_ => pipeConnected());
            Process = Process.Start(start);
            Process.EnableRaisingEvents = true;

            Task t1 = RedirectOutput(Process.StandardOutput, Console.Write);
            Task t2 = RedirectOutput(Process.StandardError, Console.Error.Write);
        }

        async Task RedirectOutput(StreamReader reader, Action<string> callback)
        {
            char[] buffer = new char[256];
            int count;

            while ((count = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
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
        /// The directory containing the executable that the user initially ran.
        /// This is usually the value of Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
        /// but in some cases, when running isolated this is that value but from the parent process.
        /// </summary>
        public static string ExeDir
        {
            get
            {
                if (IsRunningIsolated)
                    return Environment.GetEnvironmentVariable(ExecutorSubProcess.EnvVarNames.ParentProcessExeDir);
                else
                {
                    string exePath = Assembly.GetEntryAssembly()?.Location; // in unit tests GetEntryAssembly can apparently be null
                    if (String.IsNullOrEmpty(exePath))
                    {
                        // if the entry assembly was loaded from memory/bytes instead of from a file, it does not have a location.
                        // This is the case if the process was launched through tap.exe. 
                        // In that case just use the location of Engine.dll, it is the same
                        exePath = Assembly.GetExecutingAssembly().Location;
                    }
                    return Path.GetDirectoryName(exePath);
                }
            }
        }

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
            pipeConnect.Wait();
            var toWrite = Encoding.UTF8.GetBytes(newname);
            pipe.Write(toWrite, 0, toWrite.Length);
        }
    }
}
