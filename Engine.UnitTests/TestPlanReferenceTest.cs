using System.IO;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;
namespace OpenTap.UnitTests;

[TestFixture]
public class TestPlanReferenceTest
{
    [Test]
    public void TestRemovingParameter()
    {
        var planWithoutParam = nameof(TestRemovingParameter) + ".no-parameter.TapPlan";
        var planWithParam = nameof(TestRemovingParameter) + ".parameter.TapPlan";
        try
        {
            {
                var plan = new TestPlan();
                var step = new DelayStep();
                plan.ChildTestSteps.Add(step);
                plan.Save(planWithoutParam);
                TypeData.GetTypeData(step).GetMember(nameof(step.DelaySecs)).Parameterize(plan, step, "A");
                plan.Save(planWithParam);
            }

            {
                var plan = new TestPlan();
                var tpr = new TestPlanReference();
                tpr.Filepath.Text = planWithParam;
                plan.ChildTestSteps.Add(tpr);
                tpr.LoadTestPlan();
                var aMember = TypeData.GetTypeData(tpr).GetMember("A");
                aMember.Parameterize(plan, tpr, "B");
                tpr.Filepath.Text = planWithoutParam;
                var mem0 = TypeData.GetTypeData(plan).GetMember("B");
                tpr.LoadTestPlan();
                var mem = TypeData.GetTypeData(plan).GetMember("B");
                
                Assert.IsNotNull(mem0);
                Assert.IsNull(mem);
            }
            
        }
        finally
        {
            File.Delete(planWithoutParam);
            File.Delete(planWithParam);
        }
    }
}
