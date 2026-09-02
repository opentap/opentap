using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class SecureStringSerializerTest
    {
        public class SomeInstrument
        {
            public string UserName { get; set; } = "XYZ";
            public System.Security.SecureString Password { get; set; } = new System.Security.SecureString();
        }

        [Test]
        public void SerializationTest()
        {
            SomeInstrument inst = new SomeInstrument();
            var chars = "123456789012345678901234567890";
            foreach(char c in chars)
                inst.Password.AppendChar(c);
            
            string xml = new TapSerializer().SerializeToString(inst);
            var inst2 = (SomeInstrument)new TapSerializer().DeserializeFromString(xml, TypeData.GetTypeData(inst));
            Assert.AreEqual(inst.Password.ConvertToUnsecureString(), inst2.Password.ConvertToUnsecureString());
        }
    }
}