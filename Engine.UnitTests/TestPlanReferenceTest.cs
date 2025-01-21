using System.IO;
using System.Linq;
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

    /// <summary>
    /// tests various aspects of converting a test plan reference to a sequence.
    /// </summary>
    [Test]
    public void TestConvertToSequence()
    {
        string targetFile = "TestConvertToSequence.unittest.TapPlan";
        {
            File.Delete(targetFile);
            var referencedPlan = new TestPlan();
            var step = new DelayStep();
            referencedPlan.ChildTestSteps.Add(step);
            TypeData.GetTypeData(step).GetMember(nameof(step.DelaySecs)).Parameterize(referencedPlan, step, "A");

            var b1 = new TestNumberMixinBuilder()
            {
                Name = "Test",
                IsOutput = true
            };
            MixinFactory.LoadMixin(referencedPlan, b1);
            var member = TypeData.GetTypeData(referencedPlan).GetMember("Number.Test");

            var mv = member.GetValue(referencedPlan);
            Assert.IsNotNull(mv);
            member.SetValue(referencedPlan, 10);

            referencedPlan.Save(targetFile);
        }
        {
            var mixinTest = new MixinTestBuilder()
            {
                TestMember = "B"
            };

            var plan = new TestPlan();
            var testPlanReference = new TestPlanReference();
            MixinFactory.LoadMixin(testPlanReference, mixinTest);
            testPlanReference.Filepath.Text = targetFile;
            plan.ChildTestSteps.Add(testPlanReference);
            testPlanReference.LoadTestPlan();

            {
                var member = TypeData.GetTypeData(testPlanReference).GetMember("Number.Test");
                Assert.IsNotNull(member);
                var v = member.GetValue(testPlanReference);
                Assert.AreEqual(10.0, v);
            }

            // convert to sequence without showing a popup.
            using (Session.Create())
            {
                UserInput.SetInterface(null);
                testPlanReference.ConvertToSequence();
            }

            // loop twice to also try after serializing.
            for (int i = 0; i < 2; i++)
            {

                Assert.IsTrue(plan.ChildTestSteps[0] is SequenceStep);
                var seq = plan.ChildTestSteps[0] as SequenceStep;
                var members = TypeData.GetTypeData(seq).GetMembers();

                var member = members.FirstOrDefault(mem => mem.Name == "Number.Test");
                Assert.IsNotNull(member);
                var v = member.GetValue(seq);
                Assert.IsNotNull(v);
                var embeddedMembers = members.OfType<IEmbeddedMemberData>().ToArray();
                Assert.IsTrue(embeddedMembers.Length > 0);
                var delaymember = TypeData.GetTypeData(seq).GetMember("A");
                delaymember.SetValue(seq, 0.0);

                var run = plan.Execute();
                Assert.IsTrue(run.Verdict == Verdict.NotSet);

                // for the next iteration, lets try saving and loading it.

                plan.Save(targetFile);
                plan = TestPlan.Load(targetFile);
            }
        }
    }
}
