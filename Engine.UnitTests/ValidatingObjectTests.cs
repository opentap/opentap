//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class ValidatingObjectTests 
    {
        private class ValidatingObjectTest : ValidatingObject
        {
            public double Freq { get; set; }

            public ValidatingObjectTest()
            {
                Rules.Add(() => Freq > 10, "Error", "Freq");
            }
        }

        [Test]
        public void ReturnErrorTest()
        {
            ValidatingObjectTest test = new ValidatingObjectTest();
            test.Freq = 5;
            Assert.AreEqual("Error", test.Error);
        }

        [Test]
        public void ReturnNoErrorTest()
        {
            ValidatingObjectTest test = new ValidatingObjectTest();
            test.Freq = 15;
            Assert.AreEqual("", test.Error);
        }

        private class DerivedValidatingTest : ValidatingObject
        {
            public double Freq { get; set; }

            protected override string GetError(string propertyName = null)
            {
                return "Derived error";
            }

            public DerivedValidatingTest()
            {
                Rules.Add(() => Freq > 10, "Error", "Freq");
            }
        }

        [Test]
        public void ReturnInheritErrorTest()
        {
            DerivedValidatingTest test = new DerivedValidatingTest();
            test.Freq = 5;
            Assert.AreEqual("Derived error", test.Error);
        }

        public class StepWithValidationAttribute : TestStep
        {
            [Validation("X > 0")]
            [Validation("X < 10")]
            public double X { get; set; }
            
            [Validation("Y < 100")]
            public double Y { get; set; }

            public override void Run()
            {
                
            }
        }
        
        [Test]
        public void ValidationAttributeTest()
        {
            var step = new StepWithValidationAttribute();
            Assert.AreEqual("X > 0", step.Error);
            step.X = 12;
            Assert.AreEqual("X < 10", step.Error);
            step.X = 5;
            Assert.AreEqual("", step.Error);
            step.Y = 200;
            Assert.AreEqual("Y < 100", step.Error);
        }
        

        #region CallOrderTest
        private class CallOrderTestObject : ValidatingObject
        {
            public double Freq { get; set; }

            private bool hasIsValidBeenCalled = false;

            private bool isFreqValid()
            {
                hasIsValidBeenCalled = true;
                return false;
            }
            private string getErrorMessage()
            {
                Assert.IsTrue(hasIsValidBeenCalled, "CustomErrorDelegate called before IsValidDelegate");
                return "Error";
            }

            public CallOrderTestObject()
            {
                Rules.Add(() => isFreqValid(), () => getErrorMessage(), "Freq");
            }
        }

        [Test]
        public void CallOrderTest()
        {
            CallOrderTestObject test = new CallOrderTestObject();
            Assert.AreEqual("Error", test.Error);
        }
        #endregion
    }
}
