using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

    [Test]
    public void TestMemberDataCacheConsistency()
    {
        const string correct = "Correct";
        const string garbage = "Garbage";
        var step = new DelayStep
        {
            Name = correct
        };
        
        
        // This is an extremely unlikely sequence of reads and writes,
        // but this is esentially what can sometimes happen when writing
        // different merged parameters which affect different subset of the same set of steps.
        // See the `TestOverlappingMergedParameterWriting` for a more likely scenario triggering this.
        
        var a = AnnotationCollection.Annotate(step);
        var mem = a.GetMember(nameof(step.Name));
        var ov = mem.Get<IObjectValueAnnotation>(); 
        
        // 1. Verify the annotation accurately reflects the current value
        Assert.That(ov.Value, Is.EqualTo(correct));
        // 2. Assign a value, but don't write it
        ov.Value = garbage;
        
        // 3. Read the annotation back
        a.Read();
        // 4. Write the current annotation
        a.Write();
        // 5. Verify the garbage value was not written.
        Assert.That(step.Name, Is.EqualTo(correct));
    }

    // Exhaustive list of assignment order to assign the parameterized members
    [TestCase(0, 1, 2)]
    [TestCase(0, 2, 1)]
    [TestCase(1, 0, 2)]
    [TestCase(1, 2, 0)]
    [TestCase(2, 1, 0)]
    [TestCase(2, 0, 1)]
    public void TestOverlappingMergedParameterWriting(int x, int y, int z)
    {
        int[] assignmentOrder = [x, y, z];
        var plan = new TestPlan();
        var nameParameterName = "shared_name";
        var delay1ParameterName = "delay_name1";
        var delay2ParameterName = "delay_name2";
        plan.ChildTestSteps.AddRange([ new DelayStep(), new DelayStep(), new DelayStep(), new DelayStep(), new DelayStep() ]);

        // Create a merged parameters spanning all 5 steps
        foreach (var p in plan.ChildTestSteps)
        {
            var mem = AnnotationCollection.Annotate(p).GetMember(nameof(p.Name)).Get<IMemberAnnotation>().Member;
            mem.Parameterize(plan, p, nameParameterName);
        }

        // Create a merged parameter on the first three steps
        for (int i = 0; i < 3; i++)
        {
            var step = plan.ChildTestSteps[i];
            var mem = AnnotationCollection.Annotate(step).GetMember(nameof(DelayStep.DelaySecs))
                .Get<IMemberAnnotation>().Member;
            mem.Parameterize(plan, step, delay1ParameterName);
        }

        // Create a merged parameter on the last two steps
        for (int i = 3; i < 5; i++)
        {
            var step = plan.ChildTestSteps[i];
            var mem = AnnotationCollection.Annotate(step).GetMember(nameof(DelayStep.DelaySecs))
                .Get<IMemberAnnotation>().Member;
            mem.Parameterize(plan, step, delay2ParameterName);
        }

        var a = AnnotationCollection.Annotate(plan);
        var nameParameter = a.GetMember(nameParameterName);
        var delayParameter1 = a.GetMember(delay1ParameterName);
        var delayParameter2 = a.GetMember(delay2ParameterName);

        var name = "Step Name";
        double delaySecs = 1;
        double delaySecs2 = 2;

        // parameter assignments. They will be assigned in the order defined by the input parameters, xyz
        (AnnotationCollection a, string val)[] pairs =
        [
            (nameParameter, name),
            (delayParameter1, delaySecs.ToString(CultureInfo.InvariantCulture)),
            (delayParameter2, delaySecs2.ToString(CultureInfo.InvariantCulture)),
        ];

        // Ensure the assignment works reliably by looping a few times
        // During debugging, I saw scenarios where the assignment worked every 2nd time because
        // a cache was invalidated after a write which was conditional on the cache being invalidated.
        for (int i = 0; i < 10; i++)
        {
            // 1. Assign the parameter values
            for (int j = 0; j < assignmentOrder.Length; j++)
            {
                var pair = pairs[assignmentOrder[j]];
                pair.a.Read();
                var sv = pair.a.Get<IStringValueAnnotation>();
                sv.Value = pair.val;
                pair.a.Write();
            }

            // 2. Verify the correct values were propagated to all steps
            {
                foreach (var step in plan.ChildTestSteps)
                {
                    Assert.That(step.Name, Is.EqualTo(name));
                }

                for (int k = 0; k < 3; k++)
                {
                    Assert.That((plan.ChildTestSteps[k] as DelayStep).DelaySecs, Is.EqualTo(delaySecs));
                }

                for (int k = 3; k < 5; k++)
                {
                    Assert.That((plan.ChildTestSteps[k] as DelayStep).DelaySecs, Is.EqualTo(delaySecs2));
                }
            }
            // reset the parameter values for the next loop iteration
            {
                foreach (var step in plan.ChildTestSteps)
                {
                    step.Name = "doesn't matter";
                    (step as DelayStep).DelaySecs = 123;
                }
            }
        }
    }
    
    public class StepEmbeddingComplexLists : TestStep
    {
        public List<ComplexType> ComplexList { get; set; } = [];
        [EmbedProperties] public EmbeddedClass Emb { get; set; } = new();
        public override void Run() { }
    }

    public class EmbeddedClass : ValidatingObject
    {
        public List<ComplexType> ComplexList { get; set; } = [];
    }

    public class ComplexType
    {
        public int X { get; set; }
        public string Y { get; set; }
    }

    [Test]
    public void TestMergingComplexParameters()
    {
        var plan = new TestPlan();
        var seq = new SequenceStep();

        plan.ChildTestSteps.Add(seq);
        StepEmbeddingComplexLists[] steps = [new(), new(), new()];
        seq.ChildTestSteps.AddRange(steps);

        var a = AnnotationCollection.Annotate(steps);
        { // 1. merge ComplexList on the parent step
            var param = a.GetMember("ComplexList");
            var ico = param.GetIcon(IconNames.ParameterizeOnParent);
            ico.Get<IMethodAnnotation>().Invoke();
        }
        
        { // 2. merge Emb.ComplexList on test plan
            var param = a.GetMember("Emb.ComplexList");
            var ico = param.GetIcon(IconNames.ParameterizeOnTestPlan);
            ico.Get<IMethodAnnotation>().Invoke();
        }

        static void AddComplexElement(AnnotationCollection mem, int x, string y)
        {
            var proxy = mem.Get<ICollectionAnnotation>();
            var item = proxy.NewElement();
            var ov = item.Get<IObjectValueAnnotation>();
            var v = (ComplexType)ov.Value;
            v.X = x;
            v.Y = y;
            proxy.AnnotatedElements = proxy.AnnotatedElements.Append(item);
        }

        { // 3. populate the parameter on the parent step
            var aSeq = AnnotationCollection.Annotate(seq);
            var mem = aSeq.GetMember("Parameters \\ ComplexList");

            AddComplexElement(mem, 1, "Param 1");
            AddComplexElement(mem, 2, "Param 2");
            AddComplexElement(mem, 3, "Param 3");
            aSeq.Write();

            // Verify parameters are merged correctly
            foreach (var step in steps)
            {
                Assert.That(step.ComplexList.Count, Is.EqualTo(3));
                Assert.That(step.ComplexList[0].Y, Is.EqualTo("Param 1"));
                Assert.That(step.ComplexList[1].Y, Is.EqualTo("Param 2"));
                Assert.That(step.ComplexList[2].Y, Is.EqualTo("Param 3"));
            }
        }
        
        { // 4. populate the parameter on the test plan
            var aPlan = AnnotationCollection.Annotate(plan);
            var mem = aPlan.GetMember("Emb.ComplexList");

            AddComplexElement(mem, 4, "Plan 4");
            AddComplexElement(mem, 5, "Plan 5");
            AddComplexElement(mem, 6, "Plan 6");
            aPlan.Write();

            // Verify parameters are merged correctly
            foreach (var step in steps)
            {
                Assert.That(step.Emb.ComplexList.Count, Is.EqualTo(3));
                Assert.That(step.Emb.ComplexList[0].Y, Is.EqualTo("Plan 4"));
                Assert.That(step.Emb.ComplexList[1].Y, Is.EqualTo("Plan 5"));
                Assert.That(step.Emb.ComplexList[2].Y, Is.EqualTo("Plan 6"));
            }
        }

        { // 5. Verify that everything works correctly after serialization
            var ser = new TapSerializer();
            var str = ser.SerializeToString(plan);
            plan = (TestPlan)ser.DeserializeFromString(str);
            seq = (SequenceStep)plan.ChildTestSteps[0];
            Assert.That(ser.Errors, Is.Empty);
            steps = plan.ChildTestSteps[0].ChildTestSteps.Cast<StepEmbeddingComplexLists>().ToArray();
            Assert.That(steps.Length, Is.EqualTo(3));

            foreach (var step in steps)
            {
                Assert.That(step.ComplexList.Count, Is.EqualTo(3));
                Assert.That(step.ComplexList[0].Y, Is.EqualTo("Param 1"));
                Assert.That(step.ComplexList[1].Y, Is.EqualTo("Param 2"));
                Assert.That(step.ComplexList[2].Y, Is.EqualTo("Param 3"));

                Assert.That(step.Emb.ComplexList.Count, Is.EqualTo(3));
                Assert.That(step.Emb.ComplexList[0].Y, Is.EqualTo("Plan 4"));
                Assert.That(step.Emb.ComplexList[1].Y, Is.EqualTo("Plan 5"));
                Assert.That(step.Emb.ComplexList[2].Y, Is.EqualTo("Plan 6"));
            }

            { // Remove elements from the parameterized list and verify the changes are propagated correctly
                var aSeq = AnnotationCollection.Annotate(seq);
                var mem = aSeq.GetMember("Parameters \\ ComplexList");
                var col = mem.Get<ICollectionAnnotation>();
                col.AnnotatedElements = col.AnnotatedElements.Skip(1);
                aSeq.Write();

                foreach (var step in steps)
                {
                    Assert.That(step.ComplexList.Count, Is.EqualTo(2));
                    Assert.That(step.ComplexList[0].Y, Is.EqualTo("Param 2"));
                    Assert.That(step.ComplexList[1].Y, Is.EqualTo("Param 3"));
                }

                { // Verify that modifications to properties of list elements are propagated
                    aSeq.Read();
                    col = mem.Get<ICollectionAnnotation>();
                    var col1 = (ComplexType)col.AnnotatedElements.First().Get<IObjectValueAnnotation>().Value;
                    col1.Y = "Modified";
                    aSeq.Write();

                    foreach (var step in steps)
                    {
                        Assert.That(step.ComplexList.Count, Is.EqualTo(2));
                        Assert.That(step.ComplexList[0].Y, Is.EqualTo("Modified"));
                        Assert.That(step.ComplexList[1].Y, Is.EqualTo("Param 3"));
                    }
                }
            }
            
            { // Repeat the same tests for parameters merged on the test plan
                var aPlan = AnnotationCollection.Annotate(plan);
                var mem = aPlan.GetMember("Emb.ComplexList");
                var col = mem.Get<ICollectionAnnotation>();
                col.AnnotatedElements = col.AnnotatedElements.Take(2);
                aPlan.Write();

                foreach (var step in steps)
                {
                    Assert.That(step.Emb.ComplexList.Count, Is.EqualTo(2));
                    Assert.That(step.Emb.ComplexList[0].Y, Is.EqualTo("Plan 4"));
                    Assert.That(step.Emb.ComplexList[1].Y, Is.EqualTo("Plan 5"));
                }

                { // Verify that modifications to properties of list elements are propagated
                    aPlan.Read();
                    col = mem.Get<ICollectionAnnotation>();
                    var col2 = (ComplexType)col.AnnotatedElements.Last().Get<IObjectValueAnnotation>().Value;
                    col2.Y = "Modified 2";
                    aPlan.Write();

                    foreach (var step in steps)
                    {
                        Assert.That(step.Emb.ComplexList.Count, Is.EqualTo(2));
                        Assert.That(step.Emb.ComplexList[0].Y, Is.EqualTo("Plan 4"));
                        Assert.That(step.Emb.ComplexList[1].Y, Is.EqualTo("Modified 2"));
                    }
                }
            }
        }
    }

    [Test]
    public void TestRemoveParameter()
    {
        var seq = new SequenceStep();
        var delay = new DelayStep();
        seq.ChildTestSteps.Add(delay);
        var plan = new TestPlan();
        plan.ChildTestSteps.Add(seq);

        var td = TypeData.GetTypeData(delay);
        var mem = td.GetMember(nameof(delay.DelaySecs));
        var aMember = mem.Parameterize(seq, delay, "A 1");
            
        Assert.IsNotNull(TypeData.GetTypeData(seq).GetMember("A 1"));
        aMember.Remove();
        Assert.IsNull(TypeData.GetTypeData(seq).GetMember("A 1")); 
    }
    
}