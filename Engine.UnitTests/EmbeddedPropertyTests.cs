using System.Linq;
using NUnit.Framework;
using OpenTap.Engine.UnitTests;
namespace OpenTap.UnitTests
{
    [TestFixture]
    public class EmbeddedPropertyTests
    {
        public class EmbeddedTest2
        {
            // this should give EmbeddedTest all the virtual properties of DataInterfaceTestClass.
            [Display("Embedded Things")]
            [EmbedProperties(PrefixPropertyName = false)]
            public MemberDataProviderTests.DataInterfaceTestClass EmbeddedThings { get; set; } = new MemberDataProviderTests.DataInterfaceTestClass();
        }

        [Test]
        public void EmbeddedPropertiesReflectionAndAnnotation()
        {
            var obj = new EmbeddedTest2();
            obj.EmbeddedThings.SimpleNumber = 3145.2;
            var type = TypeData.GetTypeData(obj);
            var display = type.GetMembers().First().GetDisplayAttribute();
            Assert.IsTrue(display.Group[0] == "Embedded Things"); // test that the name gets transformed.
            var emba = type.GetMember(nameof(MemberDataProviderTests.DataInterfaceTestClass.SimpleNumber));
            Assert.AreEqual(obj.EmbeddedThings.SimpleNumber, (double)emba.GetValue(obj));
            var embb = type.GetMember(nameof(MemberDataProviderTests.DataInterfaceTestClass.FromAvailable));
            Assert.AreEqual(obj.EmbeddedThings.FromAvailable, (double)embb.GetValue(obj));

            var annotated = AnnotationCollection.Annotate(obj);
            annotated.Read();
            var same = annotated.Get<IMembersAnnotation>().Members.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member == emba);
            Assert.AreEqual("3145.2 Hz", same.Get<IStringValueAnnotation>().Value);
        }



        [Test]
        public void EmbeddedPropertiesSerialization()
        {
            var ts = new TapSerializer();
            var obj = new EmbeddedTest2();
            obj.EmbeddedThings.SimpleNumber = 500;
            var str = ts.SerializeToString(obj);
            obj = (EmbeddedTest2)ts.DeserializeFromString(str);
            Assert.AreEqual(500, obj.EmbeddedThings.SimpleNumber);
        }

        public class EmbA: ValidatingObject
        {
            public double X { get; set; }

            public EmbA()
            {
                Rules.Add(() => X >= 0, "X < 0", nameof(X));
            }
        }

        public class EmbB : ValidatingObject
        {
            [EmbedProperties(PrefixPropertyName = false)]
            public EmbA A { get; set; } = new EmbA();

            [EmbedProperties(Prefix = "A")]
            public EmbA A2 { get; set; } = new EmbA();
            
            public double Y { get; set; }

            public EmbB()
            {
                Rules.Add(()=> Y >= 0, "Y < 0", nameof(Y));
            }
        }

        public class EmbC
        {
            [EmbedProperties]
            public EmbB B { get; set; } = new EmbB();
            
        }
        
        [Test]
        public void NestedEmbeddedTest()
        {
            var c = new EmbC();
            c.B.A2.X = 5;
            c.B.A.X = 35;
            var embc_type = TypeData.GetTypeData(c);

            var members = embc_type.GetMembers();

            var mem = embc_type.GetMember("B.A.X");
            
            Assert.AreEqual(c.B.A2.X, (double)mem.GetValue(c));
            mem.SetValue(c, 20);
            Assert.AreEqual(c.B.A2.X, 20.0);

            var mem2 = embc_type.GetMember("B.X");
            Assert.AreEqual(c.B.A.X, (double)mem2.GetValue(c));

            var ts = new TapSerializer();
            var str = ts.SerializeToString(c);
            var c2 = (EmbC)ts.DeserializeFromString(str);
            Assert.AreNotEqual(c, c2);
            Assert.AreEqual(c.B.A.X, c2.B.A.X);

            Assert.IsTrue(true == string.IsNullOrWhiteSpace(c.B.A2.Error));
            c.B.Y = -5;
            
            var a = AnnotationCollection.Annotate(c);
            var mem_b_y = a.GetMember("B.Y");
            var err1 = mem_b_y.GetAll<IErrorAnnotation>().SelectMany(x => x.Errors).FirstOrDefault();
            Assert.IsTrue(string.IsNullOrWhiteSpace(err1) == false);
            c.B.Y = 5;
            c.B.A2.X = -5;
            var mem_b_a2_x = a.GetMember("A.X");
            var err2 = mem_b_a2_x.GetAll<IErrorAnnotation>().SelectMany(x => x.Errors).FirstOrDefault();
            Assert.IsTrue(string.IsNullOrWhiteSpace(err2) == false);
            
        }

