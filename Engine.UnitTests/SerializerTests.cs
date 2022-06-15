using System.Xml.Linq;
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class SerializerTests
    {
        public class TestPlanWithMetaData : TestPlan
        {
            [MetaData(Name= "X Setting")]
            public string X { get; set; }
            
            [MetaData]
            public string Y { get; set; }
        } 
        [Test]
        public void TestSerializeTestPlanMetaData()
        {
            var plan = new TestPlanWithMetaData();
            var xml = plan.SerializeToString();
            var xdoc = XDocument.Parse(xml);
            Assert.AreEqual("X Setting", xdoc.Root.Element("X").Attribute("Metadata").Value);
            Assert.AreEqual("Y", xdoc.Root.Element("Y").Attribute("Metadata").Value);
        }
    
    }
}
