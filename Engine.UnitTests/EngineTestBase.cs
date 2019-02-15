//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Reflection;

namespace OpenTap.Engine.UnitTests
{
    /// <summary>
    /// Making your unit test class inherit from this ensures that a message is written in the Session log containing the name of the test
    /// </summary>
    [TestFixture]
    public abstract class EngineTestBase
    {
        //public abstract TestContext TestContext { get; set; }

        protected static TraceSource testLog =  OpenTap.Log.CreateSource("UnitTest");

        [SetUp]
        public void Init()
        {
            testLog.Info("############# Starting Test {0}", "");
        }

        [TearDown]
        public void Cleanup()
        {
            testLog.Info("############# Completed Test {0} with verdict {1}", "", "");
        }
    }

    
    [SetUpFixture]
    public class TapTestInit
    {
        [OneTimeSetUp]
        public static void AssemblyInit()
        {
            EngineSettings.LoadWorkingDirectory(System.IO.Path.GetDirectoryName(typeof(TestStep).Assembly.Location));
            PluginManager.SearchAsync().Wait();
            SessionLogs.Initialize(string.Format("Tap.Engine.UnitTests {0}.TapLog", DateTime.Now.ToString("HH-mm-ss.fff")));

            Assembly engine = Assembly.GetAssembly(typeof(ITestStep));
             OpenTap.Log.CreateSource("UnitTest").Info("TAP version '{0}' initialized {1}", PluginManager.GetOpenTapAssembly().SemanticVersion, DateTime.Now.ToString("MM/dd/yyyy"));
        }

        [OneTimeTearDown]
        public static void AssemblyCleanup()
        {
            SessionLogs.Flush();
            Log.Flush();
        }
    }
}
