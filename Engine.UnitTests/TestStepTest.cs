//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;
using OpenTap.Engine.UnitTests.TestTestSteps;
using OpenTap.EngineUnitTestUtils;

namespace OpenTap.Engine.UnitTests
{
    namespace SubSpace
    {
        public class TestStepTest : TestStep
        {
            public List<TestPoint> TestPoints { get; set; }
            public class TestPoint
            {
                public int Channel;
            }
            /// <summary>
            /// Initializes a new instance of the TestStepTest class.
            /// </summary>
            public TestStepTest()
            {
                TestPoints = new List<TestPoint>();
            }
            public override void Run()
            {
            }
        }
    }
    public class TestStepTest : TestStep
    {
        public List<TestPoint> TestPoints { get; set; }
        public class TestPoint
        {
            public int Channel { get; set; }
        }
        public class NestedStep : TestStep
        {
            public override void Run() { }
        }
        /// <summary>
        /// Initializes a new instance of the TestStepTest class.
        /// </summary>
        public TestStepTest()
        {
            TestPoints = new List<TestPoint>();
        }
        public override void Run()
        {
            Results.Publish("UnitTest", new List<string>() { "Channel", "Power [dBm]" }, 27, 1.11, 2, 0);
            Verdict = Verdict.Pass;
        }

        public static TestPlan CreateGenericTestPlan()
        {
            TestPlan plan = new TestPlan();
            plan.Steps.Add(new TestStepTest());
            plan.Steps.Add(new SubSpace.TestStepTest());
            plan.Steps.Add(new TestStepTest2());
            return plan;
        }

    }
    public class TestStepTest2 : TestStep
    {
        public List<TestPoint> TestPoints { get; set; }
        public class TestPoint
        {
            public int Channel { get; set; }
        }
        public class NestedStep : TestStep
        {
            public override void Run() { }
        }
        public class SomeObject
        {
            public int Channel { get; set; }
        }
        public SomeObject ObjectProperty { get; set; }

        /// <summary>
        /// Initializes a new instance of the TestStepTest2 class.
        /// </summary>
        public TestStepTest2()
        {
            TestPoints = new List<TestPoint>();
        }
        public override void Run()
        {
        }
    }

    [AllowAsChildIn(typeof(RecursiveTestStep))]
    public class TestStepTest3 : TestStep
    {
        public List<TestPoint> TestPoints { get; set; }


        public class TestPoint
        {
            public int Channel { get; set; }
        }
        public class SomeObject
        {
            public int Channel { get; set; }
        }
        public SomeObject ObjectProperty { get; set; }

        /// <summary>
        /// Initializes a new instance of the TestStepTest2 class.
        /// </summary>
        public TestStepTest3()
        {
            TestPoints = new List<TestPoint>();
        }
        public override void Run()
        {
        }
    }
    public class RecursiveTestStep : TestStep
    {
        public int SomeInt { get; set; }
        public RecursiveTestStep()
        {
            SomeInt = 5;
        }
        public override void Run()
        {

        }
    }

    public class OpenCrash : Instrument
    {

        public override void Open()
        {
            base.Open();
            throw new Exception("intended");
        }
    }

    public class CloseCrash : Instrument
    {
        public override void Close()
        {
            base.Close();
            throw new Exception("intended");
        }
    }

    public class CreateCrash : Instrument
    {
        public CreateCrash()
        {
            throw new Exception("Intended");
        }
    }

    public class testInstr : TestStep
    {
        public IInstrument Inst { get; set; }
        public override void Run()
        {

        }
    }


    [System.ComponentModel.DisplayName(" Close Every Thing")]
    public class closeEveryThing : TestStep
    {

        public override void PrePlanRun()
        {
            base.PrePlanRun();
            Run();
        }

        public override void Run()
        {
            InstrumentSettings.Current
                .Concat<IResource>(ResultSettings.Current)
                .Concat(DutSettings.Current)
                .ToList()
                .ForEach(r => r.Close());
        }

    }

    public class IsOpenTestStep : TestStep
    {
        public IResource Resource { get; set; }

        public override void Run()
        {
            Assert.IsTrue(Resource.IsConnected);
        }
    }

    public class DummyDut : Dut
    {
        [MetaData]
        [Browsable(false)]
        public string Serial { get; set; }

        public override void Open()
        {
            base.Open();
            Serial = SerialNumber;
        } 
        public override void Close()
        {
            base.Close();
            Serial = null;
        }

        public static string SerialNumber = "123456";

    }

    public class DummyInstrument : Instrument
    {
        
    }
    
    [DisplayName("Test\\UnitTest resultListener2")]
    public class resultListener2 : ResultListener
    {
        public resultListener2()
        {
            Name = "resultListener2";
        }
    }

