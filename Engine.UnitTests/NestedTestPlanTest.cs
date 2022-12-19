using System;
using System.IO;
using NUnit.Framework;
using OpenTap.Engine.UnitTests.TestTestSteps;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.Engine.UnitTests
{
    public class NestedTestPlanTest
    {
        [Test]
        public void NestedTestPlanTest1()
        {
            var innerPlanName = Guid.NewGuid() + ".test.TapPlan";
            try
            {
                { 
                    // construct the inner test plan.
                    var innerPlan = new TestPlan();
                    var innerVerdict = new VerdictStep { VerdictOutput = Verdict.Pass };
                    
                    // parameterize the verdict.
                    TypeData.GetTypeData(innerVerdict).GetMember(nameof(innerVerdict.VerdictOutput))
                        .Parameterize(innerPlan, innerVerdict, "verdict");
                    innerPlan.Steps.Add(innerVerdict);
                    innerPlan.Save(innerPlanName);
                }

                var outerPlan = new TestPlan();
                var nestingStep = new NestedTestPlan();
                outerPlan.Steps.Add(nestingStep);
                nestingStep.Filepath.Text = innerPlanName;
                nestingStep.LoadTestPlan();
                
                // run -> verdict should be Pass
                var run1 = outerPlan.Execute();
                Assert.AreEqual(Verdict.Pass, run1.Verdict);
                    
                // verify the functionality of forwarded parameters.
                TypeData.GetTypeData(nestingStep)
                    .GetMember("verdict")
                    .SetValue(nestingStep, Verdict.Inconclusive);
                
                // run -> verdict should be Inconclusive
                var run2 = outerPlan.Execute();
                Assert.AreEqual(Verdict.Inconclusive, run2.Verdict);

            }
            finally
            {
                File.Delete(innerPlanName);
            }
        }
    }
}