//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using NUnit.Framework;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using System.IO;
using OpenTap.Cli;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class TestPlanRunnerTests
    {
        [SetUp]
        public void AssemblyInit()
        {
            SessionLogs.Initialize(String.Format("UnitTestLog{0}.txt", DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")));
        }

        public class CliTestStep : TestStep
        {
            TraceSource _log =  OpenTap.Log.CreateSource("CliStep");
            public override void Run()
            {
                _log.Info("Test123");
            }
        }

        [Test]
        public void RunPlanTest()
        {
            PluginManager.SearchAsync();
            TestPlanRunner.SetSettingsDir("Default");
            TestPlan plan = new TestPlan();
            plan.Steps.Add(new CliTestStep());
            TestPlanRunner.RunPlanForDut(plan, new List<ResultParameter>());
        }

        [Test]
        public void RunParseTest()
        {
            var setVerdict = new OpenTap.Engine.UnitTests.TestTestSteps.VerdictStep();
            TestPlan plan = new TestPlan();
            plan.Steps.Add(setVerdict);
            plan.ExternalParameters.Add(setVerdict, setVerdict.GetType().GetProperty("VerdictOutput"),"verdict");
            plan.Save("verdictPlan.TapPlan");
            var fileName = CreateCsvTestFile(new string[] { "verdict" }, new object[] { "pass" });
            {
                string[] passingThings = new[] { "verdict=\"pass\"", "verdict=\"Not_Set\"", "verdict=\"not set\"", fileName };
                foreach (var v in passingThings)
                {
                    var proc = TapProcessContainer.StartFromArgs(string.Format("run verdictPlan.TapPlan -e {0}", v));
                    proc.WaitForEnd();
                    Assert.AreEqual(0, proc.TapProcess.ExitCode);
                }
            }
            {
                string[] passingThings = new[] { "fail", "Error" };
                foreach (var v in passingThings)
                {
                    var proc = TapProcessContainer.StartFromArgs(string.Format("run verdictPlan.TapPlan -e verdict=\"{0}\"", v));
                    proc.WaitForEnd();
                    if(v == "Error")
                        Assert.AreEqual((int)ExitStatus.RuntimeError, proc.TapProcess.ExitCode);
                    else
                        Assert.AreEqual((int)ExitStatus.TestPlanFail, proc.TapProcess.ExitCode);
                }
            }
        }

        class TapProcessContainer
        {
            public Process TapProcess;
            public string ConsoleOutput = "";
            Task<string> consoleListener;
            bool procStarted = false;
            public static TapProcessContainer StartFromArgs(string args)
            {
                Process proc = new Process();

                var container = new TapProcessContainer { TapProcess = proc };
                container.consoleListener = Task.Factory.StartNew(new Func<string>(container.consoleOutputLoader));
                var program = Path.Combine(Path.GetDirectoryName(typeof(PluginManager).Assembly.Location), "tap.exe");
                proc.StartInfo = new ProcessStartInfo(program, args)
                {
                    UseShellExecute = true,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                proc.StartInfo.UseShellExecute = false;

                container.procStarted = proc.Start();
                Thread.Sleep(200);
                return container;
            }

            string consoleOutputLoader()
            {
                while (!procStarted) Thread.Sleep(10);

                StringBuilder ConsoleOutput = new StringBuilder();

                var procOutput = TapProcess.StandardOutput;
                var procOutput2 = TapProcess.StandardError;
                char[] buffer = new char[100];

                while (!procOutput.EndOfStream)
                {
                    int read = procOutput.Read(buffer, 0, buffer.Length);
                    ConsoleOutput.Append(buffer, 0, read);
                    int read2 = procOutput2.Read(buffer, 0, buffer.Length);
                    ConsoleOutput.Append(buffer, 0, read2);
                    Thread.Sleep(10);
                }
                this.ConsoleOutput = ConsoleOutput.ToString();
                return ConsoleOutput.ToString();
            }

            public void WaitForEnd()
            {
                TapProcess.WaitForExit();
                ConsoleOutput = consoleListener.Result;
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

        class RpcManager
        {
            TapProcessContainer[] processes;
            public RpcManager(params TapProcessContainer[] args)
            {
                processes = args;
            }

            public void Await(Func<bool> fcn)
            {
                Await(fcn, TimeSpan.FromSeconds(20));
            }

            public void Await(Func<bool> fcn, TimeSpan timeout)
            {
                Stopwatch sw = Stopwatch.StartNew();
                while (!fcn())
                {
                    if (sw.Elapsed > timeout) throw new TimeoutException();
                    foreach (var proc in processes)
                        Assert.IsFalse(proc.TapProcess.HasExited);

                    Thread.Sleep(1);
                }
                Thread.Sleep(100);
            }
        }

     

        private string CreateCsvTestFile(string[] names, object[] values)
        {
            var newfile = "cliTestPass.csv";
            var delimiter = ",";
            if (!System.IO.File.Exists(newfile))
            {
                System.IO.File.Create(newfile).Close();
            }
            using (System.IO.TextWriter writer = System.IO.File.CreateText(newfile))
            {
                writer.WriteLine(string.Join(delimiter, "External Name", "Value"));
                for(var index = 0; index < names.Length; index++)
                {
                    writer.WriteLine(string.Join(delimiter, names[index], values[index]));
                }
            }

            return newfile;
        }

        [Test]
        public void TestPlanRunWithNullArgument()
        {
            // As ExpectedException has been removed from nunit 3, I will have a try-catch block to catch the ArgumentNullException
            bool argumentNullExceptionCaught = false;
            try
            {
                TestPlanRun testPlanRun = new TestPlanRun(null, null, DateTime.Now, 0);
            }
            catch(ArgumentNullException ex)
            {
                Assert.AreEqual("Value cannot be null.\r\nParameter name: plan", ex.Message);
                argumentNullExceptionCaught = true;
            }
            
            Assert.IsTrue(argumentNullExceptionCaught, "ArgumentNullException was not thrown");
        }
    }
}
