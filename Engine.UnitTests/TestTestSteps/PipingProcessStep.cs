using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using OpenTap.Cli;
using OpenTap;

namespace OpenTap.Engine.UnitTests.TestTestSteps
{
    [Display("user-input", "Used to verify user-input implementations", Group: "test")]
    public class UserInputTestAction : ICliAction
    {
        public class RequestObject
        {
            [Submit] public string Answer { get; set; }
        }

        [CommandLineArgument("answers", ShortName = "a",
            Description = "The answers that the caller is intending to give.")]
        public string[] ExpectedAnswers { get; set; }

        private static TraceSource log = Log.CreateSource(nameof(UserInputTestAction));

        public static string DescribeBytes(IEnumerable<char> i)
        {
            var s = i.ToArray();
            if (s.Length == 0) return "[]";

            var sb = new StringBuilder();
            sb.Append('[');
            foreach (var ch in s)
            {
                sb.Append(string.Format("0x{0:X}", (int)ch));
                sb.Append(", ");
            }
            sb.Remove(sb.Length - 2, 2);
            sb.Append(']');
            return sb.ToString();
        }

        public int Execute(CancellationToken cancellationToken)
        {
            try
            {
                for (int i = 0; i < ExpectedAnswers.Length; i++)
                {
                    var r = new RequestObject();
                    log.Info($"Checking input {i + 1} of {ExpectedAnswers.Length} ({ExpectedAnswers[i]})");
                    UserInput.Request(r);
                    var exp = DescribeBytes(ExpectedAnswers[i]);
                    var act = DescribeBytes(r.Answer);
                    log.Info($"Expected: {exp}");
                    log.Info($"  Actual: {act}");
                    if (r.Answer.Equals(ExpectedAnswers[i], StringComparison.InvariantCulture) == false)
                    {
                        log.Error($"Input {i + 1} did not match the expected input:");
                        log.Error($"Expected '{ExpectedAnswers[i]}', got '{r.Answer}'");
                        return 3;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error while reading input: {ex.Message}");
                log.Debug(ex);
            }

            return 0;
        }
    }

    [Display("Piping Process Step", "A run process step with support for piping", "Tests")]
    public class PipingProcessStep : TestStep
    {
        [Display("Application", Order: -2.5,
            Description:
            "The path to the program. It should contain either a relative path to OpenTAP installation folder or an absolute path to the program.")]
        [FilePath(FilePathAttribute.BehaviorChoice.Open, "exe")]
        public string Application { get; set; } = "";

        [Display("Command Line Arguments", Order: -2.4, Description: "The arguments passed to the program.")]
        [DefaultValue("")]
        public string Arguments { get; set; } = "";

        [Display("Expected Exit Code", "The expected exit code of the process.")]
        public int ExpectedExitCode { get; set; } = 0;

        [Display("Write Speed", "How fast should the input be written to the process stream. Measured in number of milliseconds between writes")]
        [Unit("ms")]
        public int WriteSpeed { get; set; } = 100;

        [Layout(LayoutMode.Normal, 2, maxRowHeight: 5)]
        [Display("Pipe Data", "The data that should be written to the process' input stream.")]
        public string StdIn { get; set; } = "";

        public enum WriteMode
        {
            WriteLines,
            WriteChars,
            WriteAll,
        }

        [Display("Write Mode", "How should the data be written to the pipe?")]
        public WriteMode Mode { get; set; }

        public override void Run()
        {
            using var process = new Process
            {
                StartInfo =
                {
                    FileName = Application,
                    Arguments = Arguments,
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                }
            };

            Log.Info($"Starting process '{Application}' with arguments '{Arguments}'.");

            if (!process.Start())
            {
                Log.Error("Process failed to start.");
                UpgradeVerdict(Verdict.Error);
                return;
            }
            
            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data == null) return;
                Log.Info(args.Data);
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data == null) return;
                Log.Error(args.Data);
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.StandardInput.AutoFlush = true;

            Log.Info($"Writing payload to process: {UserInputTestAction.DescribeBytes(StdIn)}");

            try
            {
                var w = process.StandardInput;
                switch (Mode)
                {
                    case WriteMode.WriteAll:
                        w.Write(StdIn);
                        break;
                    case WriteMode.WriteLines:
                        foreach (var line in StdIn.Trim().Split('\n'))
                        {
                            if (WriteSpeed > 0)
                                TapThread.Sleep(WriteSpeed);
                            if (process.HasExited) break;
                            w.WriteLine(line);
                        }

                        break;
                    case WriteMode.WriteChars:
                        foreach (char ch in StdIn)
                        {
                            if (WriteSpeed > 0)
                                TapThread.Sleep(WriteSpeed);
                            if (process.HasExited) break;
                            w.Write(ch);
                        }

                        break;
                }
            }
            // Writing to the input pipe will fail if the process has already exited
            catch (Exception) when (process.HasExited)
            {
                // suppress
            }
            if (!process.HasExited)
                process.StandardInput.Close(); 

            if (!process.WaitForExit((int)TimeSpan.FromSeconds(10).TotalMilliseconds))
            {
                Log.Error("Process did not exit in a timely fashion.");
                UpgradeVerdict(Verdict.Error);
                return;
            }

            if (process.ExitCode == ExpectedExitCode)
                UpgradeVerdict(Verdict.Pass);
            else
            {
                Log.Error($"Expected exit code {ExpectedExitCode}, was {process.ExitCode}.");
                UpgradeVerdict(Verdict.Fail);
            }
        }
    }
}
