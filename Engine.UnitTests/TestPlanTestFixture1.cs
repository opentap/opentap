//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System.IO;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class TestPlanTestFixture1
    {
        public class TestPlanTestStep : TestStep
        {
            public class TestPlanTestStep2 : TestStep
            {
                public override void Run()
                {
                }
            }

            public override void Run()
            {
            }
        }

        [Test]
        public void TestPlanSameTestStepNameTest1()
        {
            TestPlan target = new TestPlan();

            target.Steps.Add(new TestPlanTestStep());
            target.Steps.Add(new TestPlanTestStep.TestPlanTestStep2());

            using (var ms = new MemoryStream())
            {
                target.Save(ms);

                Assert.AreNotEqual(0, ms.Length);
            }
        }
    }
}
