using System.Linq;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;
using OpenTap.UnitTests;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class MixinTests
    {

        public class SomeClass
        {
            public double Test { get; set; }
        }

        public class EmbeddingClass
        {
            public double Test2 { get; set; }
        }
        
        [Test]
        public void EmbedRepeatMixinTest()
        {
            var step = new DelayStep();
            MixinFactory.LoadMixin(step, new NumberMixinBuilder());
            MixinFactory.LoadMixin(step, new RepeatMixinBuilder());
            var td = TypeData.GetTypeData(step);
            var mem = td.GetMember("RepeatMixin.Count");
            Assert.IsNotNull(mem);
        }

        [Test]
        public void AddingMixinsTest()
        {
            var test = new SomeClass();
            var builders = MixinFactory.GetMixinBuilders(TypeData.GetTypeData(test)).ToArray();
            var builderUi = new MixinBuilderUi(builders);
            var type = TypeData.GetTypeData(builderUi);
            var members = type.GetMembers();
            var a = AnnotationCollection.Annotate(builderUi);
            var textNameMember = a.GetMember("OpenTap.TextMixinBuilder.Name");
            var enabled1 = textNameMember.Get<IAccessAnnotation>().IsVisible;

            var nameMember = a.GetMember("OpenTap.NumberMixinBuilder.Name");
            var enabled = nameMember.Get<IAccessAnnotation>().IsVisible;
            var optionMember = a.GetMember("OpenTap.NumberMixinBuilder.Options");
            optionMember.SetValue("Unit");
            var unitMember = a.GetMember("OpenTap.NumberMixinBuilder.Unit");
            var unitEnabled = unitMember.Get<IEnabledAnnotation>().IsEnabled;

            Assert.IsTrue(unitEnabled);
            Assert.IsTrue(enabled);
            Assert.IsFalse(enabled1);
            
            
        }

        [Test]
        public void TestMixinSerialization()
        {
            var test = new SomeClass();
            var builder1 = new NumberMixinBuilder { Name = "A" };
            var builder2 = new NumberMixinBuilder { Name = "B" };
            var builder3 = new TextMixinBuilder { Name = "C" };
            
            var mem = builder1.ToDynamicMember(TypeData.GetTypeData(test));
            var mem2 = builder2.ToDynamicMember(TypeData.GetTypeData(test));
            var mem3 = builder3.ToDynamicMember(TypeData.GetTypeData(test));
            
            DynamicMember.AddDynamicMember(test, mem);
            DynamicMember.AddDynamicMember(test, mem2);
            DynamicMember.AddDynamicMember(test, mem3);
            
            mem.SetValue(test, 10.0);
            mem2.SetValue(test, 15.0);
            mem3.SetValue(test, "hello");
            var mem1_2 = TypeData.GetTypeData(test).GetMember("A");
            var mem2_2 = TypeData.GetTypeData(test).GetMember("B");
            Assert.IsNotNull(mem1_2);
            Assert.IsNotNull(mem2_2);

            var xml = new TapSerializer().SerializeToString(test);
            var test2 = (SomeClass)new TapSerializer(){ThrowOnErrors = true}.DeserializeFromString(xml);

            var numMember = TypeData.GetTypeData(test2).GetMember("A");
            Assert.IsNotNull(numMember);
            Assert.AreEqual(10.0, numMember.GetValue(test2));
            
            var numMember2 = TypeData.GetTypeData(test2).GetMember("B");
            Assert.IsNotNull(numMember2);
            Assert.AreEqual(15.0, numMember2.GetValue(test2));
            
            var member3 = TypeData.GetTypeData(test2).GetMember("C");
            Assert.IsNotNull(member3);
            Assert.AreEqual("hello", member3.GetValue(test2));
        }

        [Test]
        public void TestSerializeRepeatMixin()
        {
            var step = new SequenceStep();
            MixinFactory.LoadMixin(step, new RepeatMixinBuilder());
            
            TypeData.GetTypeData(step).GetMember("RepeatMixin.Count").SetValue(step, 10);
            var xml = new TapSerializer().SerializeToString(step);

            var step2 = (SequenceStep) new TapSerializer().DeserializeFromString(xml);
            var mem2 = TypeData.GetTypeData(step2).GetMember("RepeatMixin.Count");

            Assert.AreEqual(10, mem2.GetValue(step2));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        public void TestRepeatMixin(int repeatCount)
        {
            var logStep = new LogStep();
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(logStep);
            MixinFactory.LoadMixin(logStep, new RepeatMixinBuilder());
            var countMember = TypeData.GetTypeData(logStep).GetMember("RepeatMixin.Count");
            countMember.SetValue(logStep, repeatCount);

            var rl = new RecordAllResultListener();
            var run = plan.Execute(new[] {rl});
            Assert.AreEqual(Verdict.NotSet, run.Verdict);
            var allRuns = rl.Runs.Values;

            Assert.AreEqual(repeatCount, allRuns.OfType<TestStepRun>().Count());

        }
        
        [Test]
        public void TestRepeatMixinValidation()
        {
            var step = new SequenceStep();
            MixinFactory.LoadMixin(step, new RepeatMixinBuilder());
            TypeData.GetTypeData(step).GetMember("RepeatMixin.Count").SetValue(step, -10);
            var errors = step.Error;

            Assert.IsFalse(string.IsNullOrWhiteSpace(errors));
        }
    }
}