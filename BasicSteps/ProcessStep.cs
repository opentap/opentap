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

        [Display("Application", Group:"Common", Order: -2.5, Description: "The path to the program. It should contain either a relative path to TAP installation folder or an absolute path to the program.")]
        [FilePath(FilePathAttribute.BehaviorChoice.Open, "exe")]
        public string Application { get; set; }
        
        [Display("Command Line Arguments", Group: "Common", Order: -2.4, Description: "The arguments passed to the program.")]
        public string Arguments { get; set; }
        
        [Display("Working Directory", Group:"Common", Order: -2.3, Description: "The directory where the program will be started in.")]
        [DirectoryPath]
        public string WorkingDirectory { get; set; }

        [Display("Wait For Process to End", Group:"Common", Order: -2.2, Description: "Wait for the process to terminate before continuing.")]
        public bool WaitForEnd { get; set; }
        
        int timeout = 0;
        [Display("Wait Timeout", Group: "Common", Order: -2.1, Description: "The time to wait for the process to end. Set to 0 to wait forever.")]
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
        [Display("Add to Log", Group: "Common", Order: -2.05, Description: "If enabled the result of the query is added to the log.")]
        public bool AddToLog { get; set; }

        [EnabledIf("AddToLog", true)]
        [EnabledIf("GeneratesOutput", true)]
        [Display("Log Header", Group: "Common", Order: -2.0, Description: "This string is added to the front of the result of the query.")]
        public string LogHeader { get; set; }

        public ProcessStep()
        {
            Application = "";
            Arguments = "";
            WorkingDirectory = Directory.GetCurrentDirectory();
            WaitForEnd = true;
        }

        public override void PrePlanRun()
        {
            base.PrePlanRun();

            string appFilePath = Path.GetFullPath(Application);
            if (!File.Exists(appFilePath))
            {
                throw new Exception(String.Format("The application {0} could not be found.", appFilePath));
            }

            string workingDirPath = Path.GetFullPath(WorkingDirectory);
            if (!Directory.Exists(workingDirPath))
            {
                throw new Exception(String.Format("The directory {0} could not be found.", workingDirPath));
            }
        }

        private AutoResetEvent outputWaitHandle, errorWaitHandle;
        private StringBuilder output, error;

        public override void Run()
        {
            Int32 timeout = Timeout <= 0 ? Int32.MaxValue : Timeout;

            using (Process process = new Process())
            {
                process.StartInfo.FileName = Application;
                process.StartInfo.Arguments = Arguments;
                process.StartInfo.WorkingDirectory = Path.GetFullPath(WorkingDirectory);
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                if (WaitForEnd)
                {
                    output = new StringBuilder();
                    error = new StringBuilder();

                    using (outputWaitHandle = new AutoResetEvent(false))
                    using (errorWaitHandle = new AutoResetEvent(false))
                    {
                        process.OutputDataReceived += OutputDataRecv;
                        process.ErrorDataReceived += ErrorDataRecv;

                        Log.Debug("Starting process {0} with arguments \"{1}\"", Application, Arguments);
                        process.Start();

                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        if (process.WaitForExit(timeout) &&
                            outputWaitHandle.WaitOne(timeout) &&
                            errorWaitHandle.WaitOne(timeout))
                        {
                            string ResultData = output.ToString();

                            if (AddToLog)
                            {
                                foreach (var Line in ResultData.Split(new string[1] { "\r\n" }, StringSplitOptions.None))
                                Log.Info("{0} {1}", LogHeader, Line);
                            }

                            ProcessOutput(ResultData);
                        }
                        else
                        {
                            process.OutputDataReceived -= OutputDataRecv;
                            process.ErrorDataReceived -= ErrorDataRecv;

                            string ResultData = output.ToString();

                            if (AddToLog)
                            {
                                foreach (var Line in ResultData.Split(new string[1] { "\r\n" }, StringSplitOptions.None))
                                    Log.Info("{0} {1}", LogHeader, Line);

                            }

                            ProcessOutput(ResultData);

                            Log.Error("Timed out while waiting for application. Trying to kill process...");

                            process.Kill();
                            UpgradeVerdict(Verdict.Aborted);
                        }

                    }
                }
                else
                {
                    process.Start();
                }
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
                    error.AppendLine(e.Data);
                }
            }
            catch (ObjectDisposedException)
            {
                // Suppress - Testplan has been aborted and process is disconnected
            }
        }
    }
}
