//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenTap.Plugins.BasicSteps;
using NUnit.Framework;
using OpenTap.Engine.UnitTests.TestTestSteps;

namespace OpenTap.Engine.UnitTests
{
    public class ReadInputStep : TestStep
    {
        public Input<double> Input { get; set; } = new Input<double>();

        public override void Run()
        {
            Log.Debug("Input Value: {0}", Input.Value);
        }
    }

    [TestFixture]
    public class InputTest
    {
        [Test]
        public void InputBasicTests()
        {   
            var prop = TypeData.FromType(typeof(DelayStep)).GetMember(nameof(DelayStep.DelaySecs));
            DelayStep delay = new DelayStep();
            Input<double> secs = new Input<double>() { Step = delay, Property = prop};
            
            delay.DelaySecs = 2;
            Assert.AreEqual(secs.Value, delay.DelaySecs);

            Input<double> secs2 = new Input<double>() { Step = delay, Property = prop };
            Assert.AreEqual(secs, secs2);

            Input<double> secs3 = new Input<double>() { Step = delay, Property = null };
            Input<double> secs4 = new Input<double>() { Step = null, Property = prop };
            Input<double> secs5 = new Input<double>() { Step = null, Property = null };
            Input<double> secs6 = new Input<double>() { Step = delay, PropertyName = prop.Name};
            Assert.IsFalse(secs3 == secs4);
            Assert.IsFalse(secs4 == secs5);
            Assert.IsFalse(secs3 == secs5);
            Assert.IsTrue(secs == secs2);
            Assert.IsTrue(secs == secs6);

            { // test serialize
                var plan = new TestPlan();
                plan.ChildTestSteps.Add(delay);
                plan.ChildTestSteps.Add(new ReadInputStep() { Input = secs });

                var planxml = new TapSerializer().SerializeToString(plan);
                TestPlan plan2 = (TestPlan)new TapSerializer().DeserializeFromString(planxml);
                var step2 = plan2.ChildTestSteps[1] as ReadInputStep;
                Assert.IsTrue(step2.Input.Step == plan2.ChildTestSteps[0]);
            }
        }

        [Test]
        public void NullInputTest()
        {
            Input<double> a = null;
            Input<double> b = null;
            Assert.IsTrue(a == b);
            a = new Input<double>(){};
            Assert.IsTrue(a != b);
            b = new Input<double>(){};

            var prop = TypeData.FromType(typeof(DelayStep)).GetMember(nameof(DelayStep.DelaySecs));
            DelayStep delay = new DelayStep();

            Assert.IsTrue(a == b);
            a.Step = delay;
            a.Property = prop;
            Assert.IsTrue(a != b);
            b.Step = delay;
            b.Property = prop;
            Assert.IsTrue(a == b);
        }

        [Test]
        public void TestSerializeInput()
        {
            var step = new HandleInputStep();
            var plan = new TestPlan();
            plan.Steps.Add(step);
            var s = new TapSerializer();
            var xml = s.SerializeToString(plan);
            Assert.IsFalse(s.Errors.Any());
            var deserialized = s.DeserializeFromString(xml);
            Assert.IsFalse(s.Errors.Any());
        }

        [Test]
        public void UnconfiguredInputToStringTest()
        {
            var x = new Input<string>();
            Assert.DoesNotThrow(() => x.ToString());
            Assert.AreEqual("", x.ToString());
        }
        
