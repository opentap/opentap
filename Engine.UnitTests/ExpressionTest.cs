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

        [TestCase("The number is {1 + 2}.", "The number is 3.")]
        [TestCase("{1 - 2}. {4}. {10 /5}.", "-1. 4. 2.")]
        [TestCase("", "")]
        [TestCase("123abc", "123abc")]
        [TestCase("{{123abc}}{5 + 10}{{123}}", "{123abc}15{123}")]
        [TestCase("{5}", "5")]
        [TestCase("{5+4+1}", "10")]
        [TestCase("{-5+-4+-1}", "-10")]
        [TestCase("{-5+4+1}", "0")]
        [TestCase("a b {$\"c {1 + 5}\"} e f g", "a b c 6 e f g")]
        
        [TestCase("{Math.Floor(3.5)}", "3")]
        [TestCase("{Math.Floor(1 + 3.5)}", "4")]
        
        [TestCase("{Math.Ceiling(3.5 + 0.6)}", "5")]
        [TestCase("{Math.Ceiling(3.5 + 0.6) + Math.Floor(-1.5)}", "3")]
        [TestCase("{Math.Round(1.11111111, 2)}", "1.11")]
        [TestCase(@"{Match(""A(?<B>\d*)"", ""B"", ""A112233"")}", "112233")]
        [TestCase("{Match(\"A\\d*\", \"A112233\nB1234\nA111\")}", "A112233")]
        public void StringExpressionBasicTest(string expression, string expectedResult)
        {
            
            var builder = new ExpressionCodeBuilder();
            var ast = builder.ParseStringInterpolation(expression);
            var lmb = builder.GenerateLambda(ast, Array.Empty<ParameterExpression>(), typeof(string));
            var result = lmb.DynamicInvoke();
            Assert.AreEqual(expectedResult, result);
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

        [Test]
        public void ExpressionAnnotationTest()
        {
            var delayStep = new DelayStep();
            var a = AnnotationCollection.Annotate(delayStep);
            var member = a.GetMember("DelaySecs");
            var sv = member.Get<IStringValueAnnotation>();
            sv.Value = "1 + 2";
            a.Write();
            Assert.AreEqual(3.0, delayStep.DelaySecs);
            
            a = AnnotationCollection.Annotate(delayStep);
            member = a.GetMember("DelaySecs");
            var sv2 = member.Get<IStringValueAnnotation>();
            var currentValue = sv2.Value;
            Assert.AreEqual("1 + 2", currentValue);
        }
        
        [Test]
        public void ExpressionMultiSelectAnnotationTest()
        {
            var step1 = new LogStep();
            var step2 = new LogStep();
            var steps = new[]
            {
                step1, step2
            };
            var a = AnnotationCollection.Annotate(steps);
            var member = a.GetMember(nameof(step1.LogMessage));
            var sv = member.Get<IStringValueAnnotation>();
            sv.Value = "{1 + 2}";
            a.Write();
            Assert.AreEqual("3", step1.LogMessage);
            Assert.AreEqual("3", step2.LogMessage);
        }
        
        [Test]
        public void InputExpressionTest2()
        {
            var step1 = new LogStep()
            {
                Name = "step1",
                LogMessage = "123"
            };
            
            var step2 = new LogStep();
            
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(step1);
            plan.ChildTestSteps.Add(step2);
            ExpressionManager.SetExpression(step2, TypeData.GetTypeData(step2).GetMember(nameof(step2.LogMessage)), "{Input(\"step1\", \"Log Message\")}4");
            ExpressionManager.Update(step2);

            Assert.AreEqual("1234", step2.LogMessage);

        }

        [Test]
        public void InputExpressionTest3()
        {
            var step1 = new DelayStep()
            {
                Name = "step1",
                DelaySecs = 0.5
            };
            
            var step2 = new DelayStep();
            
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(step1);
            plan.ChildTestSteps.Add(step2);
            ExpressionManager.SetExpression(step2, TypeData.GetTypeData(step2).GetMember(nameof(step2.DelaySecs)), "Input(\"step1\", \"Time Delay\") * 2.0");
            ExpressionManager.Update(step2);

            Assert.AreEqual(0.5 * 2.0, step2.DelaySecs);

        }
        
    }
}
