using System;
using System.Linq.Expressions;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;
using OpenTap.Expressions;
using BinaryExpression = OpenTap.Expressions.BinaryExpression;
namespace OpenTap.UnitTests
{
    public class ExpressionTest
    {
        [Test]
        public void StepWithExpressionTest()
        {
            var step = new DelayStep();
            ExpressionManager.SetExpression(step, TypeData.GetTypeData(step).GetMember(nameof(step.DelaySecs)), "1.0 + 2.0");
            var expr = ExpressionManager.GetExpression(step, TypeData.GetTypeData(step).GetMember(nameof(step.DelaySecs)));
            Assert.AreEqual(expr, "1.0 + 2.0");
            ExpressionManager.Update(step);
            Assert.AreEqual(1.0 + 2.0, step.DelaySecs);
            step.DelaySecs = 0.0;
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(step);

            var xml = plan.SerializeToString();

            plan = (TestPlan)new TapSerializer().DeserializeFromString(xml);
            step = (DelayStep)plan.ChildTestSteps[0];
            var expr2 = ExpressionManager.GetExpression(step, TypeData.GetTypeData(step).GetMember(nameof(step.DelaySecs)));
            Assert.IsFalse(string.IsNullOrWhiteSpace(expr2));
            ExpressionManager.Update(step);
            Assert.AreEqual(1.0 + 2.0, step.DelaySecs);
        }
        
        [Test]
        public void StepWithExpression2Test()
        {
            var step = new LogStep();
            ExpressionManager.SetExpression(step, TypeData.GetTypeData(step).GetMember(nameof(step.LogMessage)), "The result is: {1.0 + 2.0}");
            ExpressionManager.Update(step);
            Assert.AreEqual("The result is: 3", step.LogMessage);
        }

        [Test]
        public void StringExpressionBasicTest()
        {
            var builder = new ExpressionCodeBuilder();
            var ast = builder.ParseStringInterpolation("The number is {1 + 2}.");
            var lmb = builder.GenerateLambda(ast, Array.Empty<ParameterExpression>(), typeof(string));
            var result = lmb.DynamicInvoke();
            Assert.AreEqual("The number is 3.", result);
        }

        [Test]
        public void ParseFancyNameTest()
        {
            var builder = new ExpressionCodeBuilder();
            var ast = builder.Parse("Log Message + Log Message 2");
            Assert.IsInstanceOf<BinaryExpression>(ast);
            var b = (BinaryExpression)ast;
            Assert.IsInstanceOf<ObjectNode>(b.Left);
            Assert.IsInstanceOf<ObjectNode>(b.Right);
            Assert.IsTrue(b.Operator == Operators.AdditionOp);

            Assert.AreEqual("Log Message", ((ObjectNode)b.Left).Data);
            Assert.AreEqual("Log Message 2", ((ObjectNode)b.Right).Data);

        }

        [Test]
        public void EvalExpressionWithProperty()
        {
            var step = new LogStep();
            var newMem = new UserDefinedDynamicMember
            {
                TypeDescriptor = TypeData.FromType(typeof(double)),
                Name = "A 1",
                Readable = true,
                Writable = true,
                DeclaringType = TypeData.FromType(typeof(TestStep))
            };    
            
            var newMem2 = new UserDefinedDynamicMember
            {
                TypeDescriptor = TypeData.FromType(typeof(double)),
                Name = "A 2",
                Readable = true,
                Writable = true,
                DeclaringType = TypeData.FromType(typeof(TestStep))
            };
            
            DynamicMember.AddDynamicMember(step, newMem);
            newMem.SetValue(step, 1.0);
            ExpressionManager.SetExpression(step, TypeData.GetTypeData(step).GetMember(nameof(step.LogMessage)), "The result is: {A 1}.");
            ExpressionManager.Update(step);
            
            DynamicMember.AddDynamicMember(step, newMem2);
            newMem2.SetValue(step, 2.0);

            ExpressionManager.Update(step);
            Assert.AreEqual("The result is: 1.", step.LogMessage);
        }

        [Test]
        public void TestRecallUserDefinedDynamicMember()
        {
            var step = new LogStep();
            var newMem = new UserDefinedDynamicMember
            {
                TypeDescriptor = TypeData.FromType(typeof(double)),
                Name = "A 1",
                Readable = true,
                Writable = true,
                DeclaringType = TypeData.FromType(typeof(TestStep)),
                DisplayName = "A 1",
                Description = "",
                Group = "",
                Output = false,
                Order = 0,
                Result = false
            };    
            DynamicMember.AddDynamicMember(step, newMem);
            newMem.SetValue(step, 2.0);
            var xml = TapSerializer.SerializeToXml(step);
            step = xml.Deserialize();
            
            var member = TypeData.GetTypeData(step).GetMember(newMem.Name);
            var value = member.GetValue(step);
            Assert.AreEqual(2.0, value);
            Assert.IsTrue(member.TypeDescriptor == TypeData.FromType(typeof(double)));
            Assert.IsTrue(TypeData.GetTypeData(step).DescendsTo(member.DeclaringType));
        }

        [Test]
        public void ExpressionsWithDependingMembers()
        {
            var step = new LogStep();
            var newMem = new UserDefinedDynamicMember
            {
                TypeDescriptor = TypeData.FromType(typeof(double)),
                Name = "A 1",
                Readable = true,
                Writable = true,
                DeclaringType = TypeData.FromType(typeof(TestStep))
            };    
            var newMem2 = new UserDefinedDynamicMember
            {
                TypeDescriptor = TypeData.FromType(typeof(double)),
                Name = "A 2",
                Readable = true,
                Writable = true,
                DeclaringType = TypeData.FromType(typeof(TestStep))
            };   
            DynamicMember.AddDynamicMember(step, newMem);
            DynamicMember.AddDynamicMember(step, newMem2);
            newMem.SetValue(step, 1.0);
            newMem2.SetValue(step, 2.0);
            ExpressionManager.SetExpression(step, TypeData.GetTypeData(step).GetMember(nameof(step.LogMessage)), "The result is {A 1 + A 2}.");
            ExpressionManager.SetExpression(step, newMem, "3 + 4");
            ExpressionManager.SetExpression(step, newMem2, "5 + 5");
            ExpressionManager.Update(step);
            Assert.AreEqual("The result is 17.", step.LogMessage);
        }
    }
}
