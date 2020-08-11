using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    public class AnnotationTest
    {
        [Test]
        public void TestPlanReferenceNameTest()
        {
            var step = new DelayStep();
            var testPlanReference = new TestPlanReference();
            var repeatStep = new RepeatStep();
            repeatStep.ChildTestSteps.Add(testPlanReference);
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(step);
            plan.ChildTestSteps.Add(repeatStep);

            var mem = AnnotationCollection.Annotate(repeatStep).Get<IMembersAnnotation>().Members
                .FirstOrDefault(x => x.Get<IMemberAnnotation>().Member.Name == nameof(RepeatStep.TargetStep));
            var avail = mem.Get<IAvailableValuesAnnotationProxy>().AvailableValues;
            var availStrings = avail.Select(x => x.Get<IStringReadOnlyValueAnnotation>().Value).ToArray();    
            Assert.IsTrue(availStrings.Contains(step.GetFormattedName()));
            Assert.IsTrue(availStrings.Contains(testPlanReference.GetFormattedName()));
        }

        
        
        [Test]
        public void TestPlanReferenceAnnotationTest()
        {
            
            var planname = Guid.NewGuid().ToString() + ".TestPlan";
            {
                var innerplan = new TestPlan();
                var step = new DelayStep();
                innerplan.Steps.Add(step);
                innerplan.ExternalParameters.Add(step,
                    TypeData.GetTypeData(step).GetMember(nameof(DelayStep.DelaySecs)));
                innerplan.Save(planname);
            }
            try
            {
                var outerplan = new TestPlan();
                var tpr = new TestPlanReference();
                outerplan.Steps.Add(tpr);
                tpr.Filepath.Text = planname;
                tpr.LoadTestPlan();

                var annotation = AnnotationCollection.Annotate(tpr);
                var members = annotation.Get<IMembersAnnotation>().Members;
                var delaymem = annotation.GetMember("Time Delay");
                Assert.IsNotNull(delaymem);
            }
            finally
            {
                File.Delete(planname);
            }

        }
        

        [Test]
        public void SweepLoopEnabledTest()
        {
            var plan = new TestPlan();
            var sweep = new SweepLoop();
            var prog = new ProcessStep();
            plan.ChildTestSteps.Add(sweep);
            sweep.ChildTestSteps.Add(prog);
            
            sweep.SweepParameters.Add(new SweepParam(new [] {TypeData.FromType(prog.GetType()).GetMember(nameof(ProcessStep.RegularExpressionPattern))}));

            var a = AnnotationCollection.Annotate(sweep);
            a.Read();
            var a2 = a.GetMember(nameof(SweepLoop.SweepParameters));
            var col = a2.Get<ICollectionAnnotation>();
            var new1 = col.NewElement();
            col.AnnotatedElements = col.AnnotatedElements.Append(col.NewElement(), new1);
            
            var enabledmem = new1.Get<IMembersAnnotation>().Members.Last();
            var boolmember = enabledmem.Get<IMembersAnnotation>().Members.First();
            var val = boolmember.Get<IObjectValueAnnotation>();
            val.Value = true;
            
            a.Write();

            var sweepParam =  sweep.SweepParameters.FirstOrDefault();
            var en = (Enabled<string>)sweepParam.Values[1];
            Assert.IsTrue(en.IsEnabled); // from val.Value = true.

        }

        public class ErrorMetadataDutResource : Dut
        {
            [MetaData(true)]
            public double ErrorProperty { get; set; } = -5;

            public ErrorMetadataDutResource()
            {
                this.Rules.Add(() => ErrorProperty > 0, "Error property must be > 0", nameof(ErrorProperty));
            }
        }
        
        [Test]
        public void MetadataErrorAnnotation()
        {
            var p = new MetadataPromptObject() {Resources = new Dut[] {new ErrorMetadataDutResource()}};
                
            var test = AnnotationCollection.Annotate(p);
            var forwarded = test.Get<IForwardedAnnotations>();
            var member = forwarded.Forwarded.FirstOrDefault(x =>
                x.Get<IMemberAnnotation>().Member.Name == nameof(ErrorMetadataDutResource.ErrorProperty));
            
            var error = member.GetAll<IErrorAnnotation>().SelectMany(x => x.Errors).ToArray();
            Assert.AreEqual(1, error.Length);

            void checkValue(string value, int errors)
            {
                member.Get<IStringValueAnnotation>().Value = value;
                test.Write();
                test.Read();

                error = member.GetAll<IErrorAnnotation>().SelectMany(x => x.Errors).ToArray();
                Assert.AreEqual(errors, error.Length);
            }

            for (int i = 0; i < 4; i++)
            {
                checkValue("2.0", 0); // no errors.
                checkValue("-2.0", 1); // validation failed.
                checkValue("invalid", 2); // validation failed + parse error.
            }
        }

        public class ClassWithMacroString
        {
            public MacroString String { get; set; } = new MacroString();
        }

        [Test]
        public void TestClassWithMacroStringMultiSelect()
        {
            var elements = new[] {new ClassWithMacroString(), new ClassWithMacroString()};
            var elems = AnnotationCollection.Annotate(elements);
            var sv = elems.GetMember(nameof(ClassWithMacroString.String)).Get<IStringValueAnnotation>();
            sv.Value = "test";
            elems.Write();
            Assert.IsNotNull(sv.Value);
            // Check that multi-edit works.
            foreach (var elem in elements)
            {
                Assert.AreEqual("test", elem.String.ToString());
            }

            // verify that the same MacroPath instance is not being used.
            var elem2 = AnnotationCollection.Annotate(elements[0]);
            var sv2 = elem2.GetMember(nameof(ClassWithMacroString.String)).Get<IStringValueAnnotation>();
            sv2.Value = "test2";
            elem2.Write();
             
            Assert.AreEqual("test", elements[1].String.ToString());
            Assert.AreEqual("test2", elements[0].String.ToString());
            elems.Read();
            Assert.IsNull(sv.Value);
        }

        public class ClassWithMethodAnnotation
        {
            public int TimesCalled { private set; get; }
            [Browsable(true)]
            public void CallableMethod() => TimesCalled += 1;
        }

        [Test]    
        public void TestMultiSelectCallMethodAnnotation()
        {
            var elements = new[] {new ClassWithMethodAnnotation(), new ClassWithMethodAnnotation()};
            var annotation = AnnotationCollection.Annotate(elements);
            var method = annotation.GetMember(nameof(ClassWithMethodAnnotation.CallableMethod)).Get<IMethodAnnotation>();
            method.Invoke();
            foreach(var elem in elements)
                Assert.AreEqual(1, elem.TimesCalled);
        }


        public class ClassWithListOfString : TestStep
        {
            public List<string> List { get; set; } = new List<string>{"A", "B"};
            
            [AvailableValues(nameof(List))]
            public string Selected { get; set; }

            public override void Run()
            {
                
            }
        }

        [Test]
        public void ListOfStringAnnotation()
        {
            var obj = new ClassWithListOfString();
            var a = AnnotationCollection.Annotate(obj);
            var member = a.GetMember(nameof(ClassWithListOfString.List));
            var col = member.Get<ICollectionAnnotation>();
            var newelem = col.NewElement();
            Assert.IsTrue(newelem.Get<IReflectionAnnotation>().ReflectionInfo.DescendsTo(typeof(string)));
            Assert.IsNotNull(newelem.Get<IObjectValueAnnotation>().Value);
        }

        [Test]
        public void ListOfStringAnnotation2()
            {
              var obj = new ClassWithListOfString();    
            var targetObject = new DelayStep();
            var obj2 = new ClassWithListOfString();
            obj2.List.Add("C");
            var selectedMember = TypeData.GetTypeData(obj).GetMember(nameof(ClassWithListOfString.Selected));
            selectedMember.Parameterize(targetObject, obj, selectedMember.Name);
            selectedMember.Parameterize(targetObject, obj2, selectedMember.Name);
    
            // TODO:
            var b = AnnotationCollection.Annotate(targetObject);
            var avail = b.GetMember(selectedMember.Name).Get<IAvailableValuesAnnotation>();
            Assert.AreEqual(2, avail.AvailableValues.Cast<object>().Count());
        }

        [Test]
        public void ListOfStringAnnotationTpr()
        {
            var tp = new TestPlan();
            var stepa = new ClassWithListOfString();
            var selectedMember = TypeData.GetTypeData(stepa).GetMember(nameof(ClassWithListOfString.Selected));
            var stepb = new ClassWithListOfString();
            tp.ChildTestSteps.Add(stepa);
            tp.ChildTestSteps.Add(stepb);
            selectedMember.Parameterize(tp, stepa, selectedMember.Name);
            selectedMember.Parameterize(tp, stepb, selectedMember.Name);
            
            var name = Guid.NewGuid().ToString() + ".TapPlan";
            
            tp.Save(name);
            var plan2 = new TestPlan();
            var tpr = new TestPlanReference();
            plan2.ChildTestSteps.Add(tpr);
            tpr.Filepath.Text = name;
            tpr.LoadTestPlan();
            File.Delete(name);

            var member = AnnotationCollection.Annotate(tpr).GetMember(selectedMember.Name);
            var avail =  member.Get<IAvailableValuesAnnotation>();
            Assert.AreEqual(2, avail.AvailableValues.Cast<object>().Count());
        }

        [Test]
        public void TestPlanAnnotated()
        {
            var plan = new TestPlan();
            var step = new DelayStep();
            plan.ChildTestSteps.Add(step);
            var stepType = TypeData.GetTypeData(step);
            var mem = stepType.GetMember(nameof(DelayStep.DelaySecs));
            mem.Parameterize(plan, step, "delay");

            var a = AnnotationCollection.Annotate(plan);
            var members = a.Get<IMembersAnnotation>().Members;

            bool filterCol(AnnotationCollection col)
            {
                if (col.Get<IAccessAnnotation>() is IAccessAnnotation access)
                {
                    if (access.IsVisible == false) return false;
                }

                var m = col.Get<IMemberAnnotation>().Member;
                
                var browsable = m.GetAttribute<BrowsableAttribute>();

                // Browsable overrides everything
                if (browsable != null) return browsable.Browsable;

                var xmlIgnore = m.GetAttribute<XmlIgnoreAttribute>();
                if (xmlIgnore != null)
                    return false;

                if (m.HasAttribute<OutputAttribute>())
                    return true;
                if (!m.Writable || !m.Readable)
                    return false;
                return true;
            }
            
            members = members.Where(filterCol).ToArray();
            // Name, delay
            Assert.AreEqual(members.Count(), 4);
            
            AnnotationCollection getMember(string name) => members.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member.Name == name);

            var nameMem = getMember(nameof(TestPlan.Name));
            Assert.IsNotNull(nameMem);
            Assert.IsTrue(nameMem.Get<IAccessAnnotation>().IsReadOnly);
            var delayMember = getMember("delay");
            Assert.IsNotNull(delayMember);
            Assert.IsFalse(delayMember.Get<IAccessAnnotation>().IsReadOnly);
            var lockedMember = getMember(nameof(TestPlan.Locked));
            Assert.IsNotNull(lockedMember);
            Assert.IsFalse(lockedMember.Get<IAccessAnnotation>().IsReadOnly);
            var breakConditionsMember = getMember("BreakConditions");
            Assert.IsNotNull(breakConditionsMember);
            var en = breakConditionsMember.GetMember("IsEnabled");
            en.Get<IObjectValueAnnotation>().Value = true;
            en.Write();
            a.Write();
            var descr = AnnotationCollection.Annotate(step).GetMember("BreakConditions").GetMember("Value")
                .Get<IValueDescriptionAnnotation>().Describe();
            Assert.AreEqual("Break on Error (inherited from test plan).", descr);

        }
    }
}