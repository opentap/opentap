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
            char[] chars = new char[] { 's', 'e', 'c', 'r', 'e', 't' };
            foreach(char c in chars)
                inst.Password.AppendChar(c);
            
            string xml = new TapSerializer().SerializeToString(inst);
            var inst2 = (SomeInstrument)new TapSerializer().DeserializeFromString(xml, TypeData.GetTypeData(inst));
            Assert.AreEqual(inst.Password.ToString(), inst2.Password.ToString());
        }
    }
}