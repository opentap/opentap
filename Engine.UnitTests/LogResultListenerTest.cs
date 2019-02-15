//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap.EngineUnitTestUtils;
using NUnit.Framework;
using OpenTap;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class LogResultListenerTest : EngineTestBase
    {
        
        public class TestStepEmit : TestStep
        {
            public TestStepEmit()
            {
                Name = "Emit";
            }
            public override void Run()
            {
                Log.Debug("This was called");
            }
        }

        [Test]
        public void LogResultListener()
        {
            LogResultListener log = new LogResultListener { FilePath = new MacroString { Text = "logResult.txt" } };
            var expanded = log.FilePath.Expand();

            // If debugging the LogResultListener will just rename the file since it might exist
            if (System.IO.File.Exists(expanded))
                System.IO.File.Delete(expanded);

            ResourceTest.TestConformance(log);
            var testPlan = TestStepTest.CreateGenericTestPlan();
            testPlan.Steps.Add(new TestStepEmit());
            ResultSettings.Current.Add(log);
            var run = testPlan.Execute();

            var logText = System.IO.File.ReadAllText(expanded, System.Text.Encoding.ASCII);
            StringAssert.Contains("This was called", logText);

            ResultSettings.Current.Remove(log);
        }
    }
}