    /*public*/
    class ThrowExceptionSetting : ComponentSettings
    {
        public int exceptionTest
        {
            get
            {
                throw new Exception("Intended");
            }
            set
            {
                throw new Exception("Intended");
            }

        }
    }

    [Display("Enabled Check Boxes")]
    public class EnabledFormattedNameTest : TestStep
    {
        [Display("Checked Bool")]
        public Enabled<bool> CheckedBool { get; set; }
        [Display("Checked Bool Array")]
        public Enabled<bool[]> CheckedBoolArray { get; set; }
        [Display("Checked Double")]
        public Enabled<double> CheckedDouble { get; set; }
        [Display("Checked Double Array")]
        public Enabled<double[]> CheckedDoubleArray { get; set; }
        [Display("Checked String")]
        public Enabled<string> CheckedString { get; set; }
        [Display("Checked String Array")]
        public Enabled<string[]> CheckedStringArray { get; set; }
        [Display("Checked String List")]
        public Enabled<List<string>> CheckedStringList { get; set; }
        [Display("Checked Instrument")]
        public Enabled<Instrument> CheckedInstrument { get; set; }

        public EnabledFormattedNameTest()
        {
            CheckedBool = new Enabled<bool>() { IsEnabled = true, Value = false };
            CheckedBoolArray = new Enabled<bool[]>() { IsEnabled = true, Value = new bool[] { false, true } };
            CheckedDouble = new Enabled<double>() { IsEnabled = true, Value = 3.21 };
            CheckedDoubleArray = new Enabled<double[]>() { IsEnabled = true, Value = new double[] { 3.33, 45.6, 88.8 } };
            CheckedString = new Enabled<string>() { IsEnabled = true, Value = "Some comment" };
            CheckedStringArray = new Enabled<string[]>() { IsEnabled = true, Value = new string[] { "abc", "DEF", "GhI" } };
            CheckedStringList = new Enabled<List<string>>() { IsEnabled = true, Value = new List<string> { "One", "Two", "Three" } };
            CheckedInstrument = new Enabled<Instrument>() { IsEnabled = true };
        }

        public override void Run()
        {
            throw new NotImplementedException();
        }
    }

    public class NullArrayTest : TestStep
    {
        public int[] TestArray { get; set; }
        public override void Run()
        {
            throw new NotImplementedException();
        }
    }

    public class NullArrayInstrument : Instrument
    {
        public int[] TestArray { get; set; }
    }

    public class ResultStressTest : TestStep
    {
        public int Iterations { get; set; }
        public ResultStressTest()
        {
            Iterations = 100;
        }
        public override void Run()
        {
            for (double i = 0; i < Iterations; i++)
            {
                Results.Publish("UnitTest", new List<string>() { "Channel", "Power [dBm]" }, i, Math.Sin(i), 2, 0);
            }
            Verdict = Verdict.Pass;
        }
    }

    public class Result1DTest : TestStep
    {
        public int NResults { get; set; }
        public double Duration { get; set; }
        public Result1DTest()
        {
            NResults = 100;
        }
        public override void Run()
        {
            TapThread.Sleep((int)(1000 * Duration));
            for (double i = 0; i < NResults; i++)
            {
                Results.Publish("UnitTest", new List<string> { "Channel", "Power [dBm]" }, Math.Sin(i)/*,Math.Cos(i)*/);
            }
            Verdict = Verdict.Pass;
        }
    }

    public class SomeClass { }

    public class NullPropertyTest : TestStep
    {
        public SomeClass Property { get; set; }


        public override void Run()
        {
            for (double i = 0; i < 5; i++)
            {
                Results.Publish("UnitTest", new List<string> { "Channel", "Power [dBm]" }, Math.Sin(i), Math.Cos(i));
            }
        }
    }

    public class GetParamsSub : Resource
    {
        [MetaData]
        public int Index { get; set; }
        public int Index2 { get; set; }
        public GetParamsSub()
        {
            Index = 10;
        }
    }

    public class GetParameters : TestStep
    {
        public double Param1 { get; set; }
        public GetParamsSub sub { get; set; }
        public GetParameters()
        {
            sub = new GetParamsSub();
        }
        public override void Run()
        {

            Param1 = 5;
            var param = ResultParameters.GetParams(this);
            var dict = param.ToDictionary();
            Assert.IsTrue((double)dict["Param1"] == Param1);
            Assert.IsTrue(dict.ContainsKey("sub/Index"));
            Assert.IsTrue(dict.ContainsKey("sub/Index2") == false);
            Console.WriteLine(param);
        }
    }

