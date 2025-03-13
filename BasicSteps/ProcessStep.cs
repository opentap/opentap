//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace OpenTap.Plugins.BasicSteps
{
    [Display("Run Program", Group: "Basic Steps", Description: "Runs a program, and optionally applies regular expressions (regex) to the output.")]
    public class ProcessStep : RegexOutputStep
    {
        public class EnvironmentVariable : ValidatingObject
        {
            [Display("Name", "The name of the environment variable.")]
            public string Name { get; set; }
            [Display("Value", "The value of the environment variable.")]
            public string Value { get; set; }

            public EnvironmentVariable()
            {
                Rules.Add(() => !string.IsNullOrEmpty(Name), "Name must not be empty.", nameof(Name));
            }

            public override bool Equals(object obj)
            {
                if (obj is EnvironmentVariable ev)
                {
                    return ev.Name == Name && ev.Value == Value;
                }
                return base.Equals(obj);
            }

            public override int GetHashCode()
            {
                var h1 = Name?.GetHashCode() ?? 0;
                var h2 = Value?.GetHashCode() ?? 0;
                return (((h1 + 742759321) * 1593213024) + h2) * 1079741372;
            }
        }

        public override bool GeneratesOutput => WaitForEnd; 

        [Display("Application", Order: -2.5,
            Description:
            "The path to the program. It should contain either a relative path to OpenTAP installation folder or an absolute path to the program.")]
        [FilePath(FilePathAttribute.BehaviorChoice.Open, "exe")]
        public string Application { get; set; } = "";

        [Display("Command Line Arguments", Order: -2.4, Description: "The arguments passed to the program.")]
        [DefaultValue("")]
        public string Arguments { get; set; } = "";

        [Display("Working Directory", Order: -2.3, Description: "The directory where the program will be started in.")]
        [DirectoryPath]
        public string WorkingDirectory { get; set; } = "";

        [Display("Environment Variables", Order: -2.25, Description: "The environment variables passed to the program.")]
        public List<EnvironmentVariable> EnvironmentVariables { get; set; } = new List<EnvironmentVariable>();

        [Display("Wait For Process to End", Order: -2.2,
            Description: "Wait for the process to terminate before continuing.")]
        [DefaultValue(true)]
        public bool WaitForEnd { get; set; } = true;
        
        int timeoutMs = 0;
        [Display("Wait Timeout", Order: -2.1, Description: "The time to wait for the process to end. Set to 0 to wait forever.")]
        [Unit("s", PreScaling: 1000)]
        [EnabledIf("WaitForEnd", true, HideIfDisabled = true)]
        public Int32 Timeout
        {
            get { return timeoutMs; }
            set
            {
                if (value >= 0)
                    timeoutMs = value;
                else throw new Exception("Timeout must be positive");
            }
        }

        [EnabledIf(nameof(GeneratesOutput), true, HideIfDisabled = true)]
        [Display("Add to Log", Order: -2.05, Description: "If enabled the result of the query is added to the log.")]
        public bool AddToLog { get; set; }

        [EnabledIf(nameof(AddToLog), true, HideIfDisabled = true)]
        [EnabledIf(nameof(GeneratesOutput), true, HideIfDisabled = true)]
        [Display("Log Header", Order: -2.0,
            Description: "This string is added to the front of the result of the query.")]
        [DefaultValue("")]
        public string LogHeader { get; set; } = "";

        string prepend;

        [Display("Check Exit Code", "Check the exit code of the application and set verdict to fail if it is non-zero, else pass. 'Wait For End' must be set for this to work.", "Set Verdict", Order: 1.1)]
        [EnabledIf(nameof(WaitForEnd), true, HideIfDisabled = true)]
        public bool CheckExitCode { get; set; }

        [Display("Run As Administrator", "Attempt to run the application as administrator.", Order: -2.06)]
        internal bool RunElevated { get; set; } = false; // this is disabled for now.
        
        [Display("Exit Code", Group: "Results", Order: 1.53, Collapsed: true, Description: "The exit code of the process.")]
        [Output]
        [Browsable(true)]
        [EnabledIf(nameof(WaitForEnd), true, HideIfDisabled = true)]
        public int ExitCode { get; private set; }
        
        StringBuilder output;


        [Display("Output", Group: "Results", Order: 1.53, Collapsed: true, Description: "The result of the execution.")]
        [EnabledIf(nameof(GeneratesOutput), true, HideIfDisabled = true)]
        [Output]
        [Browsable(true)]
        [Layout(LayoutMode.Normal, maxRowHeight: 1)]
        public string Output => output?.ToString() ?? "";


        public ProcessStep()
        {
            Rules.Add(HasNoDuplicateEnvironmentVariables, "Environment variable names must be unique.", nameof(EnvironmentVariables));
            Rules.Add(new ValidationRule(() => !string.IsNullOrWhiteSpace(Application), "Application must be set", nameof(Application)));
        }

        private bool HasNoDuplicateEnvironmentVariables()
        {
            HashSet<string> seenVariables = new HashSet<string>();
            foreach (var variable in EnvironmentVariables)
            {
                if (!seenVariables.Add(variable.Name))
                {
                    return false;
                }
            }
            return true;
        }
        public override void Run()
        {
            if (WaitForEnd == false)
            {
                Run0();
            }
            else
            {
                // for timeouts we use the regular TapThread abort feature.
                // this way it will work like if the user clicked the stop button
                // in the test plan (except from the abort verdict).
                TapThread.WithNewContext(() =>
                {
                    if(timeoutMs > 0)
                        TapThread.Current.AbortAfter(TimeSpan.FromMilliseconds(Timeout));
                    Run0();
                }, TapThread.Current);
            }
        }

        void Run0()
        {
            output?.Clear();
            ThrowOnValidationError(true);
            if (RunElevated &&!SubProcessHost.IsAdmin())
            {
                // note, this part is currently never enabled.
                try
                {
                    // Set RunElevated = false so ProcessHelper doesn't infinitely loop
                    RunElevated = false;
                    var processRunner = new SubProcessHost { ForwardLogs = AddToLog, LogHeader = LogHeader};
                    var verdict = processRunner.Run(this, true, TapThread.Current.AbortToken);
                    UpgradeVerdict(verdict);
                    return;
                }
                catch
                {
                    UpgradeVerdict(Verdict.Error);
                    throw;
                }
                finally
                {
                    RunElevated = true;
                }
            }

            prepend = string.IsNullOrEmpty(LogHeader) ? "" : LogHeader + " ";

            var process = new Process
            {
                StartInfo =
                {
                    FileName = Application,
                    Arguments = Arguments,
                    WorkingDirectory = string.IsNullOrEmpty(WorkingDirectory) ? Directory.GetCurrentDirectory() : Path.GetFullPath(WorkingDirectory),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                }
            };
            foreach (var environmentVariable in EnvironmentVariables)
            {
                process.StartInfo.Environment.Add(environmentVariable.Name, environmentVariable.Value);
            }
            var startEvent = new ManualResetEventSlim();
            var abortRegistration = TapThread.Current.AbortToken.Register(() =>
            {
                startEvent.Wait(500);
                if (process.HasExited) return;
                Log.Debug("Ending process '{0}'.", Application);
                try
                {  // process.Kill may throw if it has already exited.
                    try
                    {
                        // signal to the sub process that no more input will arrive.
                        // For many process this has the same effect as CTRL+C as stdin is closed.
                        process.StandardInput.Close();
                    }
                    catch
                    {
                        // this might be ok. It probably means that the input has already been closed.
                    }

                    if (!process.HasExited && !process.WaitForExit(500)) // give some time for the process to close by itself.
                    {
                        Thread.Sleep(100);
                        if(process.HasExited == false)
                            process.Kill();
                    }
                }
                catch(Exception ex)
                {
                    Log.Warning("Caught exception when killing process. {0}", ex.Message);
                }
            });

            if (WaitForEnd)
            {
                output = new StringBuilder();
                
                using(process)
                using(abortRegistration)
                {
                    process.OutputDataReceived += OutputDataRecv;
                    process.ErrorDataReceived += ErrorDataRecv;

                    Log.Debug("Starting process {0} with arguments \"{1}\"", Application, Arguments);
                    // Ensure that all asynchronous processing is completed by calling process.WaitForExit()
                    // Only the overload with no parameters has this guarantee. See remarks at
                    // https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?view=netframework-4.8
                    var done = new ManualResetEventSlim(false);
                    var elapsed = Stopwatch.StartNew();
                    process.Exited += (s, e) =>
                    {
                        done.Set();
                        elapsed.Stop();
                    };

                    process.Start();

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    startEvent.Set();


                    // Wait for the process to exit, allowing for cancellation every 100 ms
                    while (process.WaitForExit(100) == false)
                        if (TapThread.Current.AbortToken.IsCancellationRequested)
                            break;

                    while (process.HasExited == false && done.WaitHandle.WaitOne(100) == false)
                    {
                        if (process.HasExited)
                            break;

                        if (TapThread.Current.AbortToken.IsCancellationRequested)
                            break;
                    }

                    if (TapThread.Current.AbortToken.IsCancellationRequested)
                    {
                        try
                        {
                            process.WaitForExit(500);
                        }
                        catch
                        {
                            // ok
                        }
                    }

                    if (elapsed.Elapsed.TotalMilliseconds < timeoutMs || timeoutMs == 0)
                    {
                        var resultData = output.ToString();

                        ProcessOutput(resultData);
                        ExitCode = process.ExitCode;
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
                        ExitCode = process.ExitCode;
                        process.OutputDataReceived -= OutputDataRecv;
                        process.ErrorDataReceived -= ErrorDataRecv;

                        var resultData = output.ToString();

                        ProcessOutput(resultData);

                        if (!TapThread.Current.Parent.AbortToken.IsCancellationRequested)
                        {
                            Log.Info("Timed out while waiting for process to end.");
                            Verdict = Verdict.Fail;
                        }
                        else
                        {
                            TapThread.ThrowIfAborted();
                        }
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
                    }
                });
            }
        }

        void OutputDataRecv(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (e.Data != null)
                {
                    if(AddToLog)
                        Log.Info("{0}{1}", prepend, e.Data);
                    lock(output)
                        output.AppendLine(e.Data);
                }
            }
            catch (ObjectDisposedException)
            {
                // Suppress - Test plan has been aborted and process is disconnected
            }
        }

        void ErrorDataRecv(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (e.Data != null)
                {
                    if(AddToLog)
                        Log.Error("{0}{1}", prepend, e.Data);
                    lock(output)
                        output.AppendLine(e.Data);
                }
            }
            catch (ObjectDisposedException)
            {
                // Suppress - Test plan has been aborted and process is disconnected
            }
        }
    }
}
