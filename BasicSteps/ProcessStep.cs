//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using OpenTap;

namespace OpenTap.Plugins.BasicSteps
{
    [Display("Run Program", Group: "Basic Steps", Description: "Runs a program, and optionally applies regular expressions (regex) to the output.")]
    public class ProcessStep : RegexOutputStep
    {
        public override bool GeneratesOutput { get { return WaitForEnd; } }

        [Display("Application", Order: -2.5, Description: "The path to the program. It should contain either a relative path to OpenTAP installation folder or an absolute path to the program.")]
        [FilePath(FilePathAttribute.BehaviorChoice.Open, "exe")]
        public string Application { get; set; }
        
        [Display("Command Line Arguments", Order: -2.4, Description: "The arguments passed to the program.")]
        public string Arguments { get; set; }
        
        [Display("Working Directory", Order: -2.3, Description: "The directory where the program will be started in.")]
        [DirectoryPath]
        public string WorkingDirectory { get; set; }

        [Display("Wait For Process to End", Order: -2.2, Description: "Wait for the process to terminate before continuing.")]
        public bool WaitForEnd { get; set; }
        
        int timeout = 0;
        [Display("Wait Timeout", Order: -2.1, Description: "The time to wait for the process to end. Set to 0 to wait forever.")]
        [Unit("s", PreScaling: 1000)]
        [EnabledIf("WaitForEnd", true)]
        public Int32 Timeout
        {
            get { return timeout; }
            set
            {
                if (value >= 0)
                    timeout = value;
                else throw new Exception("Timeout must be positive");
            }
        }

        [EnabledIf("GeneratesOutput", true)]
        [Display("Add to Log", Order: -2.05, Description: "If enabled the result of the query is added to the log.")]
        public bool AddToLog { get; set; }

        [EnabledIf("AddToLog", true)]
        [EnabledIf("GeneratesOutput", true)]
        [Display("Log Header", Order: -2.0, Description: "This string is added to the front of the result of the query.")]
        public string LogHeader { get; set; }

        [Display("Check Exit Code", "Check the exit code of the application and set verdict to fail if it is non-zero, else pass. 'Wait For End' must be set for this to work.", "Set Verdict", Order: 1.1)]
        [EnabledIf(nameof(WaitForEnd), true)]
        public bool CheckExitCode { get; set; }
        

        public ProcessStep()
        {    
            Application = "";
            Arguments = "";
            WorkingDirectory = Directory.GetCurrentDirectory();
            WaitForEnd = true;
        }

        private ManualResetEvent outputWaitHandle, errorWaitHandle;
        private StringBuilder output;

        public override void Run()
        {
            Int32 timeout = Timeout <= 0 ? Int32.MaxValue : Timeout;

            var process = new Process();
            process.StartInfo.FileName = Application;
            process.StartInfo.Arguments = Arguments;
            process.StartInfo.WorkingDirectory = Path.GetFullPath(WorkingDirectory);
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            var abortRegistration = TapThread.Current.AbortToken.Register(() =>
            {
                Log.Debug("Ending process '{0}'.", Application);
                process.Kill();
            });

            if (WaitForEnd)
            {
                output = new StringBuilder();
                
                using (outputWaitHandle = new ManualResetEvent(false))
                using (errorWaitHandle = new ManualResetEvent(false))
                using(process)
                using(abortRegistration)
                {
                    process.OutputDataReceived += OutputDataRecv;
                    process.ErrorDataReceived += ErrorDataRecv;

                    Log.Debug("Starting process {0} with arguments \"{1}\"", Application, Arguments);
                    process.Start();

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    var newlineArray = new [] {Environment.NewLine};

                    if (process.WaitForExit(timeout) &&
                        outputWaitHandle.WaitOne(timeout) &&
                        errorWaitHandle.WaitOne(timeout))
                    {
                        var resultData = output.ToString();

                        ProcessOutput(resultData);
                        if (CheckExitCode)
                        {
                            if (process.ExitCode != 0)
                                UpgradeVerdict(Verdict.Fail);
                            else
                                UpgradeVerdict(Verdict.Pass);
                        }
                    }
                    else
                    {
                        process.OutputDataReceived -= OutputDataRecv;
                        process.ErrorDataReceived -= ErrorDataRecv;

                        var resultData = output.ToString();

                        if (AddToLog)
                        {
                            foreach (var line in resultData.Split(newlineArray, StringSplitOptions.None))
                                Log.Info("{0} {1}", LogHeader, line);
                        }

                        ProcessOutput(resultData);

                        Log.Error("Timed out while waiting for application. Trying to kill process...");

                        process.Kill();
                        UpgradeVerdict(Verdict.Fail);
                    }
                }
            }
            else
            {
                TapThread.Start(() =>
                {
                    using (process)
                    using(abortRegistration)
                    {
                        process.Start();
                        process.WaitForExit();
                        abortRegistration.Dispose();
                    }
                });
            }
        }

        private void OutputDataRecv(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (e.Data == null)
                {
                    outputWaitHandle.Set();
                }
                else
                {
                    if(AddToLog)
                        Log.Info("{0}", e.Data);
                    lock(output)
                        output.AppendLine(e.Data);
                }
            }
            catch (ObjectDisposedException)
            {
                // Suppress - Testplan has been aborted and process is disconnected
            }
        }

        private void ErrorDataRecv(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (e.Data == null)
                {
                    errorWaitHandle.Set();
                }
                else
                {
                    if(AddToLog)
                        Log.Error("{0}", e.Data);
                    lock(output)
                        output.AppendLine(e.Data);
                }
            }
            catch (ObjectDisposedException)
            {
                // Suppress - Testplan has been aborted and process is disconnected
            }
        }
    }
}
