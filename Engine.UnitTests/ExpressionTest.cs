using System;
using System.Linq;
using System.Linq.Expressions;
using NUnit.Framework;
using OpenTap.Engine.UnitTests;
using OpenTap.Plugins.BasicSteps;
using OpenTap.Expressions;
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
        
        [TestCase("{floor(3.5)}", "3")]
        [TestCase("{floor(1 + 3.5)}", "4")]
        
        [TestCase("{ceiling(3.5 + 0.6)}", "5")]
        [TestCase("{ceiling(3.5 + 0.6) + floor(-1.5)}", "3")]
        [TestCase("{round(1.11111111, 2)}", "1.11")]
        [TestCase("{sin(π * 2.0)} {cos(π * 2.0)}", "0 1")]
        [TestCase("{cos(π * 2.0)} {sin(π * 2.0)}", "1 0")]
        [TestCase("{π}", "3.14159265358979")]
        [TestCase("{true}", "True")]
        [TestCase("{false}", "False")]
        [TestCase("{abs(-1.0)}", "1")]
        [TestCase("{empty(\"\")}", "True")]
        [TestCase("{empty(\"asd\")}", "False")]
        public void StringExpressionBasicTest(string expression, string expectedResult)
        {
            var builder = new ExpressionCodeBuilder();
            var ast0 = builder.ParseStringInterpolation(expression);
            var ast = ast0.Unwrap();
            var lmb = builder.GenerateLambda(ast, ParameterData.Empty, typeof(string));
            var result = lmb.Unwrap().DynamicInvoke();
            Assert.AreEqual(expectedResult, result);
        }

        [TestCase( "123 {floor(1.5} 321432", "Unexpected symbol '}'.", null)]
        [TestCase( "123 {1.5) 321432", "Unexpected symbol ')'.", null)]
        public void StringExpressionParseErrors(string errorExpression, string parseError, string compileError)
        {
            var builder = new ExpressionCodeBuilder();
            var ast = builder.ParseStringInterpolation(errorExpression);
            if (parseError != null)
            {
                Assert.AreEqual(parseError, ast.Error);
                return;
            }
            Assert.AreEqual(null, ast.Error);
            
            var lmb = builder.GenerateLambda(ast.Unwrap(), ParameterData.Empty, typeof(string));

            if (compileError != null)
            {
                StringAssert.IsMatch(compileError, lmb.Error);
                return;
            }
            Assert.AreEqual(null, lmb.Error);
        }

        [TestCase("abs(-1.0)", 1.0)]
        [TestCase("abs(2.0)", 2.0)]
        [TestCase("cos(π)", -1.0)]
        [TestCase("cos(2 * π)", 1.0)]
        [TestCase("sin(2 * π)", 0.0)]
        [TestCase("sin(π)", 0.0)]
        [TestCase("empty(\"\")", true)]
        [TestCase("1 * 2 * 3 * 4 * 5", 120)]
        [TestCase("(1 * 2 * 3 * 4 * 5) / 10", 12)]
        [TestCase("π", Math.PI)]
        [TestCase("Pi", Math.PI)]
        public void ExpressionBasicTest(string expression, object expectedResult)
        {
            var builder = new ExpressionCodeBuilder();
            var ast = builder.Parse(expression);
            var lmb = builder.GenerateLambda(ast.Unwrap(), ParameterData.Empty, typeof(object));
            var result = lmb.Unwrap().DynamicInvoke();
            if (expectedResult is double d)
            {
                Assert.AreEqual((double)result, d, 1e-15);
            }
            else
            {
                Assert.AreEqual(expectedResult, result);
            }
        }
        
        [TestCase(typeof(double), "asd(1 + 2)", null, "'asd' function not found.")]
        [TestCase(typeof(double), "1 + 2", null, null)]
        [TestCase(typeof(double), "asd", null, "'asd' symbol not found.")]
        [TestCase(typeof(double), "cos(\"asd\")", null, "Invalid argument types for 'cos'.")]
        [TestCase(typeof(double), "cos(1.0, 2.0)", null, "Invalid number of arguments for 'cos'.")]
        [TestCase(typeof(double), "cos(1.0}", "Unexpected symbol '}'.", null)]
        [TestCase(typeof(double), "Pi(1.0, 2.0)", null, "'Pi' cannot be used as a function.")]
        [TestCase(typeof(double), ")", "Unexpected symbol ')'.", null)]
        [TestCase(typeof(double), "}", "Unexpected symbol '}'.", null)]
        public void SimpleErrors(Type expressionType, string errorExpression, string parseError, string compileError)
        {
            
            var builder = new ExpressionCodeBuilder();
            var ast = builder.Parse(errorExpression);
            if (parseError != null)
            {
                Assert.AreEqual(parseError, ast.Error);
                return;
            }
            Assert.AreEqual(null, ast.Error);
            
            
            var lmb = builder.GenerateLambda(ast.Unwrap(), ParameterData.Empty, expressionType);

            if (compileError != null)
            {
                StringAssert.IsMatch(compileError, lmb.Error);
                return;
            }
            Assert.AreEqual(null, lmb.Error);
        }
        


        [Test]
        public void ParseFancyNameTest()
        {
            var builder = new ExpressionCodeBuilder();
            var ast = builder.Parse("Log Message + Log Message 2").Unwrap();
            Assert.IsInstanceOf<BinaryExpressionNode>(ast);
            var b = (BinaryExpressionNode)ast;
            Assert.IsInstanceOf<ObjectNode>(b.Left);
            Assert.IsInstanceOf<ObjectNode>(b.Right);
            Assert.IsTrue(b.Operator == Operators.Addition);

            Assert.AreEqual("Log Message", ((ObjectNode)b.Left).Content);
            Assert.AreEqual("Log Message 2", ((ObjectNode)b.Right).Content);

        }

        [Test]
        public void EvalExpressionWithProperty()
        {
            var seq = new SequenceStep();
            var delay = new DelayStep();
            var log = new LogStep();
            seq.ChildTestSteps.Add(delay);
            seq.ChildTestSteps.Add(log);
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(seq);

            var aMember = TypeData.GetTypeData(delay).GetMember(nameof(delay.DelaySecs)).Parameterize(seq, delay, "A 1");
            var bMember = TypeData.GetTypeData(log).GetMember(nameof(log.LogMessage)).Parameterize(seq, log, "B");
            
            ExpressionManager.SetExpression(seq, bMember, "The result is: {A 1}.");
            ExpressionManager.Update(seq);
            var msg1 = log.LogMessage;
            aMember.SetValue(delay, 2.0);
            ExpressionManager.Update(seq);
            var msg2 = log.LogMessage;
            
            Assert.IsNotNull(TypeData.GetTypeData(seq).GetMember(aMember.Name));
            aMember.Remove();
            Assert.IsNull(TypeData.GetTypeData(seq).GetMember(aMember.Name));
            
            ExpressionManager.Update(seq);
            var msg3 = log.LogMessage;
            
            ExpressionManager.SetExpression(seq, bMember, "The result is: {1 + 5}.");
            ExpressionManager.Update(seq);
            var msg4 = log.LogMessage;
            
            Assert.IsNotNull(TypeData.GetTypeData(seq).GetMember(bMember.Name));
            bMember.Remove();
            Assert.IsNull(TypeData.GetTypeData(seq).GetMember(bMember.Name));

            ExpressionManager.Update(seq);
            var msg5 = log.LogMessage;

            Assert.AreEqual("The result is: 0.1.", msg1);
            Assert.AreEqual("The result is: 2.", msg2);
            Assert.AreEqual("The result is: 2.", msg3);
            Assert.AreEqual("The result is: 6.", msg4);
            Assert.AreEqual("The result is: 6.", msg5);
        }


        [Test]
        public void ExpressionAnnotationTest()
        {
            using (OpenTap.Session.Create(SessionOptions.OverlayComponentSettings))
            {
                void handleExpression(object item)
                {
                    var a = AnnotationCollection.Annotate(item);
                    
                    a.GetMember("Expression").Get<IStringValueAnnotation>().Value = "asd(3 + 4)";
                    a.Write();
                    a.Read();
                    Assert.IsFalse(string.IsNullOrEmpty(((ValidatingObject)item).Error));
                    
                    
                    a.GetMember("Expression").Get<IStringValueAnnotation>().Value = "3 + 4";
                    a.Write();
                    a.Read();
                    Assert.IsTrue(string.IsNullOrEmpty(((ValidatingObject)item).Error));
                }

                UserInput.SetInterface(new UserInputTestImpl { Func = handleExpression });
                var delayStep = new DelayStep();
                var t = TypeData.GetTypeData(delayStep).GetMember(nameof(delayStep.DelaySecs));

                var member = AnnotationCollection.Annotate(delayStep).GetMember(nameof(delayStep.DelaySecs));
                member.ExecuteIcon(IconNames.AssignExpression);

                ExpressionManager.Update(delayStep);
                
                Assert.AreEqual(3 + 4, delayStep.DelaySecs);

                
                var a = AnnotationCollection.Annotate(delayStep)
                    .GetMember(nameof(delayStep.DelaySecs));
                
                var hasExpr = a.GetAll<IIconAnnotation>().FirstOrDefault(x => x.IconName == IconNames.HasExpression);
            
                Assert.AreEqual(3 + 4, delayStep.DelaySecs);
                Assert.IsNotNull(hasExpr);

                var expra = a.Get<IExpressionAnnotation>();
                expra.Expression = "asd(123)";
                a.Write();
                a.Read();
                Assert.AreEqual("'asd' function not found.", expra.Error);
                
                expra.Expression = "123 + 345";
                a.Write();
                a.Read();
                Assert.IsTrue(string.IsNullOrWhiteSpace(expra.Error));

            }
        }
    }
}
    