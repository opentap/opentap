//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System;
using System.Globalization;
using System.Reflection;

namespace OpenTap.Engine.UnitTests
{
  
    [SetUpFixture]
    public class TapTestInit
    {
        [OneTimeSetUp]
        public static void AssemblyInit()
        {
            
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            EngineSettings.LoadWorkingDirectory(System.IO.Path.GetDirectoryName(typeof(TestStep).Assembly.Location));
            ThreadManager.IdleThreadCount = 0;
            SessionLogs.SkipStartupInfo = true;
            
            PluginManager.SearchAsync().Wait();
            SessionLogs.Initialize(string.Format("OpenTap.Engine.UnitTests {0}.TapLog", DateTime.Now.ToString("HH-mm-ss.fff")));

            Assembly engine = Assembly.GetAssembly(typeof(ITestStep));
             OpenTap.Log.CreateSource("UnitTest").Info("OpenTAP version '{0}' initialized {1}", PluginManager.GetOpenTapAssembly().SemanticVersion, DateTime.Now.ToString("MM/dd/yyyy"));
        }

        [OneTimeTearDown]
        public static void AssemblyCleanup()
        {
            SessionLogs.Flush();
            Log.Flush();
            Assert.IsTrue(Session.RootSession == Session.Current);
            if (TapThread.Current.AbortToken.IsCancellationRequested)
            {
                Assert.Fail("This should not occur.");
            }
            
            //Session.RootSession.Dispose();
            //Session.RootSession.DisposeSessionLocals();
        }
    }
}
