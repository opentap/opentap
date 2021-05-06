using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using NUnit.Framework;
using OpenTap.Engine.UnitTests.TestTestSteps;
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
            var targetObject = new SequenceStep();
            var obj2 = new ClassWithListOfString();
            targetObject.ChildTestSteps.Add(obj);
            targetObject.ChildTestSteps.Add(obj2);
            obj2.List.Add("C");
            var selectedMember = TypeData.GetTypeData(obj).GetMember(nameof(ClassWithListOfString.Selected));
            selectedMember.Parameterize(targetObject, obj, selectedMember.Name);
            Assert.Throws<Exception>(() => selectedMember.Parameterize(targetObject, obj, selectedMember.Name));
            selectedMember.Parameterize(targetObject, obj2, selectedMember.Name);
            Assert.Throws<Exception>(() => selectedMember.Parameterize(targetObject, obj2, selectedMember.Name));
    
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
            [Flags]
            public enum Mode
            {
                Standard = 1,
                Rename = 2,
                Create = 4,
                TestPlan = 8,
                Remove = 16,
                ExpectNoRename = 32,
                RenameBlank = 64,
                Merge = 128,
                Error = 256,
                Cancel = 512
            }
            public bool WasInvoked;
            public string SelectName { get; set; }

            public Mode SelectedMode;

            public string LastError = "";

            
            public string ErrorString { get; set; }
            
            public void RequestUserInput(object dataObject, TimeSpan Timeout, bool modal)
            {
                var datas = AnnotationCollection.Annotate(dataObject);
                var selectedName = datas.GetMember("SelectedName");
                var message = datas.GetMember("Message");
                var response = datas.GetMember("Response");
                var resp = response.Get<IAvailableValuesAnnotationProxy>();
                if (SelectedMode.HasFlag(Mode.Cancel))
                {
                    resp.SelectedValue =
                        resp.AvailableValues.First(x => x.Get<IStringValueAnnotation>().Value == "Cancel");
                }
                
                
                selectedName.Get<IStringValueAnnotation>().Value = SelectName;
                
                var scope = datas.GetMember("Scope");
                var avail = scope.Get<IAvailableValuesAnnotationProxy>();
                var values = avail.AvailableValues.ToArray();
                
                avail.SelectedValue = avail.AvailableValues.First(); // sequence step!
                if (SelectedMode.HasFlag(Mode.TestPlan))
                {
                    avail.SelectedValue = avail.AvailableValues.Last();
                }

                if (SelectedMode.HasFlag(Mode.Remove))
                {
                    var settings = datas.GetMember("Settings");
                    settings.Get<IMultiSelectAnnotationProxy>().SelectedValues = Array.Empty<AnnotationCollection>();
                }
                Assert.IsTrue(values.Length > 0);
                datas.Write();
                
                if (SelectedMode.HasFlag(Mode.Rename))
                {
                    var msg = message.Get<IStringValueAnnotation>().Value;
                    
                    Assert.AreEqual(false == SelectedMode.HasFlag(Mode.ExpectNoRename), msg.Contains("Rename "));
                }

                if (SelectedMode.HasFlag(Mode.RenameBlank))
                {
                    var error = selectedName.Get<IErrorAnnotation>().Errors.First();
                    Assert.AreEqual("Name cannot be left empty.", error);
                }

                if (SelectedMode == (Mode.Create|Mode.TestPlan))
                {
                    var msg = message.Get<IStringValueAnnotation>().Value;
                    Assert.IsTrue(msg.Contains("Create new parameter "));   
                }

                LastError = message.Get<IErrorAnnotation>()?.Errors.FirstOrDefault();
                if (SelectedMode == (Mode.Merge|Mode.TestPlan))
                {
                    var msg = message.Get<IStringValueAnnotation>().Value;
                    Assert.IsTrue(msg.Contains("Merge with an existing parameter "));   
                }

                if (SelectedMode.HasFlag(Mode.Error))
                {
                    
                    Assert.IsTrue(LastError != null);
                    if(ErrorString != null)
                        Assert.IsTrue(LastError.Contains(ErrorString));    
                }
                
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
                    Assert.IsNotNull(plan.ExternalParameters.Get("Parameters \\ Time Delay"));

                    var unparameterize = icons[IconNames.Unparameterize].First();
                    unparameterize.Get<IMethodAnnotation>().Invoke();
                    Assert.IsNull(plan.ExternalParameters.Get("Parameters \\ Time Delay"));

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
                    
                    {
                        // verify that the right icon annotation now appears.
                        var member2 = AnnotationCollection.Annotate(step2).GetMember(nameof(DelayStep.DelaySecs));
                        var parameterized = member2.Get<ParameterizedIconAnnotation>();
                        Assert.IsNotNull(parameterized);
                        var disabled = member2.GetAll<IEnabledAnnotation>().Any(x => x.IsEnabled == false);
                        Assert.IsTrue(disabled);
                    }

                    var newParameter = TypeData.GetTypeData(sequence).GetMember("A");
                    Assert.IsNotNull(newParameter);
                    unparameterize.Get<IMethodAnnotation>().Invoke();
                    Assert.IsNull(TypeData.GetTypeData(sequence).GetMember("A"));
                    parameterize.Get<IMethodAnnotation>().Invoke();

                    var editParameter = AnnotationCollection.Annotate(sequence).GetMember("A").Get<MenuAnnotation>()
                        .MenuItems
                        .FirstOrDefault(x => x.Get<IconAnnotationAttribute>()?.IconName == IconNames.EditParameter);

                    menuInterface.SelectName = " ";
                    menuInterface.SelectedMode = MenuTestUserInterface.Mode.RenameBlank;
                    editParameter.Get<IMethodAnnotation>().Invoke();
                    Assert.IsNotNull(TypeData.GetTypeData(sequence).GetMember("A"));
                    Assert.IsNull(TypeData.GetTypeData(sequence).GetMember(""));

                    
                    menuInterface.SelectName = "B";
                    menuInterface.SelectedMode = MenuTestUserInterface.Mode.Rename;
                    editParameter.Get<IMethodAnnotation>().Invoke();

                    var bMember = TypeData.GetTypeData(sequence).GetMember("B");
                    Assert.IsNull(TypeData.GetTypeData(sequence).GetMember("A"));
                    Assert.IsNotNull(bMember);
                    menuInterface.SelectedMode |= MenuTestUserInterface.Mode.ExpectNoRename;
                    editParameter = AnnotationCollection.Annotate(sequence).GetMember("B").Get<MenuAnnotation>()
                        .MenuItems
                        .FirstOrDefault(x => x.Get<IconAnnotationAttribute>()?.IconName == IconNames.EditParameter);

                    editParameter.Get<IMethodAnnotation>().Invoke();
                    Assert.IsTrue(object.ReferenceEquals(bMember, TypeData.GetTypeData(sequence).GetMember("B")));


                    unparameterize.Get<IMethodAnnotation>().Invoke();
                    
                    Assert.IsNull(TypeData.GetTypeData(sequence).GetMember("B"));

                    menuInterface.SelectedMode = MenuTestUserInterface.Mode.Create | MenuTestUserInterface.Mode.TestPlan;
                    parameterize.Get<IMethodAnnotation>().Invoke();

                    {
                        var member2 = AnnotationCollection.Annotate(plan).GetMember("B");
                        
                        {
                            // verify that the right icon annotation now appears.
                            var parameter = member2.GetAll<IInteractiveIconAnnotation>()
                                .First(x => x.IconName == IconNames.EditParameter);
                            Assert.IsNotNull(parameter.Action.Get<IMethodAnnotation>());
                        }
                        
                        var edit = member2.Get<MenuAnnotation>().MenuItems.FirstOrDefault(x =>
                            x.Get<IconAnnotationAttribute>()?.IconName == IconNames.EditParameter);
                        menuInterface.SelectedMode = MenuTestUserInterface.Mode.Rename| MenuTestUserInterface.Mode.TestPlan;
                        menuInterface.SelectName = "C";
                        edit.Get<IMethodAnnotation>().Invoke();
                        
                        member2 = AnnotationCollection.Annotate(plan).GetMember("C");
                        edit = member2.Get<MenuAnnotation>().MenuItems.FirstOrDefault(x =>
                            x.Get<IconAnnotationAttribute>()?.IconName == IconNames.EditParameter);
                        
                        menuInterface.SelectedMode = MenuTestUserInterface.Mode.Remove;
                        edit.Get<IMethodAnnotation>().Invoke();
                        Assert.IsNull(AnnotationCollection.Annotate(plan).GetMember("C"));
                    }
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
                    {
                        // check that the right icon appeared.
                        var memberMulti2 = AnnotationCollection.Annotate(new[] {step, step2})
                            .GetMember(nameof(DelayStep.DelaySecs));
                        bool parameterizedIcon = memberMulti2.GetAll<IIconAnnotation>().Any(x => x.IconName == IconNames.Parameterized);
                        Assert.IsTrue(parameterizedIcon);
                    }
                    
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
                    
                    Assert.IsFalse(icons[IconNames.ParameterizeOnTestPlan].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(icons[IconNames.Parameterize].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(icons[IconNames.ParameterizeOnParent].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsTrue(icons[IconNames.Unparameterize].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(icons[IconNames.EditParameter].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(icons[IconNames.RemoveParameter].First().Get<IEnabledAnnotation>().IsEnabled);
                    
                    var planAnnotation = AnnotationCollection.Annotate(plan);
                    var planMenu = planAnnotation.GetMember("Parameters \\ Time Delay")
                        .Get<MenuAnnotation>();
                    var planIcons = planMenu.MenuItems.ToLookup(x => x.Get<IIconAnnotation>()?.IconName ?? "");
                    Assert.IsFalse(planIcons[IconNames.ParameterizeOnTestPlan].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(planIcons[IconNames.Parameterize].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsTrue(planIcons[IconNames.EditParameter].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(planIcons[IconNames.ParameterizeOnParent].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(planIcons[IconNames.Unparameterize].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsTrue(planIcons[IconNames.RemoveParameter].First().Get<IEnabledAnnotation>().IsEnabled);
                    
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
                    planMenu = planAnnotation.GetMember("Parameters \\ Time Delay")
                        .Get<MenuAnnotation>();
                    planIcons = planMenu.MenuItems.ToLookup(x => x.Get<IIconAnnotation>()?.IconName ?? "");
                    Assert.IsFalse(planIcons[IconNames.ParameterizeOnTestPlan].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(planIcons[IconNames.Parameterize].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(planIcons[IconNames.EditParameter].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(planIcons[IconNames.ParameterizeOnParent].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(planIcons[IconNames.Unparameterize].First().Get<IEnabledAnnotation>().IsEnabled);
                    Assert.IsFalse(planIcons[IconNames.RemoveParameter].First().Get<IEnabledAnnotation>().IsEnabled);
                }
                {
                    // remove parameter
                    plan.Locked = false;
                    var planAnnotation = AnnotationCollection.Annotate(plan);
                    var menu = planAnnotation.GetMember("Parameters \\ Time Delay").Get<MenuAnnotation>();
                    var removeItem = menu.MenuItems.First(x => x.Get<IconAnnotationAttribute>()?.IconName == IconNames.RemoveParameter);
                    removeItem.Get<IMethodAnnotation>().Invoke();
                    planAnnotation = AnnotationCollection.Annotate(plan);
                    // after removing there is not Time Delay parameter..
                    Assert.IsNull(planAnnotation.GetMember("Parameters \\ Time Delay"));
                }
                {// Break Conditions
                    var member = AnnotationCollection.Annotate(step2).GetMember("BreakConditions");
                    Assert.NotNull(member);
                    var menu = member.Get<MenuAnnotation>();
                    Assert.NotNull(menu);
                    var parameterize = menu.MenuItems.FirstOrDefault(x =>
                        x.Get<IIconAnnotation>()?.IconName == IconNames.ParameterizeOnTestPlan);
                    Assert.IsTrue(parameterize.Get<IAccessAnnotation>().IsVisible);
                    
                    Assert.AreEqual(1, TypeData.GetTypeData(plan).GetMembers().Count(x => x.Name.Contains("BreakConditions") || x.Name.Contains("BreakConditions")));
                    parameterize.Get<IMethodAnnotation>().Invoke();
                    Assert.AreEqual(2, TypeData.GetTypeData(plan).GetMembers().Count(x => x.Name.Contains("Break Conditions") || x.Name.Contains("BreakConditions")));
                    
                }
            }
            finally
            {
                UserInput.SetInterface(currentUserInterface as IUserInputInterface);
            }
        }
        [Test]
        public void MenuAnnotationTest2()
        {
            var currentUserInterface = UserInput.Interface;
            var menuInterface = new MenuTestUserInterface();
            UserInput.SetInterface(menuInterface);
            try
            {
                var plan = new TestPlan();
                var delay = new DelayStep();
                plan.Steps.Add(delay);

                { // basic functionalities test 
                    var member = AnnotationCollection.Annotate(delay).GetMember(nameof(DelayStep.DelaySecs));
                    var menu = member.Get<MenuAnnotation>();
                    var items = menu.MenuItems;

                    var icons = items.ToLookup(item =>
                        item.Get<IIconAnnotation>()?.IconName ?? "");
                    var parameterizeOnTestPlan = icons[IconNames.ParameterizeOnTestPlan].First();
                    Assert.IsNotNull(parameterizeOnTestPlan);

                    // invoking this method should
                    var method = parameterizeOnTestPlan.Get<IMethodAnnotation>();
                    method.Invoke();
                    Assert.IsNotNull(plan.ExternalParameters.Get("Parameters \\ Time Delay"));

                    member = AnnotationCollection.Annotate(delay).GetMember(nameof(DelayStep.DelaySecs));
                    menu = member.Get<MenuAnnotation>();
                    items = menu.MenuItems;

                    icons = items.ToLookup(item =>
                        item.Get<IIconAnnotation>()?.IconName ?? "");
                    parameterizeOnTestPlan = icons[IconNames.ParameterizeOnTestPlan].First();
                    Assert.IsNotNull(parameterizeOnTestPlan);
                    
                    // This fails, which it should not.
                    Assert.IsFalse(parameterizeOnTestPlan.Get<IEnabledAnnotation>().IsEnabled);
                }
            }
            finally
            {
                UserInput.SetInterface(currentUserInterface as IUserInputInterface);
            }

        }
        [Test]
        public void MenuAnnotationTest3()
        {
            var currentUserInterface = UserInput.Interface;
            var menuInterface = new MenuTestUserInterface();
            UserInput.SetInterface(menuInterface);
            try
            {
                const string doubleTimeDelay = "Parameters \\ Time Delay";
                const string byteTimeDelay = "Parameters \\ Byte Time Delay";
                const string shortTimeDelay = "Parameters \\ Short Time Delay";
                var plan = new TestPlan();
                var delay1 = new DelayStep();
                var delay2 = new ByteDelayStep();
                var delay3 = new ShortDelayStep();
                plan.Steps.Add(delay1);
                plan.Steps.Add(delay2);
                plan.Steps.Add(delay3);

                { // basic functionalities test 
                    var member = AnnotationCollection.Annotate(delay1).GetMember(nameof(DelayStep.DelaySecs));
                    var menu = member.Get<MenuAnnotation>();
                    var items = menu.MenuItems;

                    var icons = items.ToLookup(item => item.Get<IIconAnnotation>()?.IconName ?? "");
                    var parameterizeOnTestPlan = icons[IconNames.ParameterizeOnTestPlan].First();
                    Assert.IsNotNull(parameterizeOnTestPlan);
                    var method = parameterizeOnTestPlan.Get<IMethodAnnotation>();
                    method.Invoke();
                    Assert.IsNotNull(plan.ExternalParameters.Get(doubleTimeDelay));

                    member = AnnotationCollection.Annotate(delay2).GetMember(nameof(ByteDelayStep.DelaySecs));
                    menu = member.Get<MenuAnnotation>();
                    items = menu.MenuItems;

                    icons = items.ToLookup(item => item.Get<IIconAnnotation>()?.IconName ?? "");
                    parameterizeOnTestPlan = icons[IconNames.ParameterizeOnTestPlan].First();
                    Assert.IsNotNull(parameterizeOnTestPlan);
                    method = parameterizeOnTestPlan.Get<IMethodAnnotation>();
                    method.Invoke();
                    Assert.IsNotNull(plan.ExternalParameters.Get(byteTimeDelay));

                    member = AnnotationCollection.Annotate(delay3).GetMember(nameof(ShortDelayStep.DelaySecs));
                    menu = member.Get<MenuAnnotation>();
                    items = menu.MenuItems;

                    icons = items.ToLookup(item => item.Get<IIconAnnotation>()?.IconName ?? "");
                    parameterizeOnTestPlan = icons[IconNames.ParameterizeOnTestPlan].First();
                    Assert.IsNotNull(parameterizeOnTestPlan);
                    method = parameterizeOnTestPlan.Get<IMethodAnnotation>();
                    method.Invoke();
                    Assert.IsNotNull(plan.ExternalParameters.Get(shortTimeDelay));

                    var editParameter = AnnotationCollection.Annotate(plan).GetMember(byteTimeDelay).Get<MenuAnnotation>()
                        .MenuItems
                        .FirstOrDefault(x => x.Get<IconAnnotationAttribute>()?.IconName == IconNames.EditParameter);

                    // Try to merge byte time delay and time delay parameters
                    menuInterface.SelectName = doubleTimeDelay;
                    menuInterface.SelectedMode = MenuTestUserInterface.Mode.Rename | MenuTestUserInterface.Mode.Cancel;

                    editParameter.Get<IMethodAnnotation>().Invoke();
                    Assert.IsNull(menuInterface.LastError);

                    // Try to merge byte time delay and short time delay parameters, overflow exception occurs
                    editParameter = AnnotationCollection.Annotate(plan).GetMember(shortTimeDelay).Get<MenuAnnotation>()
                        .MenuItems
                        .FirstOrDefault(x => x.Get<IconAnnotationAttribute>()?.IconName == IconNames.EditParameter);

                    menuInterface.SelectName = byteTimeDelay;
                    menuInterface.SelectedMode = MenuTestUserInterface.Mode.Rename | MenuTestUserInterface.Mode.Cancel;

                    editParameter.Get<IMethodAnnotation>().Invoke();
                    Assert.IsNull(menuInterface.LastError);

                    menuInterface.SelectedMode = MenuTestUserInterface.Mode.Rename;
                    // Try to merge byte and short time delay parameters, this is allowed
                    delay3.DelaySecs = 12;
                    editParameter.Get<IMethodAnnotation>().Invoke();
                    Assert.IsNull(menuInterface.LastError);

                    // Try to set an overflowed value
                    member = AnnotationCollection.Annotate(plan).GetMember(byteTimeDelay);
                    member.SetValue(50000);
                    var memberParam = member.Get<NumberAnnotation>();
                    Assert.IsNotNull(memberParam.Errors);
                }
            }
            finally
            {
                UserInput.SetInterface(currentUserInterface as IUserInputInterface);
            }
        }

        public class ShortDelayStep : TestStep
        {
            [Display("Short Time Delay")]
            public short DelaySecs { get; set; } = 222;
            public override void Run()
            {
                TapThread.Sleep(Time.FromSeconds(DelaySecs));
            }
        }

        public class ByteDelayStep : TestStep
        {
            [Display("Byte Time Delay")]
            public sbyte DelaySecs { get; set; } = 3;
            public override void Run()
            {
                TapThread.Sleep(Time.FromSeconds(DelaySecs));
            }
        }
        public class TwoDelayStep : TestStep
        {
            public double DelaySecs { get; set; }
            public double DelaySecs2 { get; set; }
            public override void Run()
            {
                
            }
        }

        [Test]
        public void ParameterizeTest()
        {
            var plan = new TestPlan();
            var delayStep = new DelayStep();
            plan.ChildTestSteps.Add(delayStep);
            
            // Check can parameterize
            var member = AnnotationCollection.Annotate(delayStep).GetMember(nameof(DelayStep.DelaySecs));
            var menu = member.Get<MenuAnnotation>();
            var menuItems = menu.MenuItems;
            var icons = menuItems.ToLookup(item => item.Get<IIconAnnotation>()?.IconName ?? "");
            var parameterizeIcon = icons[IconNames.Parameterize].First();
            Assert.IsTrue(parameterizeIcon.Get<IAccessAnnotation>().IsVisible);
            parameterizeIcon = icons[IconNames.Unparameterize].First();
            Assert.IsFalse(parameterizeIcon.Get<IAccessAnnotation>().IsVisible);
            
            // Parameterize 
            var delayMember = TypeData.GetTypeData(delayStep).GetMember(nameof(DelayStep.DelaySecs));
            delayMember.Parameterize(plan, delayStep, "Delay");
            
            // Check can parameterize
            member = AnnotationCollection.Annotate(delayStep).GetMember(nameof(DelayStep.DelaySecs));
            menu = member.Get<MenuAnnotation>();
            menuItems = menu.MenuItems;
            icons = menuItems.ToLookup(item => item.Get<IIconAnnotation>()?.IconName ?? "");
            parameterizeIcon = icons[IconNames.Parameterize].First();
            Assert.IsFalse(parameterizeIcon.Get<IAccessAnnotation>().IsVisible);
            parameterizeIcon = icons[IconNames.Unparameterize].First();
            Assert.IsTrue(parameterizeIcon.Get<IAccessAnnotation>().IsVisible);
            
            // Set read only
            delayStep.IsReadOnly = true;
            
            // Check can parameterize
            member = AnnotationCollection.Annotate(delayStep).GetMember(nameof(DelayStep.DelaySecs));
            menu = member.Get<MenuAnnotation>();
            menuItems = menu.MenuItems;
            icons = menuItems.ToLookup(item => item.Get<IIconAnnotation>()?.IconName ?? "");
            parameterizeIcon = icons[IconNames.Unparameterize].First();
            Assert.IsFalse(parameterizeIcon.Get<IAccessAnnotation>().IsVisible);
        }
        
        /// <summary>
        /// When the test plan is changed, parameters that becomes invalid needs to be removed.
        /// This is done by doing some checks in DynamicMemberTypeDataProvider.
        /// In this test it is verified that that behavior works as expected.
        /// </summary>
        [Test]
        public void AutoRemoveParameters()
        {
            var plan = new TestPlan();
            var step = new TwoDelayStep();
            plan.ChildTestSteps.Add(step);
            var member = TypeData.GetTypeData(step).GetMember(nameof(step.DelaySecs));
            var parameter = member.Parameterize(plan, step, "delay");
            Assert.IsNotNull(TypeData.GetTypeData(plan).GetMember(parameter.Name));
            plan.ChildTestSteps.Remove(step);
            Assert.IsNull(TypeData.GetTypeData(plan).GetMember(parameter.Name));
            
            var seq = new SequenceStep();
            plan.ChildTestSteps.Add(seq);
            seq.ChildTestSteps.Add(step);
            parameter = member.Parameterize(seq, step, "delay");
            Assert.IsNotNull(TypeData.GetTypeData(seq).GetMember(parameter.Name));
            seq.ChildTestSteps.Remove(step);
            Assert.IsNull(TypeData.GetTypeData(seq).GetMember(parameter.Name));
            
            seq.ChildTestSteps.Add(step);
            parameter = member.Parameterize(seq, step, "delay");
            var member2 = TypeData.GetTypeData(seq).GetMember(parameter.Name);
            Assert.IsNotNull(member2);
            Assert.AreEqual(member2, parameter);
            var parameter2 = member2.Parameterize(plan, seq, "delay");
            Assert.IsNotNull(TypeData.GetTypeData(plan).GetMember(parameter2.Name));
            plan.ChildTestSteps.Remove(seq);
            Assert.IsNull(TypeData.GetTypeData(plan).GetMember(parameter2.Name));
            Assert.IsNotNull(TypeData.GetTypeData(seq).GetMember(parameter.Name));
            
            plan.ChildTestSteps.Add(seq);
            parameter2 = member2.Parameterize(plan, seq, "delay");
            var member3 = TypeData.GetTypeData(step).GetMember(nameof(TwoDelayStep.DelaySecs2));
            var parameter3 = member3.Parameterize(plan, step, "delay2");
            Assert.IsNotNull(TypeData.GetTypeData(plan).GetMember(parameter2.Name));
            Assert.IsNotNull(TypeData.GetTypeData(plan).GetMember(parameter3.Name));
            Assert.IsNotNull(TypeData.GetTypeData(seq).GetMember(parameter.Name));
            seq.ChildTestSteps.Remove(step);
            Assert.IsNull(TypeData.GetTypeData(plan).GetMember(parameter2.Name));
            Assert.IsNull(TypeData.GetTypeData(plan).GetMember(parameter3.Name));
            Assert.IsNull(TypeData.GetTypeData(seq).GetMember(parameter.Name));
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
            Assert.AreEqual(5, members.Count());
            
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
        public void MultiSelectParameterize()
        {
            // Previously some performance issue causes this use-case to take several minutes.
            // After fixing I added this test that shows that it does not take an extraordinary amount
            // of time to parameterize a property on many test steps.
            
            var plan = new TestPlan();
            List<DelayStep> steps = new List<DelayStep>();
            for (int i = 0; i < 500; i++) // note: Run this at 50000 iterations to determine limits.
            {
                var step = new DelayStep();
                plan.ChildTestSteps.Add(step);
                steps.Add(step);
            }

            var a = AnnotationCollection.Annotate(steps.ToArray());
            var menu = a.GetMember(nameof(DelayStep.DelaySecs)).Get<MenuAnnotation>();
            var parameterize = menu.MenuItems.FirstOrDefault(x =>
                x.Get<IconAnnotationAttribute>().IconName == IconNames.ParameterizeOnTestPlan);
            var unparameterize = menu.MenuItems.FirstOrDefault(x =>
                x.Get<IconAnnotationAttribute>().IconName == IconNames.Unparameterize);

            var sw = Stopwatch.StartNew();
            parameterize.Get<IMethodAnnotation>().Invoke();
            
            
            var elapsed = sw.Elapsed;

            var sw4 = Stopwatch.StartNew();
            var xml = plan.SerializeToString();
            var serializeElapsed = sw4.Elapsed;
            
            var sw5= Stopwatch.StartNew();
            Utils.DeserializeFromString<TestPlan>(xml);
            var deserializeElapsed = sw5.Elapsed;

            
            Assert.AreEqual(1, TypeData.GetTypeData(plan).GetMembers().OfType<ParameterMemberData>().Count());

            var parameter = TypeData.GetTypeData(plan).GetMembers().OfType<ParameterMemberData>().First();
            
            var editParameter = AnnotationCollection.Annotate(plan).GetMember(parameter.Name).Get<MenuAnnotation>().MenuItems.First(x =>
                x.Get<IconAnnotationAttribute>()?.IconName == IconNames.EditParameter);
            var currentUserInterface = UserInput.Interface;
            var menuInterface = new MenuTestUserInterface();
            UserInput.SetInterface(menuInterface);
            TimeSpan elapsed3 = TimeSpan.MaxValue;
            try
            {
                menuInterface.SelectName = "B";
                var sw3 = Stopwatch.StartNew();
                editParameter.Get<IMethodAnnotation>().Invoke();
                elapsed3 = sw3.Elapsed;
                    
            }
            finally
            {
                UserInput.SetInterface((IUserInputInterface)currentUserInterface);    
            }


            var sw2 = Stopwatch.StartNew();
            unparameterize.Get<IMethodAnnotation>().Invoke();
            var elapsed2 = sw2.Elapsed;

            Assert.AreEqual(0, TypeData.GetTypeData(plan).GetMembers().OfType<ParameterMemberData>().Count());

            // these limits are found at 50000 entries, so it is assumed that at 500, they will always work
            // unless some quadratic complexity has been introduced.
            Assert.IsTrue(serializeElapsed.TotalSeconds < 10);
            Assert.IsTrue(deserializeElapsed.TotalSeconds < 15);
            Assert.IsTrue(elapsed.TotalSeconds < 3);
            Assert.IsTrue(elapsed2.TotalSeconds < 3);
            Assert.IsTrue(elapsed3.TotalSeconds < 4);
        }

        [Test]
        public void ParameterInputsTest()
        {
            var plan = new TestPlan();
            var verdictStep = new VerdictStep();
            var ifStep1 = new IfStep();
            var ifStep2 = new IfStep();
            plan.ChildTestSteps.Add(verdictStep);
            plan.ChildTestSteps.Add(ifStep1);
            plan.ChildTestSteps.Add(ifStep2);
            var member = TypeData.GetTypeData(ifStep1).GetMember(nameof(ifStep1.InputVerdict));
            Assert.IsTrue(ParameterManager.CanParameter(member, new ITestStepParent[] {ifStep1}));
            ParameterManager.Parameterize(plan, member, new ITestStepParent[] {ifStep1, ifStep2}, "A");

            var verdictParameter = TypeData.GetTypeData(plan).GetMember("A");
            
            var value = (Input<Verdict>)verdictParameter.GetValue(plan);
            Assert.IsFalse(object.ReferenceEquals(ifStep1.InputVerdict, ifStep2.InputVerdict));
            verdictParameter.SetValue(plan, value);
            value.Step = verdictStep;
            TypeData.GetTypeData(plan).GetMember("A").SetValue(plan, value);
            Assert.IsFalse(object.ReferenceEquals(ifStep1.InputVerdict, ifStep2.InputVerdict));
            Assert.IsTrue(object.ReferenceEquals(ifStep1.InputVerdict.Step, ifStep2.InputVerdict.Step));

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

        public class TestStepWithLists : TestStep
        {
            public List<double> DoubleValues { get; set; } = new List<double>();

            public class Item
            {
                public double X { get; set; }
            }
            
            public List<Item> Items { get; set; } = new List<Item>();
            public override void Run()
            {
                
            }
        }

        [Test]
        public void ParameteriseLists()
        {
            var obj1 = new TestStepWithLists();
            var obj2 = new TestStepWithLists();
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(obj1);
            plan.ChildTestSteps.Add(obj2);

            void doTest(string name, TestStepWithLists obj, bool canAnnotate)
            {
                var a1 = AnnotationCollection.Annotate(obj).GetMember(name);
                var p = a1.GetIcon(IconNames.ParameterizeOnTestPlan);
                if (canAnnotate)
                {
                    Assert.IsTrue(p.Get<IEnabledAnnotation>().IsEnabled);
                    p.Get<IMethodAnnotation>().Invoke();
                    p.Read();
                    Assert.IsFalse(p.Get<IEnabledAnnotation>().IsEnabled);
                }
                else
                {
                    Assert.IsFalse(p.Get<IEnabledAnnotation>().IsEnabled);
                }
            }

            doTest(nameof(obj1.DoubleValues), obj1, true);
            doTest(nameof(obj2.DoubleValues), obj2, true);
            doTest(nameof(obj1.Items), obj1, true);
            doTest(nameof(obj2.Items), obj2, true);

            // finally, test that modifying the value also propagates.
            var items = AnnotationCollection.Annotate(plan).GetMember("Parameters \\ " + nameof(obj1.Items));
            Assert.IsNotNull(items);

            var collection = items.Get<ICollectionAnnotation>();
            collection.AnnotatedElements = collection.AnnotatedElements.Append(collection.NewElement()).ToArray();
            items.Write();

            Assert.AreEqual(1, obj1.Items.Count);
            Assert.AreEqual(1, obj2.Items.Count);
            
            // not same reference.
            Assert.AreNotEqual(obj1.Items, obj2.Items);
        }

        [Test]
        public void AddExternalParameterWarning()
        {
            var obj1 = new TestStepWithLists();
            var obj2 = new TestStepWithLists();
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(obj1);
            plan.ChildTestSteps.Add(obj2);
            var member= TypeData.GetTypeData(obj1).GetMember(nameof(obj1.Items));
            plan.ExternalParameters.Add(obj1, member, "items");
            
            // produces warning, does not add since the item type is not compatible.
            var ext = plan.ExternalParameters.Add(obj2, member, "items");
            Assert.AreEqual(1, ext.Properties.Count());

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

        [Test]
        public void SweepParametersAnnotation()
        {
            var plan = new TestPlan();
            var sweep = new SweepParameterStep();
            var log = new LogStep() { };
            log.LogMessage = "msg0";
            plan.Steps.Add(sweep);
            sweep.ChildTestSteps.Add(log);
            AnnotationCollection.Annotate(log)
                .GetMember(nameof(log.LogMessage))
                .ExecuteIcon(IconNames.ParameterizeOnParent);
            
            AnnotationCollection.Annotate(sweep)
                .GetMember(nameof(sweep.SweepValues))
                .ExecuteIcon(IconNames.ParameterizeOnTestPlan);

            var sweepValues = AnnotationCollection.Annotate(plan).GetMember("Parameters \\ Sweep \\ Sweep Values");
            Assert.IsNotNull(sweepValues);
            var collection = sweepValues.Get<ICollectionAnnotation>();
            for (int i = 0; i < 5; i++)
            {
                collection.AnnotatedElements = collection.AnnotatedElements.Append(collection.NewElement());
                sweepValues.Write();
                sweepValues.Read();
                Assert.IsTrue(collection.AnnotatedElements.Count() == i + 1);
                var row1 = collection.AnnotatedElements.Last();
                var enabledMember = row1.GetMember("Enabled");
                var valueMember = row1.GetMember("Parameters \\ Log Message");
                valueMember.SetValue("msg" + i);
                Assert.IsNotNull(enabledMember);
                Assert.IsNotNull(valueMember);
            }

            sweepValues.Write();
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual("msg" + i, sweep.SweepValues[i].Values["Parameters \\ Log Message"]);
            }
        }

        [Test]
        public void SweepLoopUnparameterizable()
        {
            var plan = new TestPlan();
            var sweep = new SweepLoop();
            var delay = new DelayStep();
            plan.ChildTestSteps.Add(sweep);
            sweep.ChildTestSteps.Add(delay);
            var members = new List<IMemberData>()
                {TypeData.GetTypeData(delay).GetMember(nameof(delay.DelaySecs))};
            sweep.SweepParameters.Add(new SweepParam(members));

            var sweepParametersAnnotation = AnnotationCollection.Annotate(sweep).GetMember(nameof(sweep.SweepParameters));
            Assert.IsFalse(sweepParametersAnnotation.GetIcon(IconNames.ParameterizeOnTestPlan).Get<IEnabledAnnotation>().IsEnabled);
            var nameAnnotation = AnnotationCollection.Annotate(sweep).GetMember(nameof(sweep.Name));
            Assert.IsTrue(nameAnnotation.GetIcon(IconNames.ParameterizeOnTestPlan).Get<IEnabledAnnotation>().IsEnabled);
        }

        [TestCase(nameof(DialogStep.Title), nameof(DialogStep.Message), true)]
        [TestCase(nameof(DialogStep.Buttons), nameof(DialogStep.Message), false)]
        [TestCase(nameof(DialogStep.Message), nameof(DialogStep.Buttons), false)]
        [TestCase(nameof(DialogStep.Title), nameof(DialogStep.Buttons), false)]
        [TestCase(nameof(DialogStep.Title), nameof(DialogStep.UseTimeout), false)]
        [TestCase(nameof(DialogStep.UseTimeout), nameof(DialogStep.Buttons), false)]
        [TestCase(nameof(DialogStep.Timeout), nameof(DialogStep.Message), false)]
        [TestCase(nameof(DialogStep.Message), nameof(DialogStep.Timeout), false)]
        public void TestMergeBadParameters(string paramA, string paramB, bool canMerge)
        {
            var plan = new TestPlan();
            var dialog = new DialogStep();
            plan.Steps.Add(dialog);
            var a = AnnotationCollection.Annotate(dialog);
            var menuInterface = new MenuTestUserInterface();
            var currentInterface = UserInput.Interface as IUserInputInterface;
            UserInput.SetInterface(menuInterface);
            try
            {
                menuInterface.SelectName = "a";
                menuInterface.SelectedMode = MenuTestUserInterface.Mode.Create | MenuTestUserInterface.Mode.TestPlan;
                a.GetMember(paramA).GetIcon(IconNames.Parameterize).Get<IMethodAnnotation>().Invoke();
                if (canMerge)
                {
                    menuInterface.SelectedMode = MenuTestUserInterface.Mode.Merge | MenuTestUserInterface.Mode.TestPlan;                    
                }
                else
                {
                    menuInterface.SelectedMode = MenuTestUserInterface.Mode.Merge |
                                                 MenuTestUserInterface.Mode.TestPlan | MenuTestUserInterface.Mode.Error;
                    menuInterface.ErrorString = "Cannot merge properties of this kind.";
                }
                a.GetMember(paramB).GetIcon(IconNames.Parameterize).Get<IMethodAnnotation>().Invoke();
            }
            finally
            {
                UserInput.SetInterface(currentInterface);    
            }

        }
    }
}
