using System;
using System.IO;
using System.Text;
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


        [Test]
        public void TestSetCurrentComponentSettings()
        {
            var initialEngineSettings = EngineSettings.Current;

            try
            {
                string operatorName = "John";
                EngineSettings engineSettings = new EngineSettings();
                engineSettings.OperatorName = operatorName;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (var xmlWriter = System.Xml.XmlWriter.Create(memoryStream, new System.Xml.XmlWriterSettings { Indent = true }))
                    {
                        var serializer = new TapSerializer();
                        serializer.Serialize(xmlWriter, engineSettings);
                    }
                    ComponentSettings.SetCurrent(memoryStream);
                }
                Assert.AreEqual(operatorName, EngineSettings.Current.OperatorName);
            }
            finally
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (var xmlWriter = System.Xml.XmlWriter.Create(memoryStream, new System.Xml.XmlWriterSettings { Indent = true }))
                    {
                        var serializer = new TapSerializer();
                        serializer.Serialize(xmlWriter, initialEngineSettings);
                    }
                    ComponentSettings.SetCurrent(memoryStream);
                }
            }
        }

        [Test]
        public void TestInvalidSetCurrentSettings()
        {
            string content = "Definitely not ComponentSettingsXML";
            try
            {
                using (MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
                    ComponentSettings.SetCurrent(memoryStream);

                Assert.Fail("This should not pass");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.Contains(content));
            }
        }
    }
}