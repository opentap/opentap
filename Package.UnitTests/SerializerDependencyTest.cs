using System.Linq;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class SerializerDependencyTest
    {
        [Test]
        public void VerifyPackageDependencies()
        {
            var tp = new TestPlan();
            tp.ChildTestSteps.Add(new DelayStep());
            var serializer = new TapSerializer();
            
            var str = serializer.SerializeToString(tp);
            Assert.IsTrue(str.Contains("<Package.Dependencies>"));
            Assert.IsTrue(serializer.GetUsedTypes().Contains(TypeData.FromType(typeof(DelayStep))));
        }
    }
}