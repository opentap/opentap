using System;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using NUnit.Framework;
using OpenTap.Engine.UnitTests;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests;

[TestFixture]
public class EmbeddedPropertyTests
{
    public class EmbeddedTest2
    {
        // this should give EmbeddedTest all the virtual properties of DataInterfaceTestClass.
        [Display("Embedded Things")]
        [EmbedProperties(PrefixPropertyName = false)]
        public MemberDataProviderTests.DataInterfaceTestClass EmbeddedThings { get; set; } =
            new MemberDataProviderTests.DataInterfaceTestClass();
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
        var same = annotated.Get<IMembersAnnotation>().Members
            .FirstOrDefault(x => x.Get<IMemberAnnotation>().Member == emba);
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

    public class EmbA : ValidatingObject
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

        [EmbedProperties(Prefix = "A")] public EmbA A2 { get; set; } = new EmbA();

        public double Y { get; set; }

        public EmbB()
        {
            Rules.Add(() => Y >= 0, "Y < 0", nameof(Y));
        }
    }

    public class EmbC
    {
        [EmbedProperties] public EmbB B { get; set; } = new EmbB();
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
        [EmbedProperties] public EmbD B { get; set; }
        public int X { get; set; }
    }

    [Test]
    public void RecursiveEmbeddedTest()
    {
        var d = new EmbD();
        var t = TypeData.GetTypeData(d);
        var members =
            t.GetMembers(); // this will throw a StackOverflowException if the Embedding does not take care of the potential problem.
        Assert.AreEqual(2, members.Count());
    }

    public class OverrideVerdictMixin : ITestStepPostRunMixin
    {
        [Display("Override Verdict")] public Verdict OverrideVerdict { get; set; } = Verdict.Pass;

        public void OnPostRun(TestStepPostRunEventArgs step)
        {
            step.TestStep.Verdict = OverrideVerdict;
        }
    }

    public class TestRepeatMixin : ITestStepPostRunMixin, ITestStepPreRunMixin
    {
        [Display("Repeat Count")] public int Count { get; set; } = 1;

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
        [EmbedProperties] public OverrideVerdictMixin OverrideVerdict { get; set; } = new OverrideVerdictMixin();

        [EmbedProperties] public TestRepeatMixin Repeat { get; set; } = new TestRepeatMixin();

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
        [EmbedProperties] public ResourceMultiMixin TestMixin { get; set; } = new ResourceMultiMixin();
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

    public class EmbeddingClass
    {
        protected bool Equals(EmbeddingClass other)
        {
            return IntMember == other.IntMember && StringMember == other.StringMember;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((EmbeddingClass)obj);
        }

        public override int GetHashCode() =>
            IntMember.GetHashCode() * 12389321 + StringMember.GetHashCode() * 7310632;

        public int IntMember { get; set; }
        public string StringMember { get; set; }
    }

    public class EmbeddedTest3
    {
        // This is the member I want to serialize, but it will never be shown to the user.
        [AnnotationIgnore] public EmbeddingClass[] Objects { get; set; }

        //the index of object can be selected
        public int SelectedObjectIndex { get; set; }

        [EmbedProperties] [XmlIgnore] public EmbeddingClass SelectedObject => Objects[SelectedObjectIndex];
    }

    [Test]
    public void TestXmlIgnoreEmbedding()
    {
        var emb = new EmbeddedTest3()
        {
            SelectedObjectIndex = 1,
            Objects =
            [
                new() { IntMember = 0, StringMember = "0" }, new() { IntMember = 1, StringMember = "1" },
                new() { IntMember = 2, StringMember = "2" }
            ],
        };
        var ser = new TapSerializer();
        var str = ser.SerializeToString(emb);
        var emb2 = ser.DeserializeFromString(str) as EmbeddedTest3;


        Assert.That(str, Does.Not.Contain("SelectedObject.IntMember"));
        Assert.That(emb2, Is.Not.Null);

        Assert.That(AnnotationCollection.Annotate(emb).GetMember("Objects"), Is.Null);
        Assert.That(AnnotationCollection.Annotate(emb2).GetMember("Objects"), Is.Null);

        Assert.That(emb.SelectedObjectIndex, Is.EqualTo(emb2.SelectedObjectIndex));
        Assert.That(emb.SelectedObject, Is.EqualTo(emb2.SelectedObject));
        CollectionAssert.AreEqual(emb.Objects, emb2.Objects);

        verifyEmbedded(emb);
        verifyEmbedded(emb2);

        static void verifyEmbedded(object o)
        {
            var embedded = TypeData.GetTypeData(o).GetMembers().OfType<EmbeddedMemberData>().ToArray();
            Assert.That(embedded.Length, Is.EqualTo(2));

            foreach (var emb in embedded)
            {
                Assert.That(emb.HasAttribute<XmlIgnoreAttribute>(), Is.True);
            }
        }
    }

    public class EmbeddedClassOnTestStep : ValidatingObject
    {
        public double GreaterThan0 { get; set; } = 1.0;
        public EmbeddedClassOnTestStep()
        {
            Rules.Add(() => GreaterThan0 > 0, "Error", nameof(GreaterThan0));
        }

        public class EmbeddingClass2
        {
            public string GetMember { get; }
            [Browsable(true)] public string BrowsableGetMember { get; }
            public string GetSetMember { get; set; }
            [Browsable(false)] public string UnbrowsableGetSetMember { get; set; }
        }

        public class EmbeddedTest4
        {
            [Browsable(true)] 
            [XmlIgnore]
            [EmbedProperties]
            public EmbeddingClass2 ExplicitlyBrowsable { get; } = new();

