//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class ValidatingObjectTests : EngineTestBase
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
