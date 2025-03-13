using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenTap.EngineUnitTestUtils;
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
        public void TestDeserializeParameterizedElements([Values(0, 1, 2, 3, 10)] int numRows)
        {
            using var s = Session.Create(SessionOptions.OverlayComponentSettings);
            var trace = new TestTraceListener();
            Log.AddListener(trace);

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

                for (int i = 1; i <= numRows; i++)
                {
                    addDelay(i);
                }

                sweepValues.Write();
                sweepValues.Read();
                
                var mem = sweepValues.Get<IMemberAnnotation>().Member;
                mem.Parameterize(plan, sweep, $"Parameters \\ {mem.GetDisplayAttribute().Name}"); 
            }

            TestPlan plan2 = null;
            {
                var ser = new TapSerializer() { IgnoreErrors = false };
                var planXml = ser.SerializeToString(plan);
                Assert.That(ser.Errors, Is.Empty);
                plan2 = ser.DeserializeFromString(planXml) as TestPlan;
                Assert.That(ser.Errors, Is.Empty);
            }

            void AssertSweepsEqual(SweepRowCollection sweep1, SweepRowCollection sweep2)
            {
                Assert.That(sweep1.Count, Is.EqualTo(numRows));
                Assert.That(sweep2.Count, Is.EqualTo(numRows));

                for (int i = 0; i < sweep1.Count; i++)
                {
                    var d1 = sweep1[i];
                    var d2 = sweep2[i];
                    foreach (var kvp in d1.Values)
                    {
                        var v1 = kvp.Value;
                        var v2 = d2.Values[kvp.Key];
                        Assert.That(v1, Is.EqualTo(v2));
                    }
                }
            }

            { /* verify parameters were correctly deserialized */
                var sweep1 = (SweepRowCollection)plan.ExternalParameters.Entries[0].Value; 
                var sweep2 = (SweepRowCollection)plan2.ExternalParameters.Entries[0].Value; 
                AssertSweepsEqual(sweep1, sweep2);
            } 

            { 
                var temp = Path.GetTempFileName();
                TestPlanReference refStep;
                try
                {
                    { /* test wrapping the test plan in a test plan reference step */
                        plan.Save(temp);
                        var parentPlan = new TestPlan();
                        refStep = new TestPlanReference();
                        parentPlan.ChildTestSteps.Add(refStep);
                        refStep.Filepath = new MacroString() { Text = temp };
                        refStep.LoadTestPlan();

                        var (parentPlan2, refStep2) = CloneRef(refStep);
                        refStep2.LoadTestPlan();

                        {
                            var sweep1 = GetSweepRow(refStep);
                            var sweep2 = GetSweepRow(refStep2);
                            AssertSweepsEqual(sweep1, sweep2);
                        }
                        refStep2.LoadTestPlan();
                        {
                            var sweep1 = GetSweepRow(refStep);
                            var sweep2 = GetSweepRow(refStep2);
                            AssertSweepsEqual(sweep1, sweep2);
                        }
                    }
                    { /* try adding new elements to the parameterized sweep and verify they are correctly serialized */
                        var a = AnnotationCollection.Annotate(refStep);
                        var sweepValues = a.GetMember("Sweep Values"); 
                        var col = sweepValues.Get<ICollectionAnnotation>();
                        var elem1 = col.NewElement();
                        var delayMem = elem1.GetMember("Parameters \\ Time Delay");
                        delayMem.Get<IObjectValueAnnotation>().Value = 123;
                        col.AnnotatedElements = col.AnnotatedElements.Append(elem1);
                        a.Write();

                        var (_, refStep2) = CloneRef(refStep);
                        numRows += 1;

                        {
                            var sweep1 = GetSweepRow(refStep);
                            var sweep2 = GetSweepRow(refStep2);
                            AssertSweepsEqual(sweep1, sweep2);
                        }
                        /* ensure the sweep changes are not lost after reloading */
                        {
                            refStep2.LoadTestPlan();
                            var sweep1 = GetSweepRow(refStep);
                            var sweep2 = GetSweepRow(refStep2);
                            AssertSweepsEqual(sweep1, sweep2);
                        }
                    }
                }
                finally
                {
                    File.Delete(temp);
                }

            }
            { /* verify that there are no warnings or errors logged */
                trace.Flush();
                trace.WarningMessage.RemoveAll(warn =>
                    warn.StartsWith("Duplicate Test packages detected.", StringComparison.OrdinalIgnoreCase));
                Assert.That(trace.WarningMessage, Is.Empty);
                Assert.That(trace.ErrorMessage, Is.Empty);
            }
            static (TestPlan, TestPlanReference) CloneRef(TestPlanReference refStep)
            {
                var parentPlan = new TestPlan();
                parentPlan.ChildTestSteps.Add(refStep);
                var ser = new TapSerializer() { IgnoreErrors = false };
                var xml = ser.SerializeToString(parentPlan);
                Assert.That(ser.Errors, Is.Empty);
                var plan2 = ser.DeserializeFromString(xml) as TestPlan;
                Assert.That(ser.Errors, Is.Empty);
                return (plan2, plan2.ChildTestSteps[0] as TestPlanReference);
            }
            static SweepRowCollection GetSweepRow(ITestStepParent p) => AnnotationCollection.Annotate(p).GetMember("Sweep Values").Get<IObjectValueAnnotation>().Value as SweepRowCollection;
        } 
    }
}
