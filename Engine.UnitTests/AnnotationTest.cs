using System;
using System.Collections;
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

        class MenuTestUserInterface : IUserInputInterface, IUserInterface
        {
            public bool WasInvoked;
            public string SelectName { get; set; }
            public void RequestUserInput(object dataObject, TimeSpan Timeout, bool modal)
            {
                var datas = AnnotationCollection.Annotate(dataObject);
                var selectedName = datas.GetMember("SelectedName");
                selectedName.Get<IStringValueAnnotation>().Value = SelectName;
                
                var scope = datas.GetMember("Scope");
                var avail = scope.Get<IAvailableValuesAnnotationProxy>();
                var values = avail.AvailableValues.ToArray();
                avail.SelectedValue = avail.AvailableValues.First(); // sequence step!
                Assert.IsTrue(values.Length > 0);
                datas.Write();
                
                WasInvoked = true;
            }

            public void NotifyChanged(object obj, string property) { }
        }
        
        [Test]
        public void MenuAnnotationTest()
        {
            var currentUserInterface = UserInput.Interface;
            var menuInterface = new MenuTestUserInterface();
            UserInput.SetInterface(menuInterface);
            try
            {
                var plan = new TestPlan();
                var sequenceRoot = new SequenceStep();
                var sequence = new SequenceStep();
                var step = new DelayStep();
                plan.Steps.Add(sequenceRoot);
                sequenceRoot.ChildTestSteps.Add(sequence);
                var step2 = new DelayStep();
                sequenceRoot.ChildTestSteps.Add(step);
                sequence.ChildTestSteps.Add(step2);
                
                { // basic functionalities test 
                    var member = AnnotationCollection.Annotate(step2).GetMember(nameof(DelayStep.DelaySecs));
                    var menu = member.Get<MenuAnnotation>();
                    
                    var items = menu.MenuItems;

                    var icons = items.ToLookup(item =>
                        item.Get<IIconAnnotation>()?.IconName ?? "");
                    var parameterizeOnTestPlan = icons[IconNames.ParameterizeOnTestPlan].First();
                    Assert.IsNotNull(parameterizeOnTestPlan);

                    // invoking this method should
                    var method = parameterizeOnTestPlan.Get<IMethodAnnotation>();
                    method.Invoke();
                    Assert.IsNotNull(plan.ExternalParameters.Get("Time Delay"));

                    var unparameterize = icons[IconNames.Unparameterize].First();
                    unparameterize.Get<IMethodAnnotation>().Invoke();
                    Assert.IsNull(plan.ExternalParameters.Get("Time Delay"));

                    var createOnParent = icons[IconNames.ParameterizeOnParent].First();
                    Assert.IsNotNull(createOnParent);
                    createOnParent.Get<IMethodAnnotation>().Invoke();
                    Assert.IsNotNull(TypeData.GetTypeData(sequence).GetMember("Parameters \\ Time Delay"));

                    unparameterize.Get<IMethodAnnotation>().Invoke();
                    Assert.IsNull(TypeData.GetTypeData(sequence).GetMember("Parameters \\ Time Delay"));

                    menuInterface.SelectName = "A";

                    var parameterize = icons[IconNames.Parameterize].First();
                    parameterize.Get<IMethodAnnotation>().Invoke();
                    Assert.IsTrue(menuInterface.WasInvoked);

                    var newParameter = TypeData.GetTypeData(sequence).GetMember("A");
                    Assert.IsNotNull(newParameter);
                    unparameterize.Get<IMethodAnnotation>().Invoke();
                    Assert.IsNull(TypeData.GetTypeData(sequence).GetMember("A"));
                    parameterize.Get<IMethodAnnotation>().Invoke();

                    var editParameter = AnnotationCollection.Annotate(sequence).GetMember("A").Get<MenuAnnotation>()
                        .MenuItems
                        .FirstOrDefault(x => x.Get<IconAnnotationAttribute>()?.IconName == IconNames.EditParameter);

                    menuInterface.SelectName = "B";
                    editParameter.Get<IMethodAnnotation>().Invoke();

                    Assert.IsNull(TypeData.GetTypeData(sequence).GetMember("A"));
                    Assert.IsNotNull(TypeData.GetTypeData(sequence).GetMember("B"));
                }
                
                { // test multi-select
                    var memberMulti = AnnotationCollection.Annotate(new[] {step, step2})
                        .GetMember(nameof(DelayStep.DelaySecs));
                    var menuMulti = memberMulti.Get<MenuAnnotation>();
                    Assert.IsNotNull(menuMulti);

                    var icons2 = menuMulti.MenuItems.ToLookup(x => x.Get<IIconAnnotation>()?.IconName ?? "");
                    var parmeterizeOnTestPlanMulti = icons2[IconNames.ParameterizeOnTestPlan].First();
                    parmeterizeOnTestPlanMulti.Get<IMethodAnnotation>().Invoke();
                    Assert.AreEqual(2, plan.ExternalParameters.Entries.FirstOrDefault().Properties.Count());

                    var unparmeterizePlanMulti = icons2[IconNames.Unparameterize].First();
                    unparmeterizePlanMulti.Get<IMethodAnnotation>().Invoke();
                    Assert.IsNull(plan.ExternalParameters.Entries.FirstOrDefault());

                    var parmeterizeOnParentMulti = icons2[IconNames.ParameterizeOnParent].First();
                    Assert.IsFalse(parmeterizeOnParentMulti.Get<IEnabledAnnotation>().IsEnabled);
                }

                { // Test Plan Enabled Items Locked
                    var annotation = AnnotationCollection.Annotate(step);
                    var menu = annotation.GetMember(nameof(DelayStep.DelaySecs))
                        .Get<MenuAnnotation>();
                    var icons = menu.MenuItems.ToLookup(x => x.Get<IIconAnnotation>()?.IconName ?? "");
                    icons[IconNames.ParameterizeOnTestPlan].First().Get<IMethodAnnotation>().Invoke();
                    annotation.Read();
                    
                    Assert.IsTrue(icons[IconNames.ParameterizeOnTestPlan].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsTrue(icons[IconNames.Parameterize].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsTrue(icons[IconNames.ParameterizeOnParent].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsTrue(icons[IconNames.Unparameterize].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(icons[IconNames.EditParameter].First().Get<IEnabledAnnotation>().IsEnabled);
                    
                    var planAnnotation = AnnotationCollection.Annotate(plan);
                    var planMenu = planAnnotation.GetMember("Time Delay")
                        .Get<MenuAnnotation>();
                    var planIcons = planMenu.MenuItems.ToLookup(x => x.Get<IIconAnnotation>()?.IconName ?? "");
                    Assert.IsFalse(planIcons[IconNames.ParameterizeOnTestPlan].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(planIcons[IconNames.Parameterize].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsTrue(planIcons[IconNames.EditParameter].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(planIcons[IconNames.ParameterizeOnParent].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(planIcons[IconNames.Unparameterize].First().Get<IEnabledAnnotation>().IsEnabled);
                    
                    plan.Locked = true;
                    menu = AnnotationCollection.Annotate(step).GetMember(nameof(DelayStep.DelaySecs))
                        .Get<MenuAnnotation>();
                    icons = menu.MenuItems.ToLookup(x => x.Get<IIconAnnotation>()?.IconName ?? "");
                    Assert.IsFalse(icons[IconNames.ParameterizeOnTestPlan].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(icons[IconNames.Parameterize].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(icons[IconNames.ParameterizeOnParent].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(icons[IconNames.Unparameterize].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(icons[IconNames.EditParameter].First().Get<IEnabledAnnotation>().IsEnabled);
                    planAnnotation = AnnotationCollection.Annotate(plan);
                    planMenu = planAnnotation.GetMember("Time Delay")
                        .Get<MenuAnnotation>();
                    planIcons = planMenu.MenuItems.ToLookup(x => x.Get<IIconAnnotation>()?.IconName ?? "");
                    Assert.IsFalse(planIcons[IconNames.ParameterizeOnTestPlan].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(planIcons[IconNames.Parameterize].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(planIcons[IconNames.EditParameter].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(planIcons[IconNames.ParameterizeOnParent].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(planIcons[IconNames.Unparameterize].First().Get<IEnabledAnnotation>().IsEnabled);
                    
                }
            }
            finally
            {
                UserInput.SetInterface(currentUserInterface as IUserInputInterface);
            }

        }

        [Test]
        public void EnabledAnnotated()
        {
            var step = new ProcessStep();
            var a = AnnotationCollection.Annotate(step);
            var x = a.GetMember(nameof(step.RegularExpressionPattern));
            var str = x.GetMember("Value");
            var strval =str.Get<IStringValueAnnotation>();
            var v1 = strval.Value.ToString();
            Assert.AreEqual("(.*)", v1);
            step.RegularExpressionPattern.Value = "test";
            a.Read();
            var v2 = strval.Value;
            Assert.AreEqual("test", v2);
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

            {
                var stepAnnotation = AnnotationCollection.Annotate(step);
                var enabled = stepAnnotation.GetMember("BreakConditions").GetMember("IsEnabled").Get<IObjectValueAnnotation>();
                enabled.Value = true; 
                //stepAnnotation.Write();
                var value = stepAnnotation.GetMember("BreakConditions").GetMember("Value").Get<IObjectValueAnnotation>();
                var thing = stepAnnotation.GetMember("BreakConditions").GetMember("Value");
                var proxy = thing.Get<IMultiSelectAnnotationProxy>();
                var sel = proxy.SelectedValues.ToArray();
                value.Value = BreakCondition.BreakOnInconclusive;
                stepAnnotation.Write();
                var descr2 = stepAnnotation.GetMember("BreakConditions").GetMember("Value")
                    .Get<IValueDescriptionAnnotation>().Describe();
                
            }
        }

        class ListOfEnumAnnotationClass
        {
            public List<string> Strings { get; set; } = new List<string>{"A", "B", "C"};
            public List<Verdict> Verdicts { get; set; } = new List<Verdict>{Verdict.Aborted, Verdict.Error};
            public List<DateTime> Dates { get; set; } = new List<DateTime>{DateTime.Now};
            public List<TimeSpan> TimeSpans { get; set; } = new List<TimeSpan>{TimeSpan.Zero, TimeSpan.Zero};
            public List<bool> Bools { get; set; } = new List<bool>() {true, false};

        }
        
        [Test]
        public void ListOfEnumAnnotation()
        {
            var obj = new ListOfEnumAnnotationClass();
            var annotation = AnnotationCollection.Annotate(obj);
            Assert.AreEqual(5, TypeData.GetTypeData(obj).GetMembers().Count());

            foreach (var member in TypeData.GetTypeData(obj).GetMembers())
            {
                int initCount = (member.GetValue(obj) as IList).Count;
                var memberAnnotation = annotation.GetMember(member.Name);
                var collection = memberAnnotation.Get<ICollectionAnnotation>();
                collection.AnnotatedElements = collection.AnnotatedElements.Append(collection.NewElement());
                annotation.Write();
                int finalCount = (TypeData.GetTypeData(obj).GetMember(member.Name).GetValue(obj) as IList).Count;
                Assert.IsTrue(initCount == finalCount - 1);
                
            }
        }

        class ArrayAnnotationClass
        {
            public string[] Strings { get; set; } = new string[] {"A", "B", "C"};
            public Verdict[] Verdicts { get; set; } = new Verdict[] {Verdict.Aborted, Verdict.Error};
            public DateTime[] Dates { get; set; } = new DateTime[] {DateTime.Now};
            public TimeSpan[] TimeSpans { get; set; } = new TimeSpan[] {TimeSpan.Zero, TimeSpan.Zero};
            public bool[] Bools { get; set; } = new bool[] {true, false};
        }

        [Test]
        public void AddToFixedSizeAnnotation()
        {
            var obj = new ArrayAnnotationClass();
            var annotation = AnnotationCollection.Annotate(obj);
            
            Assert.AreEqual(5, TypeData.GetTypeData(obj).GetMembers().Count());

            foreach (var member in TypeData.GetTypeData(obj).GetMembers())
            {
                int initCount = (member.GetValue(obj) as IList).Count;
                var memberAnnotation = annotation.GetMember(member.Name);
                var collection = memberAnnotation.Get<ICollectionAnnotation>();
                
                collection.AnnotatedElements = collection.AnnotatedElements.Append(collection.NewElement());
                
                annotation.Write();
                int finalCount = (TypeData.GetTypeData(obj).GetMember(member.Name).GetValue(obj) as IList).Count;
                Assert.IsTrue(initCount == finalCount - 1);
            }
            
        }

        [Test]
        public void RemoveFromFixedSizeAnnotation()
        {
            var obj = new ArrayAnnotationClass();
            var annotation = AnnotationCollection.Annotate(obj);
            
            Assert.AreEqual(5, TypeData.GetTypeData(obj).GetMembers().Count());
            
            foreach (var member in TypeData.GetTypeData(obj).GetMembers())
            {
                int initCount = (member.GetValue(obj) as IList).Count;
                var memberAnnotation = annotation.GetMember(member.Name);
                var collection = memberAnnotation.Get<ICollectionAnnotation>();
                
                collection.AnnotatedElements = collection.AnnotatedElements
                    .Take(collection.AnnotatedElements.Count() - 1).ToList();
                
                annotation.Write();
                int finalCount = (TypeData.GetTypeData(obj).GetMember(member.Name).GetValue(obj) as IList).Count;
                Assert.IsTrue(initCount == finalCount + 1);
            }
            
        }

        public class MemberWithException
        {
            public class SubThing
            {
                public double Value => throw new ArgumentNullException();
            }
            public SubThing Value
            {
                get => throw new ArgumentNullException();
                set { }    
            }

            public SubThing Value2
            {
                set{}
            }
            
        }

        [Test]
        public void TestMemberWithExceptionAnnotation()
        {
            var annotation = AnnotationCollection.Annotate(new MemberWithException());
            Assert.AreEqual(1, annotation.Get<IMembersAnnotation>().Members.Count());
            annotation.Read();
            Assert.AreEqual(1, annotation.Get<IMembersAnnotation>().Members.Count());
        } 
        
    }
}