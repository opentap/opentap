using System;
using System.IO;
using System.Linq;
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

        [Test]
        public void TestErrorsSetFromStream()
        {
            { // Two missing dependency errors
                string content = @"<?xml version=""1.0"" encoding=""utf-8""?>
<DutSettings type=""OpenTap.DutSettings"">
  <Package.Dependencies>
    <Package Name=""Not Installed Plugin"" Version=""1.0.0"" />
    <Package Name=""Also Not Installed Plugin"" Version=""1.0.0"" />
  </Package.Dependencies>
</DutSettings>
";
                using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                ComponentSettings.SetCurrent(memoryStream, out var errors);

                Assert.AreEqual(2, errors.Count());
                CollectionAssert.Contains(errors.Select(e => e.Message),
                    "Package 'Not Installed Plugin' is required to load, but it is not installed.");
                CollectionAssert.Contains(errors.Select(e => e.Message),
                    "Package 'Also Not Installed Plugin' is required to load, but it is not installed.");
            }

            { // No xml errors
                var content = @"<?xml version=""1.0"" encoding=""utf-8""?>
<DutSettings type=""OpenTap.DutSettings"">
</DutSettings>";


                using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                ComponentSettings.SetCurrent(memoryStream, out var errors);


                Assert.AreEqual(0, errors.Count());
            }

            { // Invalid component setting -- missing type
                string content = @"<?xml version=""1.0"" encoding=""utf-8""?>
<DutSettings>
</DutSettings>
";
                using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                ComponentSettings.SetCurrent(memoryStream, out var errors);
                
                Assert.AreEqual(1, errors.Count());
                var err = errors.First().ToString();
                StringAssert.Contains("Stream does not contain valid ComponentSettings. Unable to determine ComponentSettings type from root attribute", err);
            }


            { // Invalid data
                var content = "Definitely not ComponentSettingsXML";
                using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                ComponentSettings.SetCurrent(memoryStream, out var errors);
                
                Assert.AreEqual(1, errors.Count());
                StringAssert.Contains("Data at the root level is invalid. Line 1, position 1.", errors.First().Message);
            }
        }

        [Display("Test Settings", Groups: new []{"Test Settings", "With Groups"})]
        public class TestSettingsWithGroups : ComponentSettings<TestSettingsWithGroups>
        {
            public int Value { get; set; }
        }

        [Test]
        public void TestSaveSettingsWithGroup()
        {
            var s = ComponentSettings<TestSettingsWithGroups>.Current;
            s.Value = 0;
            Assert.AreEqual(0, s.Value);
            s.Save();
            var newValue = 123;
            s.Value = newValue;
            Assert.AreEqual(newValue, s.Value);
            s.Save();
            s.Reload();
            s.Invalidate();
            s = ComponentSettings<TestSettingsWithGroups>.Current;
            Assert.AreEqual(newValue, s.Value);
        } 

        public class ThrowingSetting : ComponentSettings<ThrowingSetting>
        {
            public static bool Throw { get; set; } = false;
            public ThrowingSetting()
            {
                if (Throw)
                    throw new Exception("Some exception"); 
            }
        }

        [Test]
        public void TestSessionCreateDoesNotThrow()
        {
            try
            {
                ComponentSettings.InvalidateAllSettings();
                ThrowingSetting.Throw = true;
                Assert.That(ThrowingSetting.Current, Is.Null);
                using (Session.Create(SessionOptions.None))
                    Assert.That(ThrowingSetting.Current, Is.Null);
                using (Session.Create(SessionOptions.OverlayComponentSettings))
                    Assert.That(ThrowingSetting.Current, Is.Null);
            }
            finally
            {
                ThrowingSetting.Throw = false;
            }
        }

        [Test]
        public void TestSessionCreateDoesNotOverlayIncorrectInstance()
        { 
            ComponentSettings.InvalidateAllSettings();
            ThrowingSetting.Throw = false;
            // The first instance can be correctly constructed
            Assert.That(ThrowingSetting.Current, Is.Not.Null);
            // Make future constructors throw
            try
            {
                ThrowingSetting.Throw = true;
                // With no overlay settings, the first instance is returned
                using (Session.Create(SessionOptions.None))
                    Assert.That(ThrowingSetting.Current, Is.Not.Null);
                // With overlay settings, we cannot create a new instance
                using (Session.Create(SessionOptions.OverlayComponentSettings))
                    Assert.That(ThrowingSetting.Current, Is.Null);
                // The original instance is still used
                using (Session.Create(SessionOptions.None))
                    Assert.That(ThrowingSetting.Current, Is.Not.Null);
            }
            finally
            {
                ThrowingSetting.Throw = false;
            }
        }

        [Test]
        public void TestPersistenceOfSaveAll()
        {
            var instrumentSettingsSavePath = ComponentSettings.GetSaveFilePath(typeof(InstrumentSettings));
            if (File.Exists(instrumentSettingsSavePath))
                File.Delete(instrumentSettingsSavePath);
            var initialInstrumentSettings = InstrumentSettings.Current;

            try
            {
                InstrumentSettings instruments = new InstrumentSettings();
                instruments.Add(new ScpiDummyInstrument());
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (var xmlWriter = System.Xml.XmlWriter.Create(memoryStream, new System.Xml.XmlWriterSettings { Indent = true }))
                    {
                        var serializer = new TapSerializer();
                        serializer.Serialize(xmlWriter, instruments);
                    }
                    ComponentSettings.SetCurrent(memoryStream);
                }

                ComponentSettings.SaveAllCurrentSettings(); // Instument has been added and SaveAllCurrentSettings is called
                Assert.IsTrue(File.Exists(instrumentSettingsSavePath)); // This FAILS!
            }
            finally
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (var xmlWriter = System.Xml.XmlWriter.Create(memoryStream, new System.Xml.XmlWriterSettings { Indent = true }))
                    {
                        var serializer = new TapSerializer();
                        serializer.Serialize(xmlWriter, initialInstrumentSettings);
                    }
                    ComponentSettings.SetCurrent(memoryStream);
                }
            }
        }
    }
}
