//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System.IO;
using System.Text;
using OpenTap.Plugins.BasicSteps;
using OpenTap;

namespace OpenTap.Engine.UnitTests
{
    public class MacroFilePathTestStep : TestStep
    {
        public MacroString PathToThing { get; set; }

        public string ExpandedString { get; set; }

        public MacroFilePathTestStep()
        {
            PathToThing = new MacroString(this);
            ExpandedString = "";
        }

        public override void Run()
        {
            ExpandedString = PathToThing.Expand();
        }
    }

    [TestFixture]
    public class ExternalTestPlanParameterTest
    {


        [Test]
        public void SetValuesTest()
        {
            var delayStep1 = new DelayStep();
            var delayStep2 = new DelayStep();
            var logStep = new LogStep();
            var logStep2 = new LogStep();
            var fileStep = new MacroFilePathTestStep();
            var fileStep2 = new MacroFilePathTestStep();
            fileStep.PathToThing.Text = "<TESTPLANDIR>\\asdasd";
            TestPlan plan = new TestPlan();
            plan.ChildTestSteps.Add(delayStep1);
            plan.ChildTestSteps.Add(delayStep2);
            plan.ChildTestSteps.Add(logStep);
            plan.ChildTestSteps.Add(logStep2);
            plan.ChildTestSteps.Add(fileStep);
            plan.ChildTestSteps.Add(fileStep2);
            var delayInfo = TypeInfo.GetTypeInfo(delayStep1);
            var logInfo = TypeInfo.GetTypeInfo(logStep);
            var fileStepInfo = TypeInfo.GetTypeInfo(fileStep);
            plan.ExternalParameters.Add(delayStep1, delayInfo.GetMember("DelaySecs"));
            plan.ExternalParameters.Add(delayStep2, delayInfo.GetMember("DelaySecs"), "Time Delay");
            plan.ExternalParameters.Add(logStep, logInfo.GetMember("Severity"), Name: "Severity");
            plan.ExternalParameters.Add(logStep2, logInfo.GetMember("Severity"), Name: "Severity");
            plan.ExternalParameters.Add(fileStep, fileStepInfo.GetMember("PathToThing"), Name: "Path1");
            plan.ExternalParameters.Add(fileStep2, fileStepInfo.GetMember("PathToThing"), Name: "Path1");
            for (int j = 0; j < 5; j++)
            {
                for (double x = 0.01; x < 10; x += 3.14)
                {
                    plan.ExternalParameters.Get("Time Delay").Value = x;
                    Assert.AreEqual(x, delayStep1.DelaySecs);
                    Assert.AreEqual(x, delayStep2.DelaySecs);
                }

                plan.ExternalParameters.Get("Severity").Value = LogSeverity.Error;
                Assert.AreEqual(LogSeverity.Error, logStep.Severity);
                Assert.AreEqual(LogSeverity.Error, logStep2.Severity);

                string planstr = null;
                using (var memstream = new MemoryStream())
                {
                    plan.Save(memstream);
                    planstr = Encoding.UTF8.GetString(memstream.ToArray());
                }
                Assert.IsTrue(planstr.Contains(@"external=""Time Delay"""));
                Assert.IsTrue(planstr.Contains(@"external=""Severity"""));
                Assert.IsTrue(planstr.Contains(@"external=""Path1"""));

                using (var memstream = new MemoryStream(Encoding.UTF8.GetBytes(planstr)))
                    plan = TestPlan.Load(memstream,planstr);

                delayStep1 = (DelayStep)plan.ChildTestSteps[0];
                delayStep2 = (DelayStep)plan.ChildTestSteps[1];
                logStep = (LogStep)plan.ChildTestSteps[2];
                logStep2 = (LogStep)plan.ChildTestSteps[3];
                fileStep = (MacroFilePathTestStep)plan.ChildTestSteps[4];
                fileStep2 = (MacroFilePathTestStep)plan.ChildTestSteps[5];
                Assert.IsTrue(fileStep2.PathToThing.Context == fileStep2);
                Assert.AreEqual(fileStep2.PathToThing.Text, fileStep.PathToThing.Text);
            }
        }
    }
}
