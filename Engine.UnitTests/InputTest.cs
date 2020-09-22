//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using OpenTap.Plugins.BasicSteps;
using NUnit.Framework;
using OpenTap;

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
