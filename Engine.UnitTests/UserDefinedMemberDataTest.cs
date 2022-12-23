using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.Engine.UnitTests
{
    public class UserDefinedMemberDataTest
    {
        [Test]
        public void UseUserDefinedMemberData()
        {
            var step = new LogStep();
            var plan = new TestPlan();  
            plan.Steps.Add(step);
            var member = new UserDefinedDynamicMember()
            {
                Name = "Test", 
                TypeDescriptor = TypeData.FromType(typeof(int)),
                Readable = true,
                Writable = true
            };
            DynamicMember.AddDynamicMember(step, member);
            var userdefined = UserDefinedDynamicMember.GetUserDefinedMembers(step);
            Assert.AreEqual(1, userdefined.Length);
            member.SetValue(step, 10);
            var testMemberValue = member.GetValue(step);

            var xml = plan.SerializeToString();
            var serializer = new TapSerializer();
            var plan2 = (TestPlan)serializer.DeserializeFromString(xml);
            var step2 = (LogStep)plan2.Steps[0];

            var testMemberValue2 = member.GetValue(step2);
            
            Assert.AreEqual("", string.Join(",", serializer.Errors));
            Assert.AreEqual(10, testMemberValue);
            Assert.AreEqual(10, testMemberValue2);
        }
    }
}