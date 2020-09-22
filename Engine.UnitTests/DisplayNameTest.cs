using NUnit.Framework;

namespace OpenTap.UnitTests
{
    public class DisplayNameTest
    {
        [Display("Custom Bench Settings", Description: "Custom bench setting")]
        public abstract class CustomBenchSettings : Resource { }

        [Display("Real A Name", Description: "Example custom bench setting.")]
        public class WrongAName : CustomBenchSettings { }

        [Display("Real B Name", Description: "Another example custom bench setting.")]
        public class WrongBName : WrongAName { }

        [Test]
        public void TestDisplayName()
        {
            var aType = TypeData.FromType(typeof(WrongAName));
            var bType = TypeData.FromType(typeof(WrongBName));

            var displayDataA = aType.GetAttribute<DisplayAttribute>();
            var displayDataB = bType.GetAttribute<DisplayAttribute>();

            Assert.IsTrue(displayDataA.Name.Equals("Real A Name"));
            Assert.IsTrue(displayDataB.Name.Equals("Real B Name"));
        }
    }
}