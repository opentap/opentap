//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            CancellationTokenSource cts = new CancellationTokenSource();
            TestPlanRunner.RunPlanForDut(plan, new List<ResultParameter>(), cts.Token);
        }

        [Test]
        public void RunParseTest()
        {
            var setVerdict = new OpenTap.Engine.UnitTests.TestTestSteps.VerdictStep();
            TestPlan plan = new TestPlan();
            plan.Steps.Add(setVerdict);
            plan.ExternalParameters.Add(setVerdict, TypeData.GetTypeData(setVerdict).GetMember("VerdictOutput"),"verdict");
            plan.ExternalParameters.Get("verdict").Value = "Not Set";
            plan.Save("verdictPlan.TapPlan");
            var fileName = CreateCsvTestFile(new string[] { "verdict" }, new object[] { "pass" });
            {
                string[] passingThings = new[] { "verdict=\"pass\"", "verdict=\"Not Set\"", "verdict=\"not set\"", fileName };
                passingThings.AsParallel().ForAll(v =>
                {

                    var args = string.Format("run verdictPlan.TapPlan -e {0}", v);
                    Log.CreateSource("RunParseTest").Debug("Running tap {0}", args);
                    var proc = TapProcessContainer.StartFromArgs(string.Format("run verdictPlan.TapPlan -e {0}", v),TimeSpan.FromMinutes(5));
                    proc.WaitForEnd();
                    Assert.AreEqual(0, proc.TapProcess.ExitCode);
                });
            }
            {
                string[] passingThings = new[] { "fail", "Error" };
                passingThings.AsParallel().ForAll(v =>
                {
                    var args = string.Format("run verdictPlan.TapPlan -e verdict=\"{0}\"", v);
                    Log.CreateSource("RunParseTest").Debug("Running tap {0}", args);
                    var proc = TapProcessContainer.StartFromArgs(args, TimeSpan.FromSeconds(120));
                    proc.WaitForEnd();
                    if (v == "Error")
                        Assert.AreEqual((int) ExitStatus.RuntimeError, proc.TapProcess.ExitCode);
                    else
                        Assert.AreEqual((int) ExitStatus.TestPlanFail, proc.TapProcess.ExitCode);
                });
            }
        }

        [Test]
        public void SimpleVerdictStepTest()
        {
            var setVerdict = new OpenTap.Engine.UnitTests.TestTestSteps.VerdictStep();
            TestPlan plan = new TestPlan();
            plan.Steps.Add(setVerdict);
            plan.Save("verdictPlan.TapPlan");
            var proc = TapProcessContainer.StartFromArgs("run verdictPlan.TapPlan");
            proc.WaitForEnd();
            Assert.AreEqual(0, proc.TapProcess.ExitCode);
        }

        [Test]
        public void TestProcessContainer()
        {
            var proc = TapProcessContainer.StartFromArgs("package list", TimeSpan.FromSeconds(100));
            proc.WaitForEnd();
            
            Assert.AreEqual(0, proc.TapProcess.ExitCode);
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
                Assert.AreEqual($"Value cannot be null.{Environment.NewLine}Parameter name: plan", ex.Message);
                argumentNullExceptionCaught = true;
            }
            
            Assert.IsTrue(argumentNullExceptionCaught, "ArgumentNullException was not thrown");
        }
    }
}
