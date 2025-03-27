using System.Linq;
using NUnit.Framework;

namespace OpenTap.UnitTests;

public class DutInstrumentInheritanceTest
{
    public interface IDutInstrument : IDut, IInstrument
    {
    }

    public abstract class ItemResource : Resource
    {
    }

    public class ItemInstrument : ItemResource, IInstrument
    {
        public override string ToString()
        {
            return "Instrument Item";
        }
    }

    public class ItemDut : ItemResource, IDut
    {
        public override string ToString()
        {
            return "Dut Item";
        }
    }

    public class ItemTestStep : TestStep
    {
        // Can take either the dut or instrument variant
        public ItemResource Resource { get; set; }
        public override void Run()
        {
        }
    }

    [Test]
    public void TestStepResourceAssignability()
    {
        using var session = Session.Create(SessionOptions.OverlayComponentSettings);
        var ins = new ItemInstrument();
        var dut = new ItemDut();
        InstrumentSettings.Current.Add(ins);
        DutSettings.Current.Add(dut);
            
        var plan = new TestPlan(); 
        var step = new ItemTestStep();
        plan.ChildTestSteps.Add(step);

        var a = AnnotationCollection.Annotate(step);
        var mem = a.GetMember(nameof(step.Resource));
        { // Verify that both the dut and the instrument variant can be assigned to the step resource
            var avail = mem.Get<IAvailableValuesAnnotation>().AvailableValues.Cast<object>().ToArray();

            Assert.That(avail.Length, Is.EqualTo(2));
            Assert.That(avail, Contains.Item(dut));
            Assert.That(avail, Contains.Item(ins));
        }

        { // Verify that the resource can be reassigned using StringValueAnnotations
            var sv = mem.Get<IStringValueAnnotation>();
            { // resource should be ins
                step.Resource = ins;
                a.Read();
                Assert.That(step.Resource, Is.EqualTo(ins));
                Assert.That(sv.Value, Is.EqualTo(ins.ToString()));
            }
            { // resource should be dut
                sv.Value = dut.ToString();
                a.Write();
                a.Read();
                Assert.That(step.Resource, Is.EqualTo(dut));
                Assert.That(sv.Value, Is.EqualTo(dut.ToString()));
            }
            { // and back to ins
                sv.Value = ins.ToString();
                a.Write();
                a.Read();
                Assert.That(step.Resource, Is.EqualTo(ins));
                Assert.That(sv.Value, Is.EqualTo(ins.ToString()));
            }
        }
    } 
}