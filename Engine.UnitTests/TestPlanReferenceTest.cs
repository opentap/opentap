using System.IO;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;
namespace OpenTap.UnitTests;

[TestFixture]
public class TestPlanReferenceTest
{
    [Test]
    public void RemovingParameterFromReferencedPlan()
    {
        // this unit test checks that a parameterization is automatically removed if
        // the parameter comes from a test plan reference which initially has the parameter
        // but later the referenced plan removes the parameter after which
        // the test plan is reloaded.
        
        var planWithoutParam = nameof(RemovingParameterFromReferencedPlan) + ".no-parameter.TapPlan";
        var planWithParam = nameof(RemovingParameterFromReferencedPlan) + ".parameter.TapPlan";
        try
        {
            {
                // create two test plans, one with the external parameter 'A' and one without.
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
                
                // first we load the test plan reference containing the parameter A and 
                // parameterize that onto the plan.
                
                tpr.Filepath.Text = planWithParam;
                plan.ChildTestSteps.Add(tpr);
                tpr.LoadTestPlan();
                var aMember = TypeData.GetTypeData(tpr).GetMember("A");
                aMember.Parameterize(plan, tpr, "B");
                var memberBPre = TypeData.GetTypeData(plan).GetMember("B");
                
                // now we select the test plan _without_ the parameter A
                // and load that with the parameter B still existing on the test plan.
                
                tpr.Filepath.Text = planWithoutParam;
                tpr.LoadTestPlan();
                
                var memberBPost = TypeData.GetTypeData(plan).GetMember("B");
                
                // if everything works, we should no longer be able to access 'B'.
                // it is automatically removed by the parameter sanitation algorithm, ParameterManager.checkParameterSanity.
                
                Assert.IsNotNull(memberBPre);
                Assert.IsNull(memberBPost);
            }
            
        }
        finally
        {
            File.Delete(planWithoutParam);
            File.Delete(planWithParam);
        }
    }
}
