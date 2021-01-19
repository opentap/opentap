using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    public class ReflectionTest
    {
        
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
    }
}