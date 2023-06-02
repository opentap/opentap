using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;
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
            Assert.AreEqual(step.DelaySecs, 3.0);
        }
    }
}
