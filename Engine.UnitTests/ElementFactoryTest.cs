using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class ElementFactoryTest
    {
        public class ClassWithComplexList
        {
            public class ComplexElement
            {
                readonly ClassWithComplexList Parent;
                
                internal ComplexElement(ClassWithComplexList parent)
                {
                    this.Parent = parent;
                }
            }

            public class ComplexList : List<ComplexElement>
            {
                readonly ClassWithComplexList parent;
                public ComplexList(ClassWithComplexList lst)
                {
                    this.parent = lst;
                }
            }

            public class ComplexList2 : List<int>
            {
                readonly ClassWithComplexList parent;
                public ComplexList2(ClassWithComplexList lst)
                {
                    this.parent = lst;
                }
            }
            [ElementFactory(nameof(NewElement))]
            public List<ComplexElement> Items { get; set; } = new List<ComplexElement>();
            
            [ElementFactory(nameof(NewElement))]
            [Factory(nameof(NewComplexList))]
            public ComplexList Items2 { get; set; }
            
            [Factory(nameof(NewComplexList))]
            public ComplexList Items3 { get; set; }
            
            
            // Complex list, simple elements
            [Factory(nameof(NewComplexList2))]
            public ComplexList2 Items4 { get; set; }

            
            [Factory(nameof(NewComplexList))]
            [ElementFactory(nameof(NewElement))]
            public ComplexList Items5 { get; set; }
            
            internal ComplexElement NewElement()
            {
                return new ComplexElement(this);
            }

            internal ComplexList NewComplexList()
            {
                return new ComplexList(this);
            }
            
            internal ComplexList2 NewComplexList2()
            {
                return new ComplexList2(this);
            }
            
            public ClassWithComplexList()
            {
                Items2 = new ComplexList(this);
                Items5 = new ComplexList(this);
            }

        }

        [Test]
        public void SerializeWithElementFactory()
        {
        
            var test = new ClassWithComplexList();
            test.Items.Add(test.NewElement());
            test.Items2.Add(test.NewElement());
            test.Items5.Add(test.NewElement());
            test.Items4 = test.NewComplexList2();
            test.Items4.Add(10);

            var xml = new TapSerializer().SerializeToString(test);

            var test2 =(ClassWithComplexList) new TapSerializer(){IgnoreErrors = false}.DeserializeFromString(xml);
            Assert.AreEqual(1, test2.Items2.Count);
            Assert.IsNotNull(test2.Items2[0]);
            Assert.AreEqual(1, test2.Items.Count);
            Assert.IsNotNull(test2.Items[0]);
            Assert.IsNotNull(test2.Items5);
            Assert.AreEqual(1, test2.Items5.Count);

            Assert.AreEqual(10, test2.Items4[0]);
        }

        [Test]
        public void AnnotateWithElementFactory()
        {
            var test = new ClassWithComplexList();
            var a = AnnotationCollection.Annotate(test);
            var complexItems = a.GetMember(nameof(test.Items2));
            var collection = complexItems.Get<ICollectionAnnotation>();
            collection.AnnotatedElements = collection.AnnotatedElements.Append(collection.NewElement());
            int preCount = test.Items2.Count;
            a.Write();
            int postCount = test.Items2.Count;
            
            Assert.AreEqual(0, preCount);
            Assert.AreEqual(1, postCount);

        }

        [Test]
        public void TestDeserializePrameterizedElements()
        {
            var plan = new TestPlan();
            var sweep = new SweepParameterStep();
            var delay = new DelayStep();
            sweep.ChildTestSteps.Add(delay);
            plan.ChildTestSteps.Add(sweep);
            { /* parameterize DelaySecs */
                var delaySecs = AnnotationCollection.Annotate(delay).GetMember(nameof(delay.DelaySecs));
                var mem = delaySecs.Get<IMemberAnnotation>().Member;
                mem.Parameterize(sweep, delay, $"Parameters \\ {mem.GetDisplayAttribute().Name}");
            }

            { /* parameterize sweep values on test plan */
                var sweepValues = AnnotationCollection.Annotate(sweep).GetMember(nameof(sweep.SweepValues));

                var col = sweepValues.Get<ICollectionAnnotation>();

                void addDelay(double delay)
                {
                    var elem1 = col.NewElement();
                    var delayMem = elem1.GetMember("Parameters \\ Time Delay");
                    delayMem.Get<IObjectValueAnnotation>().Value = delay;
                    col.AnnotatedElements = col.AnnotatedElements.Append(elem1);
                }
                
                addDelay(1);
                addDelay(2);
                
                sweepValues.Write();
                sweepValues.Read();
                
                var mem = sweepValues.Get<IMemberAnnotation>().Member;
                mem.Parameterize(plan, sweep, $"Parameters \\ {mem.GetDisplayAttribute().Name}"); 
            }

            TestPlan plan2 = null;
            {
                var ser = new TapSerializer() { IgnoreErrors = false };
                var planXml = ser.SerializeToString(plan);
                Assert.IsTrue(!ser.Errors.Any());
                plan2 = ser.DeserializeFromString(planXml) as TestPlan;
                Assert.IsTrue(!ser.Errors.Any());
            }

            void AssertSweepsEqual(SweepRowCollection sweep1, SweepRowCollection sweep2)
            {
                Assert.IsTrue(sweep1.Count == sweep2.Count);

                for (int i = 0; i < sweep1.Count; i++)
                {
                    var d1 = sweep1[i];
                    var d2 = sweep2[i];
                    foreach (var kvp in d1.Values)
                    {
                        var v1 = kvp.Value;
                        var v2 = d2.Values[kvp.Key];
                        Assert.IsTrue(v1.Equals(v2));
                    }
                }
            }

            { /* verify parameters were correctly deserialized */
                var sweep1 = sweep.SweepValues;
                var sweep2 = (SweepRowCollection)plan2.ExternalParameters.Entries.First().Value; 
                AssertSweepsEqual(sweep1, sweep2);
            }

            { /* test wrapping the test plan in a test plan reference step */
                var temp = Path.GetTempFileName();
                try
                {
                    plan.Save(temp);
                    var parentPlan = new TestPlan();
                    var refStep = new TestPlanReference();
                    parentPlan.ChildTestSteps.Add(refStep);
                    refStep.Filepath = new MacroString() { Text = temp };
                    refStep.LoadTestPlan();
                    var ser2 = new TapSerializer() { IgnoreErrors = false };
                    var parentPlanXml = ser2.SerializeToString(parentPlan);
                    Assert.IsTrue(!ser2.Errors.Any());
                    var parentPlan2 = ser2.DeserializeFromString(parentPlanXml) as TestPlan;
                    Assert.IsTrue(!ser2.Errors.Any());

                    var refStep2 = parentPlan2.ChildTestSteps[0] as TestPlanReference;
                    refStep2.LoadTestPlan();

                    SweepRowCollection GetSweepRow(ITestStepParent p) => AnnotationCollection.Annotate(p).GetMember("Sweep Values").Get<IObjectValueAnnotation>().Value as SweepRowCollection;
                    
                    var sweep1 = GetSweepRow(refStep);
                    var sweep2 = GetSweepRow(refStep2);
                    
                    AssertSweepsEqual(sweep1, sweep2);
                }
                finally
                {
                    File.Delete(temp);
                }
            }
        } 
    }
}