    [TestFixture]
    public class TestStepTestFixture 
    {
        [Test]
        public void SetStepNameToNullThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new DelayStep { Name = null });
        }
    }

    [TestFixture]
    public class AllowChildTest
    {
        
        private class BaseAutomationStep : TestStep
        {
            public override void Run()
            {
            }
        }

        [AllowAsChildIn(typeof(BaseAutomationStep))]
        private class AutomationStep : BaseAutomationStep
        {
        }

        [AllowAsChildIn(typeof(BaseAutomationStep))]
        [AllowAnyChild]
        private class LoopStep : BaseAutomationStep
        {
        }

        [Test]
        public void TestAllowChildren()
        {
            LoopStep step = new LoopStep();
            step.ChildTestSteps.Add(new AutomationStep());
        }

        [Test]
        public void TestGuid()
        {
            var a = new DelayStep();
            var b = new DelayStep();
            Assert.AreNotEqual(a.Id, b.Id);
        }
    }

    [TestFixture]
    public class BasicTestStepTests
    {
        public class FunkyArrayStep : TestStep
        {
            public object[] Array { get; set; }
            public override void Run()
            {
                throw new NotImplementedException();
            }
            public FunkyArrayStep()
            {
                Array = new object[] { new object(), null, 5.0, "Test" };
            }
        }
        [Test]
        public void TestNameFormat()
        {
            var culture = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            try
            {
                DelayStep delay = new DelayStep();
                delay.DelaySecs = 1.0;
                delay.Name = "Delay of {Time Delay} and {Time Delay}";
                var result = delay.GetFormattedName();
                Assert.AreEqual(result, "Delay of 1 s and 1 s");

                delay.Name = "Delay of {} and {Time Delay2}";
                result = delay.GetFormattedName();
                Assert.AreEqual(result, delay.Name);
                delay.Name = "Delay of {{}} and {{Time Delay2}}";
                result = delay.GetFormattedName();
                Assert.AreEqual(result, delay.Name);
                delay.Name = "Delay of {{}} and {{Time Delay}}";
                result = delay.GetFormattedName();
                Assert.AreEqual(result, "Delay of {{}} and {1 s}");
                DialogStep diag = new DialogStep();
                diag.Name = "Timeout of {Timeout Timeout}";
                diag.Timeout = 2;
                var result2 = diag.GetFormattedName();
                Assert.AreEqual(result2, "Timeout of 2 s");

                SCPIRegexStep scpiRegex = new SCPIRegexStep();
                scpiRegex.RegularExpressionPattern = new Enabled<string> { Value = "asd", IsEnabled = true };
                scpiRegex.Name = "|{Set Verdict Regular Expression}|";
                var result3 = scpiRegex.GetFormattedName();
                Assert.AreEqual(result3, "|asd|");

                scpiRegex.Instrument = null;
                scpiRegex.Name = "|{Instrument}|";
                var result4 = scpiRegex.GetFormattedName();
                Assert.AreEqual(result4, "|Not Set|");

                scpiRegex.Name = "|{asdwasd}|";
                var result5 = scpiRegex.GetFormattedName();
                Assert.AreEqual(result5, scpiRegex.Name);

                var arrayStep = new NullArrayTest() { TestArray = new int[] { 1, 3 } };
                arrayStep.Name = "{TestArray}";
                var result6 = arrayStep.GetFormattedName();
                Assert.AreEqual(result6, "1, 3");

                var funkyArray = new FunkyArrayStep();
                funkyArray.Name = "{Array}";
                var result7 = funkyArray.GetFormattedName();
                Assert.AreEqual(result7.Count(x => x == ','), funkyArray.Array.Length - 1);
                Assert.IsTrue(result7.Contains(", Test"));
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            }
        }

        [Test]
        public void TestNameFormat2()
        {
            var plan = new TestPlan();
            var repeat = new RepeatStep { Count =  10, Action = RepeatStep.RepeatStepAction.Fixed_Count};
            repeat.Name = "Repeat : {Iteration}";
            plan.ChildTestSteps.Add(repeat);
            var logStep = new LogStep();
            repeat.ChildTestSteps.Add(logStep);
            var log = new TestTraceListener();
            
            Log.AddListener(log);
            var run = plan.Execute();
            Assert.AreEqual(Verdict.NotSet, run.Verdict);
            Log.RemoveListener(log);
            var thelog = log.GetLog();

            for (int i = 0; i < repeat.Count; i++)
            {
                var str = string.Format("Repeat : {0} of {1}", i, repeat.Count);
                Assert.IsTrue(thelog.Contains(str));    
            }
        }
        
        [Test]
        public void TestNameFormat3()
        {
            var step = new VerdictStep() {Name = "Delay: {Resulting Verdict}", VerdictOutput =  Verdict.NotSet};
            var fmt = step.GetFormattedName();
            Assert.AreEqual("Delay: Not Set", fmt); 
        }
        
        [Test]
        public void ContinueLoop()
        {
            var sequence = new SequenceStep();
            var verdict1 = new VerdictStep {VerdictOutput = Verdict.Pass};
            var ifstep = new IfStep() {Action = IfStep.IfStepAction.ContinueLoop, TargetVerdict = Verdict.Pass};
            ifstep.InputVerdict.Property = TypeData.GetTypeData(verdict1).GetMember(nameof(VerdictStep.Verdict));
            ifstep.InputVerdict.Step = verdict1;
            var verdict2 = new VerdictStep() {VerdictOutput = Verdict.Fail};
            sequence.ChildTestSteps.Add(verdict1);
            sequence.ChildTestSteps.Add(ifstep); // instructed to skip the last verdict step
            sequence.ChildTestSteps.Add(verdict2); // if this step runs the plan will get the verdict 'fail'.
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(sequence);

            var run = plan.Execute();
            Assert.AreEqual(Verdict.Pass, run.Verdict);
        }
    }

    public class FormattedNameTests
    {
        public class TestNameFormat1
        {
            const string testStepName = "Enabled Check Boxes";
            private EnabledFormattedNameTest step;

            [Test]
            public void TestNameEnabledType()
            {
                step = new EnabledFormattedNameTest();
                AssertFormatName("{Checked Bool}", "False");
                AssertFormatName("{Checked Bool Array}", "False, True");
                AssertFormatName("{Checked Double}", "3.21");
                AssertFormatName("{Checked Double Array}", "3.33, 45.6, 88.8");
                AssertFormatName("{Checked String}", "Some comment");
                AssertFormatName("{Checked String Array}", "abc, DEF, GhI");
                AssertFormatName("{Checked String List}", "One, Two, Three");
                AssertFormatName("{Checked Instrument}", "NULL");

                //Uncheck a property
                step.CheckedBoolArray.IsEnabled = false;
                AssertFormatName("{Checked Bool Array}", "False, True (disabled)");

                //Remove all value of the properties
                step.CheckedDoubleArray.Value = new double[] { };
                AssertFormatName("{Checked Double Array}", "");

                step.CheckedDoubleArray.IsEnabled = false;
                AssertFormatName("{Checked Double Array}", " (disabled)");

                step.CheckedStringArray.Value = null;
                AssertFormatName("{Checked String Array}", "NULL");
            }

            private void AssertFormatName(string formatName, string expectedOutput)
            {
                step.Name = testStepName + " " + formatName;
                var result = step.GetFormattedName();
                Assert.AreEqual(result, testStepName + " " + expectedOutput);
            }
        }

        public class TestNameFormat2
        {
            const string testStepName = "Handle Input";
            private HandleInputStep inputStep;

            [Test]
            public void TestNameInputOutputType()
            {
                GenerateOutputStep outputStep = new GenerateOutputStep();
                inputStep = new HandleInputStep();
                var plan = new TestPlan();
                plan.ChildTestSteps.Add(outputStep);
                plan.ChildTestSteps.Add(inputStep);

                var annotation = AnnotationCollection.Annotate(inputStep);
                var inputAnnotation = annotation.GetMember(nameof(HandleInputStep.InputBoolArray));
                SetOutputProperty(inputAnnotation);
                inputAnnotation = annotation.GetMember(nameof(HandleInputStep.InputDouble));
                SetOutputProperty(inputAnnotation);
                inputAnnotation = annotation.GetMember(nameof(HandleInputStep.InputDoubleArray));
                SetOutputProperty(inputAnnotation);
                inputAnnotation = annotation.GetMember(nameof(HandleInputStep.InputString));
                SetOutputProperty(inputAnnotation);
                inputAnnotation = annotation.GetMember(nameof(HandleInputStep.InputStringArray));
                SetOutputProperty(inputAnnotation);
                inputAnnotation = annotation.GetMember(nameof(HandleInputStep.InputStringList));
                SetOutputProperty(inputAnnotation);

                annotation.Write(inputStep);

                AssertFormatName("{Input Bool Array}", "False, True");
                AssertFormatName("{Input Double}", "1");
                AssertFormatName("{Input Double Array}", "1, 2.2");
                AssertFormatName("{Input String}", "Something");
                AssertFormatName("{Input String Array}", "tom, dick");
                AssertFormatName("{Input String List}", "One, Two, Three");
            }

            private void SetOutputProperty(AnnotationCollection inputAnnotation)
            {
                var avail = inputAnnotation.Get<IAvailableValuesAnnotation>();
                var setVal = avail as IAvailableValuesSelectedAnnotation;
                var options = avail.AvailableValues.Cast<object>().ToArray();

                setVal.SelectedValue = options[1];
            }

            private void AssertFormatName(string formatName, string expectedOutput)
            {
                inputStep.Name = testStepName + " " + formatName;
                var result = inputStep.GetFormattedName();
                Assert.AreEqual(result, testStepName + " " + expectedOutput);
            }
        }
    }
}
