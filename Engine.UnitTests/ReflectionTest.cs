using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    public class ReflectionTest
    {
        public double Member1 { get; set; }
        public double Member2 { get; set; }

        [Test]
        public void TestDisplayAttribute()
        {
            { // dialog step
                var dialogStep = new DialogStep();
                var dialogStepType = TypeData.GetTypeData(dialogStep);
                var dialogStepDisplay = dialogStepType.GetDisplayAttribute();
                Assert.IsFalse(dialogStepDisplay.IsDefaultAttribute());
                Assert.AreEqual("Dialog", dialogStepDisplay.Name);
                Assert.AreEqual("Basic Steps", dialogStepDisplay.Group[0]);
                Assert.AreEqual(DisplayAttribute.DefaultOrder, dialogStepDisplay.Order);
            }
            { 
                var otherClass = TypeData.FromType(typeof(ReflectionTest));
                var otherClassDisplay = otherClass.GetDisplayAttribute();
                Assert.IsTrue(otherClassDisplay.IsDefaultAttribute());
                Assert.AreEqual(nameof(ReflectionTest), otherClassDisplay.Name);
                Assert.AreEqual(0, otherClassDisplay.Group.Length);
                Assert.AreEqual(DisplayAttribute.DefaultOrder, otherClassDisplay.Order);
            }
        }

        [Test]
        public void DisplayAttributeEqualityCheck()
        {
            var otherClass = TypeData.FromType(typeof(ReflectionTest));
            var mem1Display = otherClass.GetMember(nameof(Member1)).GetDisplayAttribute();
            var mem2Display = otherClass.GetMember(nameof(Member2)).GetDisplayAttribute();
            var mem1Display2 = otherClass.GetMember(nameof(Member1)).GetDisplayAttribute();
            Assert.AreEqual(nameof(Member1), mem1Display.Name);
            Assert.AreEqual(nameof(Member2), mem2Display.Name);
            Assert.AreNotEqual(mem1Display, mem2Display);
            Assert.AreEqual(mem1Display, mem1Display2);
        }
    }
}