        public class EmbD
        {
            [EmbedProperties]
            public EmbD B { get; set; }
            public int X { get; set; }
        }

        [Test]
        public void RecursiveEmbeddedTest()
        {
            var d = new EmbD();
            var t = TypeData.GetTypeData(d);
            var members = t.GetMembers(); // this will throw a StackOverflowException if the Embedding does not take care of the potential problem.
            Assert.AreEqual(2, members.Count());
        }

        public class OverrideVerdictMixin : ITestStepPostRunMixin
        {
            [Display("Override Verdict")]
            public Verdict OverrideVerdict { get; set; } = Verdict.Pass;
            
            public void OnPostRun(TestStepPostRunEventArgs step)
            {
                step.TestStep.Verdict = OverrideVerdict;
            }
        }
        
        public class RepeatMixin2 : ITestStepPostRunMixin, ITestStepPreRunMixin
        {
            [Display("Repeat Count")]
            public int Count { get; set; } = 1;

            int? it = null; 
            public void OnPostRun(TestStepPostRunEventArgs step)
            {
                if (it == null)
                    it = 1;
                if (it >= Count)
                    it = null;
                else
                {
                    step.TestStep.StepRun.SuggestedNextStep = step.TestStep.Id;
                    it += 1;
                }
            }
            
            public void OnPreRun(TestStepPreRunEventArgs step)
            {
                if (Count == 0) step.SkipStep = true;
            }
        }

        public class TestStepWithMixin : TestStep
        {
            [EmbedProperties]
            public OverrideVerdictMixin OverrideVerdict { get; set; } = new OverrideVerdictMixin();

            [EmbedProperties]
            public RepeatMixin2 Repeat { get; set; } = new RepeatMixin2();

            internal int Repeats = 0;
            public override void PrePlanRun()
            {
                base.PrePlanRun();
                Repeats = 0;
            }

            public override void Run()
            {
                Repeats++;
                UpgradeVerdict(Verdict.Fail);
            }
        }

        [Test]
        public void TestTestStepMixinsBasic()
        {
            var plan = new TestPlan();
            var step1 = new TestStepWithMixin();
            step1.Repeat.Count = 5;
            plan.ChildTestSteps.Add(step1);
            var run = plan.Execute();
            Assert.AreEqual(step1.OverrideVerdict.OverrideVerdict, run.Verdict);
            Assert.AreEqual(step1.Repeat.Count, step1.Repeats);
            
            // now set the repeat count to 0.
            step1.Repeat.Count = 0;
            var run2 = plan.Execute();
            Assert.AreEqual(Verdict.NotSet, run2.Verdict);
            Assert.AreEqual(step1.Repeat.Count, step1.Repeats);
        }

        public class ResourceMultiMixin : IResourcePreOpenMixin
        {
            public bool PreOpenCalled = false;
            public void OnPreOpen(ResourcePreOpenEventArgs eventArgs)
            {
                PreOpenCalled = true;
            }
        }

        public class ResourceMixinTest : Instrument
        {
            [EmbedProperties]
            public ResourceMultiMixin TestMixin { get; set; } = new ResourceMultiMixin();
        }

        [Test]
        public void TestResourceMixins()
        {
            var resource = new ResourceMixinTest();
            var step = new AnnotationTest.InstrumentStep();
            step.Instrument = resource;
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(step);
            plan.Execute();
            Assert.IsTrue(resource.TestMixin.PreOpenCalled);
        }
    }

}