            [Browsable(false)] 
            [EmbedProperties]
            public EmbeddingClass2 ExplicitlyUnbrowsable { get; } = new();

            [EmbedProperties]
            [XmlIgnore]
            public EmbeddingClass2 XmlIgnored { get; } = new();
        }

        [Test]
        public void TestBrowsableEmbedding()
        {
            var obj = new EmbeddedTest4();
            var a = AnnotationCollection.Annotate(obj);

            /* this is the rough logic used by ui implementations to determine if a property is visible */
            static bool visible(AnnotationCollection mem)
            {
                var m = mem.Get<IMemberAnnotation>().Member;
                var browsable = m.GetAttribute<BrowsableAttribute>();

                // Browsable overrides everything
                if (browsable != null) return browsable.Browsable;

                var xmlIgnore = m.GetAttribute<XmlIgnoreAttribute>();
                if (xmlIgnore != null)
                    return false;
                return false;
            }

            /* verify that Browsable(true) is correctly inherited by all embedded properties */
            Assert.That(visible(a.GetMember(nameof(obj.ExplicitlyBrowsable) + "." + nameof(obj.ExplicitlyBrowsable.GetMember))),               Is.True); /* parent browsable */
            Assert.That(visible(a.GetMember(nameof(obj.ExplicitlyBrowsable) + "." + nameof(obj.ExplicitlyBrowsable.BrowsableGetMember))),      Is.True); /* overrides browsable(true) */
            Assert.That(visible(a.GetMember(nameof(obj.ExplicitlyBrowsable) + "." + nameof(obj.ExplicitlyBrowsable.GetSetMember))),            Is.True); /* parent browsable */
            Assert.That(visible(a.GetMember(nameof(obj.ExplicitlyBrowsable) + "." + nameof(obj.ExplicitlyBrowsable.UnbrowsableGetSetMember))), Is.False); /* browsable(false) */

            /* verify that an explicit Browsable(false) makes all embedded properties unconditonally unbrowsable */
            Assert.That(visible(a.GetMember(nameof(obj.ExplicitlyUnbrowsable) + "." + nameof(obj.ExplicitlyUnbrowsable.GetMember))),               Is.False); /* parent browsable(false) */
            Assert.That(visible(a.GetMember(nameof(obj.ExplicitlyUnbrowsable) + "." + nameof(obj.ExplicitlyUnbrowsable.BrowsableGetMember))),      Is.True); /* browsable(true) */
            Assert.That(visible(a.GetMember(nameof(obj.ExplicitlyUnbrowsable) + "." + nameof(obj.ExplicitlyUnbrowsable.GetSetMember))),            Is.False); /* parent browsable(false) */
            Assert.That(visible(a.GetMember(nameof(obj.ExplicitlyUnbrowsable) + "." + nameof(obj.ExplicitlyUnbrowsable.UnbrowsableGetSetMember))), Is.False); /* browsable(false) */

            /* verify that default browsability makes embedded properties use default browsable behavior */
            Assert.That(visible(a.GetMember(nameof(obj.XmlIgnored) + "." + nameof(obj.XmlIgnored.GetMember))),               Is.False); /* XmlIgnore makes this unbrowsable */
            Assert.That(visible(a.GetMember(nameof(obj.XmlIgnored) + "." + nameof(obj.XmlIgnored.BrowsableGetMember))),      Is.True); /* browsable(true) overrides XmlIgnore */
            Assert.That(visible(a.GetMember(nameof(obj.XmlIgnored) + "." + nameof(obj.XmlIgnored.GetSetMember))),            Is.False); /* XmlIgnore makes this unbrowsable */
            Assert.That(visible(a.GetMember(nameof(obj.XmlIgnored) + "." + nameof(obj.XmlIgnored.UnbrowsableGetSetMember))), Is.False); /* browsable(false) */

        }
    }
    
    public class StepWithEmbedded : TestStep
    {
        [EmbedProperties]
        public EmbeddedClassOnTestStep Emb { get; } = new EmbeddedClassOnTestStep();
        public override void Run()
        {
            
        }
    }
    
    [Test]
    public void EmbedParameterizeValidationErrorTest()
    {

        var plan = new TestPlan();
        var step = new StepWithEmbedded();
        var seq = new SequenceStep();
        seq.ChildTestSteps.Add(step);
        plan.ChildTestSteps.Add(seq);

        var memberParameter = TypeData.GetTypeData(step).GetMember("Emb.GreaterThan0")
            .Parameterize(seq, step, "GreaterThan0");

        memberParameter.Parameterize(plan, seq, "GreaterThan0Plan");
        
        Assert.IsTrue(string.IsNullOrWhiteSpace(step.Error));
        step.Emb.GreaterThan0 = 0;
        //Assert.IsFalse(string.IsNullOrWhiteSpace(step.Error));

        {
            var va = AnnotationCollection.Annotate(step).GetMember("Emb.GreaterThan0")
                .Get<ValidationErrorAnnotation>();
            Assert.IsTrue(va.Errors.Count() > 0);
        }
        
        {
            var va = AnnotationCollection.Annotate(seq).GetMember("GreaterThan0")
                .Get<ValidationErrorAnnotation>();
            Assert.IsTrue(va.Errors.Count() > 0);
        }
        {
            var va = AnnotationCollection.Annotate(plan).GetMember("GreaterThan0Plan")
                .Get<ValidationErrorAnnotation>();
            Assert.IsTrue(va.Errors.Count() > 0);
        }

        
    }
}