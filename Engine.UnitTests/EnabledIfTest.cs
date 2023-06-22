using NUnit.Framework;
namespace OpenTap.UnitTests
{
    public class EnabledIfTest
    {
        class EnabledIfStep : TestStep
        {
            public double Z { get; set; }
            public double Y { get; set; }
            [EnabledIf("Y > 0")]
            [EnabledIf("Z > 0")]
            public double X { get; set; }
            
            [EnabledIf("Y > 0 && Y < 2")]
            public double W { get; set; }

            public override void Run()
            {
                
            }
        }

        [Test]
        public void EnabledIfStepTest()
        {
            EnabledIfStep step = new EnabledIfStep();
            var type = TypeData.GetTypeData(step);
            var member = type.GetMember(nameof(EnabledIfStep.X));
            var member2 = type.GetMember(nameof(EnabledIfStep.W));
            step.Z = 1;
            step.Y = 0;
            bool enabled1 = EnabledIfAttribute.IsEnabled(member, step);
            step.Y = 1;
            bool enabled2 = EnabledIfAttribute.IsEnabled(member, step);
            step.Y = 0;
            bool enabled3 = EnabledIfAttribute.IsEnabled(member, step);

            step.Z = 0;
            step.Y = 1;
            bool enabled4 = EnabledIfAttribute.IsEnabled(member, step);
            
            bool enabled5 = EnabledIfAttribute.IsEnabled(member2, step);
            step.Y = 3;
            bool enabled6 = EnabledIfAttribute.IsEnabled(member2, step);

            Assert.IsFalse(enabled1);
            Assert.IsTrue(enabled2);
            Assert.IsFalse(enabled3);
            Assert.IsFalse(enabled4);
            Assert.IsTrue(enabled5);
            Assert.IsFalse(enabled6);
        }
    }
}
