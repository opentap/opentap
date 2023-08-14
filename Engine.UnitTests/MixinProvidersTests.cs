using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;
namespace OpenTap.UnitTests
{
    [TestFixture]
    public class MixinProvidersTests
    {
        [Test]
        public void TestNumberMixin()
        {
            var step = new DelayStep();
            var a = AnnotationCollection.Annotate(step);
            var menu = a.Get<MenuAnnotation>();

        }
        
    }
}
