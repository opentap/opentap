//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using System.IO;
namespace OpenTap.Engine.UnitTests
{
    public class TapProcessContainer
    {
        public Process TapProcess;
        public string ConsoleOutput = "";
        Task consoleListener;
        void go()
        {
            TapProcess.Start();
            var consoleOutput = new StringBuilder();
            
            var procOutput = TapProcess.StandardOutput;
            var procOutput2 = TapProcess.StandardError;

            async Task asyncReader(StreamReader read)
            {
                char[] buffer = new char[100];
                while (!read.EndOfStream)
                {
                    int read2 = await read.ReadAsync(buffer, 0, buffer.Length);
                    if (read2 == -1)
                        break;
                    lock(consoleOutput)
                        consoleOutput.Append(buffer, 0, read2);
                }
            }
            
            async Task consoleOutputLoader()
            {
                await Task.WhenAll(asyncReader(procOutput), asyncReader(procOutput2));
                ConsoleOutput = consoleOutput.ToString();
            }
            consoleListener = consoleOutputLoader();
        }

        public static TapProcessContainer StartFromArgs(string args) => StartFromArgs(args, TimeSpan.FromMinutes(2));
            
        public static TapProcessContainer StartFromArgs(string args, TimeSpan timeOutAfter)
        {
            Process proc = new Process();

            var container = new TapProcessContainer { TapProcess = proc };


            var file = Path.GetDirectoryName(typeof(PluginManager).Assembly.Location);
            var files = new[] { Path.Combine(file, "tap.exe"), Path.Combine(file, "tap"), Path.Combine(file, "tap.dll") };
            global::OpenTap.Log.CreateSource("test").Debug($"location: {file}");
            var program = files.First(File.Exists);
            if (program.Contains(".dll"))
            {
                program = "dotnet";
                args = $"\"{file}/tap.dll\" " + args;
            }

            proc.StartInfo = new ProcessStartInfo(program, args)
            {
                UseShellExecute = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            proc.StartInfo.UseShellExecute = false;

            container.go();
            TapThread.Start(() =>
            {
                TapThread.Sleep(timeOutAfter);
                proc.Kill();
            });
            return container;
        }

        public void WaitForEnd()
        {
            consoleListener.Wait();
            TapProcess.WaitForExit();
        }

        public void WriteLine(string str)
        {
            TapProcess.StandardInput.WriteLine(str);
        }

        public void Kill()
        {
            try
            {
                TapProcess.Kill();
            }
            catch
            {

            }
        }
    }
}
