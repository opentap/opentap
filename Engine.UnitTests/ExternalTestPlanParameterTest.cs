//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System.IO;
using System.Text;
using OpenTap.Plugins.BasicSteps;
using System;
using System.Linq;
using OpenTap.Plugins;

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
            var ifstep = new IfStep();
            
            fileStep.PathToThing.Text = "<TESTPLANDIR>\\asdasd";
            TestPlan plan = new TestPlan();
            plan.ChildTestSteps.Add(delayStep1);
            plan.ChildTestSteps.Add(delayStep2);
            plan.ChildTestSteps.Add(logStep);
            plan.ChildTestSteps.Add(logStep2);
            plan.ChildTestSteps.Add(fileStep);
            plan.ChildTestSteps.Add(fileStep2);
            plan.ChildTestSteps.Add(ifstep);
            ifstep.InputVerdict.Step = delayStep2;
            ifstep.InputVerdict.Property = TypeData.GetTypeData(delayStep1).GetMember("Verdict");
            var delayInfo = TypeData.GetTypeData(delayStep1);
            var logInfo = TypeData.GetTypeData(logStep);
            var fileStepInfo = TypeData.GetTypeData(fileStep);
            plan.ExternalParameters.Add(delayStep1, delayInfo.GetMember("DelaySecs"));
            plan.ExternalParameters.Add(delayStep2, delayInfo.GetMember("DelaySecs"), "Time Delay");
            plan.ExternalParameters.Add(logStep, logInfo.GetMember("Severity"), Name: "Severity");
            plan.ExternalParameters.Add(logStep2, logInfo.GetMember("Severity"), Name: "Severity");
            plan.ExternalParameters.Add(fileStep, fileStepInfo.GetMember("PathToThing"), Name: "Path1");
            plan.ExternalParameters.Add(fileStep2, fileStepInfo.GetMember("PathToThing"), Name: "Path1");
            plan.ExternalParameters.Add(ifstep, TypeData.GetTypeData(ifstep).GetMember(nameof(IfStep.InputVerdict)), Name: "InputVerdict");
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
                
                plan.ExternalParameters.Get("Path1").Value = plan.ExternalParameters.Get("Path1").Value;

                string planstr = null;
                using (var memstream = new MemoryStream())
                {
                    plan.Save(memstream);
                    planstr = Encoding.UTF8.GetString(memstream.ToArray());
                }
                Assert.IsTrue(planstr.Contains(@"Parameter=""Time Delay"""));
                Assert.IsTrue(planstr.Contains(@"Parameter=""Severity"""));
                Assert.IsTrue(planstr.Contains(@"Parameter=""Path1"""));

                using (var memstream = new MemoryStream(Encoding.UTF8.GetBytes(planstr)))
                    plan = TestPlan.Load(memstream, planstr);

                delayStep1 = (DelayStep)plan.ChildTestSteps[0];
                delayStep2 = (DelayStep)plan.ChildTestSteps[1];
                logStep = (LogStep)plan.ChildTestSteps[2];
                logStep2 = (LogStep)plan.ChildTestSteps[3];
                fileStep = (MacroFilePathTestStep)plan.ChildTestSteps[4];
                fileStep2 = (MacroFilePathTestStep)plan.ChildTestSteps[5];
                ifstep = (IfStep)plan.ChildTestSteps[6];
                Assert.IsTrue(fileStep2.PathToThing.Context == fileStep2);
                Assert.AreEqual(fileStep2.PathToThing.Text, fileStep.PathToThing.Text);
                Assert.AreEqual(delayStep2, ifstep.InputVerdict.Step);
            }
        }

        [Test]
        public void SerializeDeserializeWithDutExternalParameter()
        {
            var plan = new TestPlan();
            var step = new TestPlanTest.DutStep();
            var dut1 = new DummyDut {Name = "DUT1"};
            var dut2 = new DummyDut {Name = "DUT2"};
            
            DutSettings.Current.AddRange(new []{dut1, dut2});
            try
            {
                step.Dut = dut1;
                plan.ChildTestSteps.Add(step);
                plan.ExternalParameters.Add(step,
                    TypeData.GetTypeData(step).GetMember(nameof(TestPlanTest.DutStep.Dut)), "dut");

                using (var memstr = new MemoryStream())
                {
                    plan.Save(memstr);
                    
                    var serializer = new TapSerializer();
                    var ext = serializer.GetSerializer<ExternalParameterSerializer>();
                    ext.PreloadedValues["dut"] = "DUT2";

                    memstr.Seek(0, SeekOrigin.Begin);
                    plan = (TestPlan)serializer.Deserialize(memstr);
                }

                step = (TestPlanTest.DutStep) plan.ChildTestSteps[0];
                Assert.AreEqual(step.Dut, dut2);
            }
            finally
            {
                DutSettings.Current.Remove(dut1);
                DutSettings.Current.Remove(dut2);
            }

        }


        [Test]
        public void TestParameterizedVerdictOf()
        {
            // Create the test plan
            var plan = new TestPlan();
            var seq = new SequenceStep();
            var repeat = new RepeatStep();
            plan.ChildTestSteps.Add(seq);
            seq.ChildTestSteps.Add(repeat);

            var a = AnnotationCollection.Annotate(repeat);

            void VerifyAvailable(AnnotationCollection mem)
            {
                // Verify the expected available values for the member
                var avail = mem.Get<IAvailableValuesAnnotation>().AvailableValues.Cast<object>().ToArray();
                Assert.AreEqual(2, avail.Length);
                Assert.IsTrue(avail.Contains(seq));
                Assert.IsTrue(avail.Contains(repeat));
            }
            
            var mem = a.GetMember(nameof(repeat.TargetStep));
            VerifyAvailable(mem);

            { // Parameterize the member on the parent step
                var parameterize = mem.Get<MenuAnnotation>().MenuItems.FirstOrDefault(x =>
                    x.Get<IconAnnotationAttribute>()?.IconName == IconNames.ParameterizeOnParent);
                parameterize.Get<IMethodAnnotation>().Invoke();

                mem = AnnotationCollection.Annotate(seq).GetMember(@"Parameters \ Verdict Of");
                VerifyAvailable(mem);
                
                var unparameterize = mem.Get<MenuAnnotation>().MenuItems.FirstOrDefault(x =>
                    x.Get<IconAnnotationAttribute>()?.IconName == IconNames.Unparameterize);
                unparameterize.Get<IMethodAnnotation>().Invoke();
            }

            { // Parameterize the member on the test plan
                var parameterize = mem.Get<MenuAnnotation>().MenuItems.FirstOrDefault(x =>
                    x.Get<IconAnnotationAttribute>()?.IconName == IconNames.ParameterizeOnTestPlan);
                parameterize.Get<IMethodAnnotation>().Invoke();

                mem = AnnotationCollection.Annotate(plan).GetMember(@"Parameters \ Verdict Of");
                VerifyAvailable(mem);
            }
        }

        private void GenerateTestPlanWithNDelaySteps(int stepsCount, string filePath, double defaultValue, string externalParameterName)
        {
            Assert.IsTrue(stepsCount > 0);

            // Create a test plan with two DelaySteps.
            // Each of the DelaySteps will expose a it's delay property as an external parameter.
            TestPlan plan = new TestPlan();

            for (int i = 0; i < stepsCount; i++)
            {
                var delayStep = new DelayStep { DelaySecs = defaultValue };
                plan.ChildTestSteps.Add(delayStep);
                var delayInfo = TypeData.GetTypeData(delayStep);
                plan.ExternalParameters.Add(delayStep, delayInfo.GetMember("DelaySecs"), externalParameterName);
            }

            // Write the test plan to a file
            plan.Save(filePath);
        }

        [Test]
        public void SettingOfExternalParametersOnTestReferencePlan()
        {
            double defaultValue = 0.7; //seconds
            double newValue = 7.0; //seconds            
            //double tolerance = Math.Abs(newValue * .0000001); // The tolerance for variation in their delay double values
            int stepsCount = 20; // how many delay steps should be generated and tested
            string externalParameterName = "External Delay";
            string filePath = System.IO.Path.GetTempPath() + Guid.NewGuid().ToString() + ".TapPlan";
            
            GenerateTestPlanWithNDelaySteps(stepsCount, filePath, defaultValue, externalParameterName);

            try
            {
                // Create a test plan
                TestPlan testPlan = new TestPlan();

                // Create a ReferencePlanStep and add it to the test plan
                TestPlanReference tpr = new TestPlanReference();
                MacroString ms = new MacroString(tpr) { Text = filePath };
                tpr.Filepath = ms; // automatically calls LoadTesPlan
                testPlan.ChildTestSteps.Add(tpr);

                Assert.AreEqual(1, testPlan.ChildTestSteps.Count);
                Assert.AreEqual(stepsCount, tpr.ChildTestSteps.Count);

                // ----------------------------------------------------------------------------------------------------
                // This is how to get access to a TestPlanReference loaded test plan's children's external paramters:
                ITypeData ti = TypeData.GetTypeData(tpr);
                // IMemberInfo mi = ti.GetMember(externalParameterName); <- not possible to get property by its name in case the property name contains characters not valid of a C# property name
                IMemberData mi = ti.GetMembers().FirstOrDefault(m => m.Attributes.Any(xa => (xa as IDisplayAnnotation)?.Name == externalParameterName)); // <- the right approach              
                // ----------------------------------------------------------------------------------------------------

                Assert.IsNotNull(mi);
                Assert.AreEqual(defaultValue, mi.GetValue(tpr));
                mi.SetValue(tpr, newValue);

                // Test that the new value has been set on all the inner delay steps
                for (int i = 0; i < stepsCount; i++)
                {
                    DelayStep delayStep = tpr.ChildTestSteps[i] as DelayStep;
                    Assert.IsNotNull(delayStep);
                    //Assert.IsTrue(Math.Abs(newValue - delayStep.DelaySecs) <= tolerance);
                    Assert.AreEqual(newValue, delayStep.DelaySecs);
                }
            }
            finally
            {
                // delete the temporary file in the end
                if(File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [Test]
        public void SaveAndLoadTestPlanReference()
        {
            double defaultValue = 0.7; //seconds   
            //double tolerance = Math.Abs(newValue * .0000001); // The tolerance for variation in their delay double values
            int stepsCount = 1; // how many delay steps should be generated and tested
            string externalParameterName = "External Delay";
            string externalParamaterNameEncoded = "External_x0020_Delay";
            string externalParamaterNameLegacy = "prop0";
            string filePath1 = System.IO.Path.GetTempPath() + Guid.NewGuid().ToString() + ".TapPlan";
            string filePath2 = System.IO.Path.GetTempPath() + Guid.NewGuid().ToString() + ".TapPlan";
            string filePath3 = System.IO.Path.GetTempPath() + Guid.NewGuid().ToString() + ".TapPlan";

            try
            {
                // Save the test plan to be referenced
                GenerateTestPlanWithNDelaySteps(stepsCount, filePath1, defaultValue, externalParameterName);

                // Scope for separating the test Serialization (Save) from Deserialization (Load)
                {
                    // Create a test plan
                    TestPlan testPlan = new TestPlan();

                    // Create a ReferencePlanStep and add it to the test plan
                    TestPlanReference tpr = new TestPlanReference();
                    MacroString ms = new MacroString(tpr) { Text = filePath1 };
                    tpr.Filepath = ms; // automatically calls LoadTesPlan
                    testPlan.ChildTestSteps.Add(tpr);

                    // Save the new test plan
                    testPlan.Save(filePath2);

                    // The output should be something like this, remark the "External Delay" has been encoded as "External_x0020_Delay"
                    //<?xml version=\"1.0\" encoding=\"utf-8\"?>
                    //<TestPlan type=\"OpenTap.TestPlan\" Locked=\"false\">
                    //  <Steps>
                    //    <TestStep type=\"ref@OpenTap.Plugins.BasicSteps.TestPlanReference\" Version=\"9.0.0-Development\" Id=\"ae56d9d6-e077-4524-bd14-cb0c9f2d4ced\">
                    //      <External_x0020_Delay>0.7</External_x0020_Delay>
                    //      <Filepath>%TEMP%\\e7563ab3-d5e2-4e27-bc77-1f9b76feb37c.TapPlan</Filepath>
                    //      <StepMapping />
                    //      <Enabled>true</Enabled>
                    //      <Name>Test Plan Reference</Name>
                    //    </TestStep>
                    //  </Steps>
                    //  <Package.Dependencies>
                    //    <Package Name=\"OpenTAP\" Version=\"9.0.0+15a61e86\" />
                    //  </Package.Dependencies>
                    //</TestPlan>

                    // Verify that the saved file contains the encoded elements
                    using (var str = File.Open(filePath2, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (var read = new StreamReader(str))
                        {
                            string content = read.ReadToEnd();
                            Assert.IsTrue(content.Contains($"<{externalParamaterNameEncoded}>"));
                            Assert.IsTrue(content.Contains($"</{externalParamaterNameEncoded}>"));
                            Assert.IsFalse(content.Contains($"<{externalParameterName}>"));
                            Assert.IsFalse(content.Contains($"</{externalParameterName}>"));
                            Assert.IsFalse(content.Contains($"<{externalParamaterNameLegacy}>"));
                            Assert.IsFalse(content.Contains($"</{externalParamaterNameLegacy}>"));
                        }
                    }
                }

                // Scope for separating the test Deserialization (Load) from Serialization (Save)
                {
                    TestPlan testPlan = TestPlan.Load(filePath2);
                    Assert.AreEqual(1, testPlan.ChildTestSteps.Count);
                    TestPlanReference tpr = testPlan.ChildTestSteps[0] as TestPlanReference;
                    Assert.IsNotNull(tpr);

                    ITypeData ti = TypeData.GetTypeData(tpr);

                    // ensure there is a property "External Delay"
                    IMemberData mi = ti.GetMembers().FirstOrDefault(m => m.Attributes.Any(xa => (xa as IDisplayAnnotation)?.Name == externalParameterName));
                    Assert.IsNotNull(mi);
                    Assert.AreEqual(defaultValue, mi.GetValue(tpr));

                    // ensure there is no property "External_x0020_Delay"
                    Assert.IsNull(ti.GetMembers().FirstOrDefault(m => m.Attributes.Any(xa => (xa as IDisplayAnnotation)?.Name == externalParamaterNameEncoded)));
                    
                    // ensure there is no property "prop0"
                    Assert.IsNull(ti.GetMembers().FirstOrDefault(m => m.Attributes.Any(xa => (xa as IDisplayAnnotation)?.Name == externalParamaterNameLegacy)));
                }

                // Scope for separating the test Deserialization legacy (Load) from Serialization (Save)
                {
                    // Replace 
                    string content = "";
                    using (var str = File.Open(filePath2, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (var read = new StreamReader(str))
                        {
                            content = read.ReadToEnd();
                        }
                    }

                    Assert.IsTrue(content.Contains($"<{externalParamaterNameEncoded}>"));
                    Assert.IsTrue(content.Contains($"</{externalParamaterNameEncoded}>"));
                    Assert.IsFalse(content.Contains($"<{externalParameterName}>"));
                    Assert.IsFalse(content.Contains($"</{externalParameterName}>"));
                    Assert.IsFalse(content.Contains($"<{externalParamaterNameLegacy}>"));
                    Assert.IsFalse(content.Contains($"</{externalParamaterNameLegacy}>"));

                    content = content.Replace(externalParamaterNameEncoded, externalParamaterNameLegacy);
                        
                    Assert.IsFalse(content.Contains($"<{externalParamaterNameEncoded}>"));
                    Assert.IsFalse(content.Contains($"</{externalParamaterNameEncoded}>"));
                    Assert.IsFalse(content.Contains($"<{externalParameterName}>"));
                    Assert.IsFalse(content.Contains($"</{externalParameterName}>"));
                    Assert.IsTrue(content.Contains($"<{externalParamaterNameLegacy}>"));
                    Assert.IsTrue(content.Contains($"</{externalParamaterNameLegacy}>"));

                    Assert.IsFalse(File.Exists(filePath3));
                    using (var str = File.Open(filePath3, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                    {
                        using (var write = new StreamWriter(str))
                        {
                             write.Write(content);
                        }
                    }
                    Assert.IsTrue(File.Exists(filePath3));

                    // Load the test case and its test ref
                    TestPlan testPlan = TestPlan.Load(filePath3);
                    Assert.AreEqual(1, testPlan.ChildTestSteps.Count);
                    TestPlanReference tpr = testPlan.ChildTestSteps[0] as TestPlanReference;
                    Assert.IsNotNull(tpr);

                    ITypeData ti = TypeData.GetTypeData(tpr);

                    // ensure there is a property "External Delay"
                    IMemberData mi = ti.GetMembers().FirstOrDefault(m => m.Attributes.Any(xa => (xa as IDisplayAnnotation)?.Name == externalParameterName));
                    Assert.IsNotNull(mi);
                    Assert.AreEqual(defaultValue, mi.GetValue(tpr));

                    // ensure there is no property "External_x0020_Delay"
                    Assert.IsNull(ti.GetMembers().FirstOrDefault(m => m.Attributes.Any(xa => (xa as IDisplayAnnotation)?.Name == externalParamaterNameEncoded)));

                    // ensure there is no property "prop0"
                    Assert.IsNull(ti.GetMembers().FirstOrDefault(m => m.Attributes.Any(xa => (xa as IDisplayAnnotation)?.Name == externalParamaterNameLegacy)));
                }
            }
            finally
            {
                if (File.Exists(filePath1))
                {
                    File.Delete(filePath1);
                }
                if (File.Exists(filePath2))
                {
                    File.Delete(filePath2);
                }
                if (File.Exists(filePath3))
                {
                    File.Delete(filePath3);
                }
            }
        }

        [Test]
        public void SaveAndLoadExternalScopeParameters()
        {
            var plan = new TestPlan();
            var sequence = new SequenceStep();
            var delay = new DelayStep();
            plan.Steps.Add(sequence);
            sequence.ChildTestSteps.Add(delay);
            var newmember = TypeData.GetTypeData(delay).GetMember(nameof(DelayStep.DelaySecs))
                .Parameterize(sequence, delay, nameof(DelayStep.DelaySecs));
            var fwd = newmember.Parameterize(plan, sequence, nameof(DelayStep.DelaySecs));
            
            Assert.AreEqual(1, plan.ExternalParameters.Entries.Count);
            var xml = plan.SerializeToString();
            var newplan = Utils.DeserializeFromString<TestPlan>(xml);
            
            Assert.AreEqual(1, newplan.ExternalParameters.Entries.Count);
        }

        [Test]
        public void MultiSelectEditParametersDisabled()
        {
            var plan = new TestPlan();

            var sequence = new SequenceStep();    
            var delay = new DelayStep();
            plan.Steps.Add(sequence);
            sequence.ChildTestSteps.Add(delay);

            var p1 = TypeData.GetTypeData(delay).GetMember(nameof(DelayStep.DelaySecs))
                .Parameterize(sequence, delay, nameof(DelayStep.DelaySecs));

            var sequence2 = new SequenceStep();    
            var delay2 = new DelayStep();
            plan.Steps.Add(sequence2);
            sequence2.ChildTestSteps.Add(delay2);
            
            var p2 = TypeData.GetTypeData(delay2).GetMember(nameof(DelayStep.DelaySecs))
                .Parameterize(sequence2, delay2, nameof(DelayStep.DelaySecs));
            
            
            Assert.NotNull(p1);
            Assert.NotNull(p2);

            var models = new ITestStepParent[][]
            {
                new ITestStepParent[] {sequence, sequence2},
                new ITestStepParent[] {sequence},
                new ITestStepParent[] {sequence2}
            };

            foreach (var stepModel in models)
            {
                var stepsModel = AnnotationCollection.Annotate(stepModel);
                var delayAnnotation = stepsModel.GetMember(nameof(DelayStep.DelaySecs));
                var menu = delayAnnotation.Get<MenuAnnotation>();
                var edit = menu.MenuItems.FirstOrDefault(x =>
                    x.Get<IconAnnotationAttribute>()?.IconName == IconNames.EditParameter);

                var access = edit.Get<IAccessAnnotation>();
                
                Assert.AreEqual(access.IsVisible, stepModel.Length == 1);
            }            
        }
    }
}
