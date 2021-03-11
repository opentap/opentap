using System;
using NUnit.Framework;
using OpenTap.Engine.UnitTests;

namespace OpenTap.UnitTests
{
    public class ComponentSettingsTest
    {

        [Test]
        public void TestCacheInvalidated()
        {
            using (Session.Create())
            {
                EngineSettings.Current.OperatorName = "TEST";
                Assert.AreEqual("TEST", EngineSettings.Current.OperatorName);

                int callCount = 0;
                EngineSettings.Current.CacheInvalidated += (s, e) =>
                {
                    Assert.IsTrue(s is EngineSettings);
                    callCount += 1;
                };
                EngineSettings.Current.Invalidate();
                Assert.AreEqual(1, callCount);
                Assert.AreNotEqual("TEST", EngineSettings.Current.OperatorName);
                EngineSettings.Current.OperatorName = "TEST";
                
                // The event must be re-configured when the cache is invalidated.
                EngineSettings.Current.CacheInvalidated += (s, e) =>
                {
                    Assert.IsTrue(s is EngineSettings);
                    callCount += 1;
                };
                
                ComponentSettings.InvalidateAllSettings();
                Assert.AreEqual(2, callCount);

            }

            Assert.AreNotEqual("TEST", EngineSettings.Current.OperatorName);
        }
    }
}