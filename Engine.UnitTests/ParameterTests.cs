using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;
namespace OpenTap.UnitTests;

[TestFixture]
public class ParameterTests
{
    [Test]
    public void TestMultiLevelParameters()
    {
        // copying multi-level parameters has been a problem because the parameter got removed
        // the moment it was no longer a part of the test plan, even if the parameter was inside the scopes.

        var seq3 = new SequenceStep();
        var seq = new SequenceStep();
        var seq2 = new SequenceStep();
        var delay = new DelayStep();
        seq.ChildTestSteps.Add(seq2);
        seq2.ChildTestSteps.Add(delay);
        seq3.ChildTestSteps.Add(seq);

        TypeData.GetTypeData(delay).GetMember(nameof(delay.DelaySecs)).Parameterize(seq, delay, "A");
        Assert.IsTrue(TypeData.GetTypeData(seq).GetMember("A") != null);

        seq3.ChildTestSteps.RemoveItems([seq]);

        Assert.IsTrue(TypeData.GetTypeData(seq).GetMember("A") != null);

    }
    
}