        /// <summary>
        /// OpenTAP issue: #666
        /// </summary>
        [Test]
        public void ParallelIfVerdict()
        {
            var plan = new TestPlan();
            var par = new ParallelStep();
            var seq = new SequenceStep();
            var del1 = new DelayStep {DelaySecs = 0};
            var ifVerdict = new IfStep();
            var del2 = new DelayStep {DelaySecs = 0};

            plan.Steps.Add(par);
            par.ChildTestSteps.Add(seq);
            seq.ChildTestSteps.Add(del1);
            del1.Enabled = false;
            seq.ChildTestSteps.Add(ifVerdict);
            ifVerdict.ChildTestSteps.Add(del2);

            ifVerdict.InputVerdict.Step = del1;
            ifVerdict.InputVerdict.Property = TypeData.GetTypeData(del1).GetMember(nameof(del1.Verdict));

            var cancel = new CancellationTokenSource();
            var trd = TapThread.StartAwaitable(() =>
            {
                var r = plan.Execute();
                Assert.AreEqual(Verdict.NotSet, r.Verdict);
            }, cancel.Token);
            if (!trd.Wait(TimeSpan.FromMinutes(2)))
            {
                cancel.Cancel();
                Assert.Fail("Test timed out");
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(10)]
        public void ParallelIfVerdictDeadlock(int n)
        {
            var plan = new TestPlan();
            ParallelStep parallelStep = new ParallelStep();
            plan.Steps.Add(parallelStep);
            for (int i = 1; i < n; i++)
            {
                var step = new ParallelStep();
                parallelStep.ChildTestSteps.Add(step);
                parallelStep = step;
            }
            var ifVerdictStep = new IfStep();
            parallelStep.ChildTestSteps.Add(ifVerdictStep);
            ifVerdictStep.InputVerdict.Step = parallelStep;
            ifVerdictStep.InputVerdict.Property = TypeData.GetTypeData(parallelStep).GetMember(nameof(parallelStep.Verdict));
            
            Assert.AreEqual(plan.Execute().Verdict, Verdict.Error);
        }

        [Test]
        public void MovingStepWithInputTest()
        {
            var plan = new TestPlan();
            var ifVerdict = new IfStep
            {
            };
            var ifVerdict2 = new IfStep{};
            plan.ChildTestSteps.Add(ifVerdict);
            plan.ChildTestSteps.Add(ifVerdict2);
            ifVerdict2.InputVerdict.Step = ifVerdict;
            ifVerdict2.InputVerdict.Step = null;
            plan.ChildTestSteps.Remove(ifVerdict);
            plan.ChildTestSteps.Insert(0, ifVerdict);
            
            // this should still be null, but was not because of a (fixed) bug.
            Assert.IsNull(ifVerdict2.InputVerdict.Step);
        }

        [Test]
        public void RepeatVerdictTest()
        {
            var plan = new TestPlan();
            var repeat0 = new RepeatStep { Count = 2 };
            var repeat = new RepeatStep { Count = 2 };
            var delay = new DelayStep();
            var ifVerdict = new IfStep
            {
                Action = IfStep.IfStepAction.RunChildren,
                InputVerdict =
                {
                    Step = delay, Property = TypeData.GetTypeData(delay).GetMember(nameof(delay.Verdict))
                }
            };
            var setVerdict = new VerdictStep() {VerdictOutput = Verdict.Pass};
            plan.ChildTestSteps.Add(repeat0);
            repeat0.ChildTestSteps.Add(repeat);
            repeat0.ChildTestSteps.Add(ifVerdict);
            repeat.ChildTestSteps.Add(delay);
            ifVerdict.ChildTestSteps.Add(setVerdict);

            var run = plan.Execute();
            Assert.AreEqual(Verdict.Pass, run.Verdict);
        }
    }

    [AllowAnyChild]
    public class OutputParentStep : TestStep
    {
        [Output]
        public double OutputValue { get; set; }

        public override void Run()
        {
            RunChildSteps();
        }
    }

    public class GenerateOutputStep : TestStep
    {       
        [Output]
        public bool[] OutputBoolArray { get; private set; }
        [Output]
        public double OutputDouble { get; private set; }
        [Output]
        public double[] OutputDoubleArray { get; private set; }
        [Output]
        public string OutputString { get; private set; }
        [Output]
        public string[] OutputStringArray { get; private set; }
        [Output]
        public List<string> OutputStringList { get; private set; }

        public GenerateOutputStep()
        {
            OutputBoolArray = new bool[] { false, true };
            OutputDouble = 1.0;
            OutputDoubleArray = new double[] { 1.0, 2.2 };
            OutputString = "Something";
            OutputStringArray = new string[] { "tom", "dick" };
            OutputStringList = new List<string> { "One", "Two", "Three" };
        }

        public override void Run()
        {
            throw new NotImplementedException();
        }
    }

    [Display("Handle Input")]
    public class HandleInputStep : TestStep
    {
        [Display("Input Bool Array")]
        public Input<bool[]> InputBoolArray { get; set; }
        [Display("Input Double")]
        public Input<double> InputDouble { get; set; }
        [Display("Input Double Array")]
        public Input<double[]> InputDoubleArray { get; set; }
        [Display("Input String")]
        public Input<string> InputString { get; set; }
        [Display("Input String Array")]
        public Input<string[]> InputStringArray { get; set; }
        [Display("Input String List")]
        public Input<List<string>> InputStringList { get; set; }

        public HandleInputStep()
        {
            InputDouble = new Input<double>();
            InputDoubleArray = new Input<double[]>();
            InputBoolArray = new Input<bool[]>();
            InputString = new Input<string>();
            InputStringArray = new Input<string[]>();
            InputStringList = new Input<List<string>>();
        }

        public override void Run()
        {
            throw new NotImplementedException();
        }
    }
}
