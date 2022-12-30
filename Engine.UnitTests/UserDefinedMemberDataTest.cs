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

        [Test]
        public void ExpressionTestStepTest()
        {
            var expressionStep = new ExpressionStep();
            var memberA = new UserDefinedDynamicMember()
            {
                Name = "A", 
                TypeDescriptor = TypeData.FromType(typeof(int)),
                Readable = true,
                Writable = true
            };
            var memberB = new UserDefinedDynamicMember()
            {
                Name = "B", 
                TypeDescriptor = TypeData.FromType(typeof(int)),
                Readable = true,
                Writable = true
            };
            var memberC = new UserDefinedDynamicMember()
            {
                Name = "C", 
                TypeDescriptor = TypeData.FromType(typeof(bool)),
                Readable = true,
                Writable = true
            };
            DynamicMember.AddDynamicMember(expressionStep, memberA);
            DynamicMember.AddDynamicMember(expressionStep, memberB);
            DynamicMember.AddDynamicMember(expressionStep, memberC);
            
            expressionStep.Expression = "A = B + 3; B = B * 2 + 1; C = B > 3";
            int a = 0;
            int b = 0;
            bool c = false;
            for (int i = 0; i < 10; i++)
            {
                expressionStep.Run();
                a = b + 3;
                b = b * 2 + 1;
                c = b > 3;
                var b1 = memberB.GetValue(expressionStep);
                var a1 = memberA.GetValue(expressionStep);
                var c1 = memberC.GetValue(expressionStep);
                Assert.AreEqual(a, a1);
                Assert.AreEqual(b, b1);
                Assert.AreEqual(c, c1);
            }
        }
    }
}