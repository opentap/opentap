//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
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
            var prop = typeof(DelayStep).GetProperty(nameof(DelayStep.DelaySecs));
            DelayStep delay = new DelayStep();
            Input<double> secs = new Input<double>() { Step = delay, Property = prop};
            delay.DelaySecs = 2;
            Assert.AreEqual(secs.Value, delay.DelaySecs);

            Input<double> secs2 = new Input<double>() { Step = delay, Property = prop };
            Assert.AreEqual(secs, secs2);

            Input<double> secs3 = new Input<double>() { Step = delay, Property = null };
            Input<double> secs4 = new Input<double>() { Step = null, Property = prop };
            Input<double> secs5 = new Input<double>() { Step = null, Property = null };
            Assert.IsFalse(secs3 == secs4);
            Assert.IsFalse(secs4 == secs5);
            Assert.IsFalse(secs3 == secs5);
            Assert.IsTrue(secs == secs2);

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
}
