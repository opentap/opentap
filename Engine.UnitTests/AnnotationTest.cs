using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using NUnit.Framework;
using OpenTap.Engine.UnitTests;
using OpenTap.Engine.UnitTests.TestTestSteps;
using OpenTap.EngineUnitTestUtils;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    public class AnnotationTest
    {
        public class ClassWithEnabled
        {
            public Enabled<string> X { get; set; } = new Enabled<string>();
        }

        [Test]
        public void TestEnabledAnnotation()
        {
            var obj = new ClassWithEnabled();
            obj.X.Value = "123";
            var a = AnnotationCollection.Annotate(obj);
            var strAnnotation = a.GetMember(nameof(obj.X)).Get<IStringValueAnnotation>();
            var str1 = strAnnotation.Value;
            obj.X.IsEnabled = true;
            a.Read();
            var str2 = strAnnotation.Value;

            strAnnotation.Value = str1;
            Assert.IsTrue(obj.X.IsEnabled);
            a.Write();
            Assert.IsFalse(obj.X.IsEnabled);
            strAnnotation.Value = str2;
            a.Write();
            Assert.IsTrue(obj.X.IsEnabled);

        }
        
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
        public void TestStepDuplicateIdWarning()
        {
            using var session = Session.Create(SessionOptions.OverlayComponentSettings);
            var path = Path.GetTempFileName();
            try
            {
                // The two delay steps have identical IDs -- verify we get a warning while loading this plan
                var testplan = @"<?xml version=""1.0"" encoding=""utf-8""?>
<TestPlan type=""OpenTap.TestPlan"" Locked=""false"">
  <Steps>
		<TestStep type=""OpenTap.Plugins.BasicSteps.DelayStep"" Id=""419bfb67-ed07-4b7c-a653-b9504372fe4a"">
			<Enabled>true</Enabled>
			<Name>Delay 1</Name>
		</TestStep>
    <TestStep type=""OpenTap.Plugins.BasicSteps.DelayStep"" Id=""419bfb67-ed07-4b7c-a653-b9504372fe4a"">
      <Enabled>true</Enabled>
      <Name>Delay 2</Name>
    </TestStep>
  </Steps>
</TestPlan>
";
                File.WriteAllText(path, testplan);
                
                {   // Verify loading generates no warnings or errors
                    var listener = new TestTraceListener();
                    Log.AddListener(listener);
                    var tp = TestPlan.Load(path);
                    Log.RemoveListener(listener);
                    
                    Assert.That(listener.ErrorMessage.Count, Is.EqualTo(0));
                    Assert.That(listener.WarningMessage.Any(warning => warning == "Duplicate test step ID found. The duplicate ID has been changed for step 'Delay 2'."));
                }

            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }

        }
        [Test]
        public void TestTestPlanReferenceDuplicates()
        {
            using var session = Session.Create(SessionOptions.OverlayComponentSettings);
            var tempfiles = new[]
            {
                Path.GetTempFileName(),
                Path.GetTempFileName(),
                Path.GetTempFileName(),
            };

            try
            {
                {   // Create test plans
                    new TestPlan() { ChildTestSteps = { new DelayStep(), new DelayStep() } }.Save((tempfiles[0]));
                    new TestPlan()
                    {
                        ChildTestSteps =
                        {
                            new TestPlanReference() { Filepath = new MacroString() { Text = tempfiles[0] } },
                            new TestPlanReference() { Filepath = new MacroString() { Text = tempfiles[0] } },
                        }
                    }.Save(tempfiles[1]);
                    new TestPlan()
                    {
                        ChildTestSteps =
                        {
                            new TestPlanReference() { Filepath = new MacroString() { Text = tempfiles[1] } },
                            new TestPlanReference() { Filepath = new MacroString() { Text = tempfiles[1] } },
                        }
                    }.Save(tempfiles[2]);
                }

                {   // Verify loading generates no warnings or errors
                    var listener = new TestTraceListener();
                    Log.AddListener(listener);
                    var tp = TestPlan.Load(tempfiles[2]);
                    ((TestPlanReference)tp.ChildTestSteps[0]).LoadTestPlan();
                    ((TestPlanReference)tp.ChildTestSteps[1]).LoadTestPlan();
                    Log.RemoveListener(listener);
                    
                    Assert.That(listener.ErrorMessage.Count, Is.EqualTo(0));
                    Assert.That(listener.WarningMessage.Count, Is.EqualTo(0));
                }
            }
            finally
            {
                foreach (var tempfile in tempfiles)
                    if (File.Exists(tempfile))
                        File.Delete(tempfile);
            }
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
                {
                    var annotation = AnnotationCollection.Annotate(tpr);
                    var loadTestPlanMem = annotation.GetMember("LoadTestPlan");
                    var enabled = loadTestPlanMem.Get<IEnabledAnnotation>();
                    Assert.IsFalse(enabled.IsEnabled);
                    
                    
                    var filePathMem = annotation.GetMember("Filepath");
                    var fpEnabled = filePathMem.Get<IEnabledAnnotation>();
                    Assert.IsTrue(fpEnabled?.IsEnabled ?? true);
                    var fpString = filePathMem.Get<IStringValueAnnotation>();
                    fpString.Value = planname;
                    annotation.Write();
                    annotation.Read();
                    Assert.IsTrue(enabled.IsEnabled);


                }


                tpr.Filepath.Text = planname;
                tpr.LoadTestPlan();
                {
                    var annotation = AnnotationCollection.Annotate(tpr);
                    var delaymem = annotation.GetMember("Time Delay");
                    Assert.IsNotNull(delaymem);
                }
            }
            finally
            {
                File.Delete(planname);
            }

        }

        [Test]
        public void ArrayStepNameAnnotationTest()
        {
            var step1 = new DelayStep() { Name = "A" };
            var step2 = new DelayStep() { Name = "A" };
            var step3 = new DelayStep() { Name = "A" };
            var steps = new ITestStep[]
            {
                step1, step2, step3
            };
            
            Assert.AreEqual(GetValue(steps), "A");
            step2.Name = "B";
            Assert.IsNull(GetValue(steps));

            static string GetValue(ITestStep[] steps)
            {
                var annotations = AnnotationCollection.Annotate(steps);
                var membersAnnotation = annotations.Get<IMembersAnnotation>();
                var memberAnnotation = membersAnnotation.Members.First(m => m.Name == "Step Name");
                var stepNameAnnotation = memberAnnotation.Get<IStringReadOnlyValueAnnotation>();
                Assert.DoesNotThrow(() => _ = stepNameAnnotation.Value);
                return stepNameAnnotation.Value;
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

        public class StepWithMacroString : TestStep
        {
            public string A { get; set; } = "Hello";
            public MacroString MacroString { get; set; }
            public override void Run()
            {
            }
        }

        [Test]
        public void TestMacroStringContext()
        {
            var plan = new TestPlan();
            var step = new StepWithMacroString();
            plan.ChildTestSteps.Add(step);
            step.MacroString = new MacroString(step);
            Assert.IsTrue(step.MacroString.Context == plan.ChildTestSteps.First(), "MacroString Context is set.");

            plan = new TapSerializer().Clone(plan) as TestPlan;
            step = plan.ChildTestSteps.First() as StepWithMacroString;
            
            Assert.IsTrue(step.MacroString.Context == plan.ChildTestSteps.First(), "MacroString Context is set.");
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
            public class NestedObject
            {
                public bool State { get; set; }
                public IEnumerable<int> Avail => Enumerable.Range(State ? 5 : 0, 5);
                [AvailableValues(nameof(Avail))]
                public int SelectedInt { get; set; }
            }
            
            public List<string> List { get; set; } = new List<string>{"A", "B"};
            
            [AvailableValues(nameof(List))]
            public string Selected { get; set; }
            
            public List<NestedObject> List2 { get; set; } = new List<NestedObject>{};

            public override void Run()
            {
                
            }
        }
        
        [Test]
        public void ListOfObjectAnnotation()
        {
            var obj = new ClassWithListOfString();
            var a = AnnotationCollection.Annotate(obj);
            var member = a.GetMember(nameof(ClassWithListOfString.List2));
            var col = member.Get<ICollectionAnnotation>();
            var newelem = col.NewElement();
            col.AnnotatedElements = new[] { newelem };
            
            a.Write();
            a.Read();
            
            // if we take the first element here, then previously updates would be lost after the first read. 
            // - at least if reading from the point of view of an available values proxy.
            newelem = col.AnnotatedElements.FirstOrDefault();
            newelem.GetMember("State").Get<IStringValueAnnotation>().Value = "true";
            a.Write();
            a.Read();
            var selected = newelem.GetMember("SelectedInt").Get<IAvailableValuesAnnotationProxy>().SelectedValue; 
            var vals = newelem.GetMember("SelectedInt").Get<IAvailableValuesAnnotationProxy>().AvailableValues;
            // verify that the available values corresponds to NestedObject.State = true;
            Assert.IsTrue(vals.Select(x => x.Source).SequenceEqual(obj.List2[0].Avail.Cast<object>()));
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
        public void TestReparameterize()
        {
            var testplan = new TestPlan();
            var seq = new SequenceStep();
            var delay = new DelayStep();
            seq.ChildTestSteps.Add(delay);
            testplan.ChildTestSteps.Add(seq);
            seq.ChildTestSteps.Add(new VerdictStep() { Verdict = Verdict.Pass });

            var timeDelay = AnnotationCollection.Annotate(delay).GetMember("DelaySecs");
            var menu = timeDelay.Get<MenuAnnotation>();
            var icons = menu.MenuItems.ToLookup(x => x.Get<IIconAnnotation>()?.IconName ?? "");

            var parameterize = icons[IconNames.ParameterizeOnParent].First();
            var unparameterize = icons[IconNames.Unparameterize].First();

            void invoke(AnnotationCollection a)
            {
                a.Get<IMethodAnnotation>().Invoke();
            }
            
            void run()
            {
                var planRun = testplan.Execute();
                Assert.AreEqual(Verdict.Pass, planRun.Verdict);
            }

            { // Verify nothing breaks when parameterizing, unparameterizing, and reparameterizing
                run();
                invoke(parameterize);
                run();
                invoke(unparameterize);
                run();
                invoke(parameterize);
                run();
                invoke(unparameterize);
                run();
            }
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
        public void MenuAnnotationTest4()
        {
            var currentUserInterface = UserInput.Interface;
            var menuInterface = new MenuTestUserInterface();
            UserInput.SetInterface(menuInterface);
            try
            {
                var plan = new TestPlan();
                var sequence = new SequenceStep();
                var step = new DelayStep();
                plan.Steps.Add(sequence);
                sequence.ChildTestSteps.Add(step);

                {
                    var p1 = TypeData.GetTypeData(step).GetMember(nameof(step.DelaySecs)).Parameterize(sequence, step, "A");
                    p1.Parameterize(plan, sequence, p1.Name);

                    var editParameterIcon = AnnotationCollection.Annotate(sequence).GetMember(p1.Name).Get<MenuAnnotation>().MenuItems
                        .First(x => x.Get<IconAnnotationAttribute>()?.IconName == IconNames.EditParameter);
                    
                    menuInterface.SelectedMode = MenuTestUserInterface.Mode.TestPlan | MenuTestUserInterface.Mode.Error;
                    menuInterface.SelectName = p1.Name;
                    
                    editParameterIcon.Get<IMethodAnnotation>().Invoke();

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

        [Test]
        public void TestMergedInputVerdict([Values(true, false)]bool availableProxy)
        {
            var dialog = new DialogStep();
            var if1 = new IfStep();
            var if2 = new IfStep();
            var if3 = new IfStep();
            var plan = new TestPlan()
            {
                ChildTestSteps =
                {
                    dialog, if1, if2, if3
                }
            };

            ITestStep[] merged = { if1, if2, if3 };

            void setInputVerdict(AnnotationCollection a, ITestStep targetStep)
            {
                var member = a.GetMember(nameof(if1.InputVerdict));
                if (availableProxy)
                {
                    var avail = member.Get<IAvailableValuesAnnotationProxy>();
                    var sel = avail.AvailableValues.First(av =>
                        av.GetMember("Step").Get<IObjectValueAnnotation>().Value == targetStep);
                    avail.SelectedValue = sel;
                }
                else
                {
                    var input = new Input<Verdict>()
                    {
                        Property = TypeData.FromType(typeof(DialogStep)).GetMember(nameof(dialog.Verdict)),
                        Step = targetStep
                    };
                    member.Get<IObjectValueAnnotation>().Value = input;
                }

                a.Write();
                a.Read();
            }
            
            { // Test multiselect editing
                var a = AnnotationCollection.Annotate(merged);
                setInputVerdict(a, dialog);
                Assert.AreSame(dialog, if1.InputVerdict.Step);
                Assert.AreSame(dialog, if2.InputVerdict.Step);
                Assert.AreSame(dialog, if3.InputVerdict.Step);
            }
            
            { // Unset all
                var a = AnnotationCollection.Annotate(merged);
                setInputVerdict(a, null);
                Assert.AreSame(null, if1.InputVerdict.Step);
                Assert.AreSame(null, if2.InputVerdict.Step);
                Assert.AreSame(null, if3.InputVerdict.Step);
            }

            { // Set if1 independent of if2
                var a = AnnotationCollection.Annotate(if1);
                setInputVerdict(a, if2);
                Assert.AreSame(if2, if1.InputVerdict.Step);
                Assert.AreSame(null, if2.InputVerdict.Step);
                Assert.AreSame(null, if3.InputVerdict.Step);
            }

            { // Set if2 independent of if1
                var a = AnnotationCollection.Annotate(if2);
                setInputVerdict(a, if1);
                Assert.AreSame(if2, if1.InputVerdict.Step);
                Assert.AreSame(if1, if2.InputVerdict.Step);
                Assert.AreSame(null, if3.InputVerdict.Step);
            }
            
            { // Set all back to dialog
                var a = AnnotationCollection.Annotate(merged);
                setInputVerdict(a, dialog);
                Assert.AreSame(dialog, if1.InputVerdict.Step);
                Assert.AreSame(dialog, if2.InputVerdict.Step);
                Assert.AreSame(dialog, if3.InputVerdict.Step);
            }
            
            { // Unset all
                var a = AnnotationCollection.Annotate(merged);
                setInputVerdict(a, null);
                Assert.AreSame(null, if1.InputVerdict.Step);
                Assert.AreSame(null, if2.InputVerdict.Step);
                Assert.AreSame(null, if3.InputVerdict.Step);
            }
        }

        [Test]
        public void TestMergedInputVerdict2([Values(true, false)] bool availableProxy)
        {
            var repeat1 = new RepeatStep() { Action = RepeatStep.RepeatStepAction.While };
            var repeat2 = new RepeatStep() { Action = RepeatStep.RepeatStepAction.While };
            var plan = new TestPlan()
            {
                ChildTestSteps =
                {
                    repeat1, repeat2
                }
            };
            
            ITestStep[] merged = { repeat1, repeat2 };

            void setInputVerdict(AnnotationCollection a, ITestStep targetStep)
            {
                var member = a.GetMember(nameof(repeat1.TargetStep));
                if (availableProxy)
                {
                    var avail = member.Get<IAvailableValuesAnnotationProxy>();
                    var sel = avail.AvailableValues.FirstOrDefault(av =>
                        av.Get<IObjectValueAnnotation>().Value == targetStep);
                    avail.SelectedValue = sel;
                }
                else
                {
                    member.Get<IObjectValueAnnotation>().Value = targetStep;
                }

                a.Write();
                a.Read();
            } 
            
            { // Test multiselect editing
                var a = AnnotationCollection.Annotate(merged);
                setInputVerdict(a, repeat1);
                Assert.AreSame(repeat1, repeat1.TargetStep);
                Assert.AreSame(repeat1, repeat1.TargetStep);
            }

            { // Set repeat1 independent of repeat2
                var a = AnnotationCollection.Annotate(repeat1);
                setInputVerdict(a, repeat2);
                Assert.AreSame(repeat2, repeat1.TargetStep);
                Assert.AreSame(repeat1, repeat2.TargetStep);
            }

            { // Set repeat2 independent of repeat1
                var a = AnnotationCollection.Annotate(repeat2);
                setInputVerdict(a, repeat1);
                Assert.AreSame(repeat2, repeat1.TargetStep);
                Assert.AreSame(repeat1, repeat2.TargetStep);
            }
            
            { // Set all back to repeat1
                var a = AnnotationCollection.Annotate(merged);
                setInputVerdict(a, repeat1);
                Assert.AreSame(repeat1, repeat1.TargetStep);
                Assert.AreSame(repeat1, repeat2.TargetStep);
            }
        }

        [Test]
        public void ParameterizeOnParentTest()
        {
            var plan = new TestPlan();
            var seqStep = new SequenceStep();
            plan.ChildTestSteps.Add(seqStep);
            var delayStep = new DelayStep();
            seqStep.ChildTestSteps.Add(delayStep);

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
            delayMember.Parameterize(seqStep, delayStep, "Delay");

            // Check can parameterize
            member = AnnotationCollection.Annotate(delayStep).GetMember(nameof(DelayStep.DelaySecs));
            menu = member.Get<MenuAnnotation>();
            menuItems = menu.MenuItems;
            icons = menuItems.ToLookup(item => item.Get<IIconAnnotation>()?.IconName ?? "");
            parameterizeIcon = icons[IconNames.Parameterize].First();
            Assert.IsFalse(parameterizeIcon.Get<IAccessAnnotation>().IsVisible);
            parameterizeIcon = icons[IconNames.Unparameterize].First();
            Assert.IsTrue(parameterizeIcon.Get<IAccessAnnotation>().IsVisible);
            
            var paramIconAnnotation = member.Get<ISettingReferenceIconAnnotation>();
            Assert.AreEqual(seqStep.Id, paramIconAnnotation.TestStepReference);
            Assert.AreEqual("Delay", paramIconAnnotation.MemberName);

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

        public class InstrumentStep1 : TestStep
        {
            public Instrument Instrument { get; set; }
            public override void Run()
            {
            }
        }
        
        public class InstrumentStep2 : TestStep
        {
            public Instrument Instrument { get; set; }
            public override void Run()
            {
            }
        }

        [Test]
        public void MultiSelectUnparameterize()
        {
            var step1 = new InstrumentStep1();
            var step2 = new InstrumentStep2();
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(step1);
            plan.ChildTestSteps.Add(step2);

            {
                // 1. parameterize them
                var a = AnnotationCollection.Annotate(new ITestStep[]
                {
                    step1, step2
                });

                var member = a.GetMember(nameof(step1.Instrument));
                member.GetIcon(IconNames.ParameterizeOnTestPlan).Get<IMethodAnnotation>()?.Invoke();
            }

            // 2. check.
            Assert.IsNotEmpty(TypeData.GetTypeData(plan).GetMembers().OfType<IParameterMemberData>());
            
            {
                // 3. unparameterize them
                var a = AnnotationCollection.Annotate(new ITestStep[]
                {
                    step1, step2
                });

                var member = a.GetMember(nameof(step1.Instrument));
                member.GetIcon(IconNames.Unparameterize).Get<IMethodAnnotation>()?
                    .Invoke();
            }
            
            // 4. check.
            Assert.IsEmpty(TypeData.GetTypeData(plan).GetMembers().OfType<IParameterMemberData>());
        }
        
        
        [Test]
        public void MultiSelectList()
        {
            var step1 = new ClassWithListOfString();
            var step2 = new ClassWithListOfString();
            var a = AnnotationCollection.Annotate(new []{step1, step2});
            var lst = a.GetMember(nameof(step1.List));
            var valAnnotation = lst.Get<ICollectionAnnotation>();
            valAnnotation.AnnotatedElements = valAnnotation.AnnotatedElements.Append(valAnnotation.NewElement()).ToArray();
            a.Write();
            Assert.IsTrue(step1.List.Count == 3);
            Assert.IsTrue(step1.List.SequenceEqual(step2.List));
            Assert.AreNotSame(step1.List, step2.List);
        }
        
        [Test]
        public void FixNullList()
        {
            var step1 = new ClassWithListOfString { List = null };
            
            var a = AnnotationCollection.Annotate(step1);
            var lst = a.GetMember(nameof(step1.List));
            var seqAnnotation = lst.Get<ICollectionAnnotation>();
            var numbers = seqAnnotation.AnnotatedElements.ToArray();
            seqAnnotation.AnnotatedElements = numbers.Append(seqAnnotation.NewElement());
            a.Write();
            // step1.List should not be null as we have just added an element to it.
            Assert.IsNotNull(step1.List);
            Assert.AreEqual(1, step1.List.Count);
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
        
        [Display("Validation Error Step")]
        public class ValidationErrorTestStep : TestStep
        {
            public bool HasValidationErrors { get; set; }
            public ValidationErrorTestStep()
            {
                Rules.Add(() => !HasValidationErrors, "Validation errors", nameof(ValidationErrorTestStep));
            }
            public override void Run()
            {
            }
        }

        [Test]
        public void SweepParametersValidationTest()
        {
            var plan = new TestPlan();
            var sweep = new SweepParameterStep();
            var errorStep = new DelayStep() {Name = "errorStep"};
            var noErrorStep = new DelayStep() {Name = "noErrorStep"};
            var validationErrorStep = new ValidationErrorTestStep();

            plan.ChildTestSteps.Add(sweep);
            sweep.ChildTestSteps.Add(errorStep);
            sweep.ChildTestSteps.Add(noErrorStep);
            sweep.ChildTestSteps.Add(validationErrorStep);

            TypeData.GetTypeData(errorStep).GetMember(nameof(errorStep.DelaySecs))
                .Parameterize(sweep, errorStep, nameof(errorStep.DelaySecs));
            TypeData.GetTypeData(validationErrorStep).GetMember(nameof(validationErrorStep.HasValidationErrors))
                .Parameterize(sweep, validationErrorStep, nameof(validationErrorStep.HasValidationErrors));

            void addRow(double delay, bool validationErrors)
            {
                sweep.SweepValues.Add(new SweepRow(sweep));
                var td = TypeData.GetTypeData(sweep.SweepValues[0]);
                var timeDelay = td.GetMember(nameof(errorStep.DelaySecs));
                var valErrors = td.GetMember(nameof(validationErrorStep.HasValidationErrors));

                timeDelay.SetValue(sweep.SweepValues.Last(), delay);
                valErrors.SetValue(sweep.SweepValues.Last(), validationErrors);
            }

            // First row has no errors
            addRow(1, false);
            // Second row has an invalid time delay
            addRow(-1, false);
            // Third row as validation errors
            addRow(1, true);
            // Fourth row has an invalid time delay and validation errors
            addRow(-1, true);

            void TestErrors()
            {

                var lines = sweep.Error.Split('\n');

                // Test errors outside of run

                {
                    // Test row 1
                    var row = lines.Where(l => l.Contains("Row 1"));
                    CollectionAssert.IsEmpty(row);
                }

                {
                    // Test row 2
                    var row = lines.Where(l => l.Contains("Row 2")).ToArray();
                    Assert.AreEqual(1, row.Length);
                    Assert.IsTrue(row.First().Contains($"Delay must be a positive value."),
                        "Row 2 should have an invalid DelaySecs value.");
                }

                {
                    // Test row 3
                    var row = lines.Where(l => l.Contains("Row 3")).ToArray();
                    Assert.AreEqual(1, row.Length);
                    Assert.IsTrue(row.Any(r => r.Contains("Validation Error Step - Validation errors")),
                        "Row 3 should have validation errors.");
                }

                {
                    // Test row 4
                    var row = lines.Where(l => l.Contains("Row 4")).ToArray();
                    Assert.AreEqual(2, row.Length);
                    var row1 = row.First();
                    Assert.IsTrue(row1.Contains($"Delay must be a positive value."),
                        "Row 4 should have an invalid DelaySecs value.");
                    var row2 = row.Last();
                    Assert.IsTrue(row2.Contains("Validation Error Step - Validation errors"),
                        "Row 4 should have validation errors.");
                }
            }
            TestErrors();
            
            var t = TapThread.Start(() => { plan.Execute(); });
            
            while (plan.IsRunning == false)
                TapThread.Sleep(10);
            
            // Validation should work while the test plan is running by returning the most recent validation error string
            TestErrors();


            while (plan.IsRunning)
                TapThread.Sleep(10);

            TestErrors();
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

        class BoolArrayClass
        {
            public List<bool> A { get; set; }= new List<bool>();
            public bool[] B { get; set; }= new bool[0];
            //public int[] C { get; set; }= new int[0];
            //public List<int> D { get; set; }= new List<int>();
            public List<object> E { get; set; }= new List<object>();
            public List<Verdict> F { get; set; }= new List<Verdict>();
            public Verdict[] G { get; set; }= new Verdict[0];
        }

        [Test]
        public void TestSetBoolArrayValues()
        {
            var obj = new BoolArrayClass() ;
            var a = AnnotationCollection.Annotate(obj);
            foreach (var mem in a.Get<IMembersAnnotation>().Members)
            {
                for (int i = 0; i < 5; i++)
                {
                    a.Read();
                    var col = mem.Get<ICollectionAnnotation>();
                    col.AnnotatedElements = col.AnnotatedElements.Append(col.NewElement());
                    mem.Write();
                    Assert.AreEqual(i + 1, col.AnnotatedElements.Count());
                }
            }
        }

        public class SuggestedValuesObject
        {
            public List<int> SuggestedValues { get; set; } = new List<int> {1, 2, 3};
            [SuggestedValues(nameof(SuggestedValues))]
            public int SelectedValue { get; set; }
            
            [AvailableValues(nameof(SuggestedValues))]
            public int SelectedValue2 { get; set; }
        }

        [Test]
        public void SuggestedAndAvailableValuesUpdateTest()
        {
            // an issue was discovered that when the list of suggested values is updated, without replacing it with
            // a new list instance the an issue occurs because the ISuggestedValueAnnotationProxy does some internal caching.
            var obj = new SuggestedValuesObject();
            var a = AnnotationCollection.Annotate(obj);
            var sv = a.GetMember(nameof(obj.SelectedValue)).Get<ISuggestedValuesAnnotationProxy>();
            var av = a.GetMember(nameof(obj.SelectedValue2)).Get<IAvailableValuesAnnotationProxy>();
            Assert.AreEqual(3, sv.SuggestedValues.Count());
            Assert.AreEqual(3, av.AvailableValues.Count());
            obj.SuggestedValues.Add(4);
            a.Read();
            Assert.AreEqual(4, sv.SuggestedValues.Count()); // Failed initially
            Assert.AreEqual(4, av.AvailableValues.Count());
        }

        public class InstrumentStep : TestStep
        {
            public IInstrument Instrument { get; set; }
            public override void Run()
            {
            }
        }

        [Test]
        public void TestSetStringResourceTest()
        {
            using (Session.Create(SessionOptions.OverlayComponentSettings))
            {
                var step = new InstrumentStep();
                var instrument = new DummyInstrument() { Name = "Instr" };
                InstrumentSettings.Current.Add(instrument);
                
                var a = AnnotationCollection.Annotate(step);
                var instr = a.GetMember(nameof(step.Instrument));
                var strval = instr.Get<IStringValueAnnotation>();
                var current = strval.Value;
                Assert.AreNotEqual(instrument.Name, current); // this should be null or some other default value.
                strval.Value = instrument.Name;
                a.Write();
                a.Read();
                var next = strval.Value;
                Assert.AreEqual(instrument, step.Instrument);
                Assert.AreEqual(instrument.Name, next);
            }
        }


        class ClassWithNumbers
        {
            [Unit("things")]
            public int[] Values { get; set; } = new int[] {1, 2, 3};
        }
        
        [Test]
        public void TestNumberSequenceAnnotation()
        {
            var obj = new ClassWithNumbers();
            var annotation = AnnotationCollection.Annotate(obj);
            var str = annotation.GetMember(nameof(obj.Values)).Get<IStringValueAnnotation>();
            var err = annotation.GetMember(nameof(obj.Values)).Get<IErrorAnnotation>();
            // this should not throw.
            str.Value = "asd";
            Assert.IsTrue(err.Errors.Count() == 1);
            str.Value = "1, 2, 3, 4";
            Assert.IsTrue(err.Errors.Count() == 0);
            annotation.Write();
            Assert.IsTrue(obj.Values.SequenceEqual(new int[] {1, 2, 3, 4}));
        }
        
        [Test]
        public void WriteMergedAnnotationTest()
        {
            var names = new[] { "name1", "name1", "name2" };
            var delaySteps = new DelayStep[3];
            for (int i = 0; i < 3; i++)
            {
                var delay = new DelayStep() { Name = names[i] };
                delaySteps[i] = delay;
            }

            var a = AnnotationCollection.Annotate(delaySteps);
            { // verify merge fails
                var mem = a.GetMember(nameof(DelayStep.Name));
                var name = mem.Get<MergedValueAnnotation>();
                Assert.AreEqual(null, name.Value);
            }
            { // Verify collection is not modified on write
                a.Write();
                CollectionAssert.AreEqual(names, delaySteps.Select(d => d.Name));   
            }
            { // Verify merge succeeds 
                delaySteps[2].Name = "name1";
                a.Read();   
                var mem = a.GetMember(nameof(DelayStep.Name));
                var name = mem.Get<MergedValueAnnotation>();
                Assert.AreEqual("name1", name.Value);
            }
        }

        public class DutAndInstrumentResource : Resource, IDut, IInstrument
        { 
        }
        
        public class DutAndInstrumentUser : TestStep
        {
            public IDut DUT { get; set; }
            public IInstrument Instrument { get; set; }
            public DutAndInstrumentResource Both { get; set; }
            public override void Run()
            {
            }
        }
        [Test]
        public void AvailableResourcesTest()
        {
            using var session = Session.Create(SessionOptions.OverlayComponentSettings);
            DutSettings.Current.Clear();
            InstrumentSettings.Current.Clear();
            
            var ins = new DutAndInstrumentResource() { Name = "The Instrument" };
            var dut = new DutAndInstrumentResource() { Name = "The DUT" };
            
            InstrumentSettings.Current.Add(ins);
            DutSettings.Current.Add(dut);

            var step = new DutAndInstrumentUser();
            var a = AnnotationCollection.Annotate(step);

            DutAndInstrumentResource[] getAvailable(string name) =>
                a.GetMember(name).Get<IAvailableValuesAnnotation>().AvailableValues.Cast<DutAndInstrumentResource>().ToArray();

            var availableInstruments = getAvailable(nameof(step.Instrument));
            var availableDuts = getAvailable(nameof(step.DUT));
            var availableBoth = getAvailable(nameof(step.Both)); 
            
            Assert.AreEqual(1, availableInstruments.Length);
            CollectionAssert.Contains(availableInstruments, ins);
            
            Assert.AreEqual(1, availableDuts.Length);
            CollectionAssert.Contains(availableDuts, dut);
            
            Assert.AreEqual(2, availableBoth.Length);
            CollectionAssert.Contains(availableBoth, ins);
            CollectionAssert.Contains(availableBoth, dut);
        }

        [Test]
        public void AvailableValuesUpdateTest()
        {
            var step = new AvailableValuesUpdateTest();
            var annotation = AnnotationCollection.Annotate(step);
            var a = annotation.GetMember(nameof(step.A));
            var b = annotation.GetMember(nameof(step.B));
            var a1 = a.Get<IAvailableValuesAnnotationProxy>();
            var b1 = b.Get<IAvailableValuesAnnotationProxy>();

            bool doTest()
            {
                // A available values is not allowed to contain the value of B and vice versa.
                var test2 = b1.AvailableValues.Select(x => x.Get<IObjectValueAnnotation>().Value).Contains(a1.SelectedValue.Get<IObjectValueAnnotation>().Value);
                var test1 = a1.AvailableValues.Select(x => x.Get<IObjectValueAnnotation>().Value).Contains(b1.SelectedValue.Get<IObjectValueAnnotation>().Value);
                var b1val = b1.SelectedValue.Get<IObjectValueAnnotation>().Value;
                var a1val = a1.SelectedValue.Get<IObjectValueAnnotation>().Value;
                
                if (Equals(b1val, a1val))
                    return false;
                return !test1 && !test2;
            }

            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(doTest());
                
                b1.SelectedValue = b1.AvailableValues.FirstOrDefault(x => !Equals(x.Get<IObjectValueAnnotation>().Value,
                    b1.SelectedValue.Get<IObjectValueAnnotation>().Value));
                
                annotation.Write();
                annotation.Read();
                Assert.IsTrue(doTest());
                a1.SelectedValue = a1.AvailableValues.FirstOrDefault(x => !Equals(x.Get<IObjectValueAnnotation>().Value, 
                    a1.SelectedValue.Get<IObjectValueAnnotation>().Value));
                
                annotation.Write();
                annotation.Read();
                Assert.IsTrue(doTest());
            }

            for (int i = 0; i < 5; i++)
            {
                var number = annotation.GetMember(nameof(step.FromIncreasingNumber));
                var number1 = number.Get<IAvailableValuesAnnotationProxy>();
                var numbers = number1.AvailableValues.Select(x => (int)x.Get<IObjectValueAnnotation>().Value).ToArray();
                Assert.AreEqual(step.IncreasingNumber, numbers[0]);
                var numberIncrease = annotation.GetMember(nameof(step.IncreaseNumber)).Get<IMethodAnnotation>();
                numberIncrease.Invoke();
                annotation.Read();
            }
        }

        public enum Overlapping
        {
            A = 0,
            X = 1,
            Z = 2, // there is one constraint, which is that the selected name must be the first one. Otherwise Overlapping.Z.ToString() => "Y"
            [Obsolete]
            [Browsable(false)]
            Y = 2,
            [Browsable(false)]
            [Obsolete]
            Y2 = 2,
            W = 3
        }

        class ClassWithOverlapping
        {
            public Overlapping Overlapping { get; set; } = Overlapping.X;
        }
        [Test]
        public void OverlappingEnumTest()
        {
            var o = new ClassWithOverlapping();
            var a = AnnotationCollection.Annotate(o);
            var a2 = a.GetMember(nameof(o.Overlapping));
            var available = a2.Get<IAvailableValuesAnnotation>().AvailableValues.Cast<Overlapping>();
            CollectionAssert.AreEqual(available, new[] { Overlapping.A, Overlapping.X, Overlapping.Z, Overlapping.W });

            var a3 = a2.Get<IAvailableValuesAnnotationProxy>();
            var strValues = a3.AvailableValues.Select(x => x.Get<IStringReadOnlyValueAnnotation>().Value).ToArray();
            CollectionAssert.AreEqual(strValues, new[] { "A", "X", "Z", "W" });
            a3.SelectedValue = a3.AvailableValues.Skip(2).FirstOrDefault();
            a.Write();
            a.Read();
            Assert.AreEqual(Overlapping.Z, o.Overlapping);
        }

        class EnabledThings
        {
            public double X { get; set; }
        }
        
        class MaybeEnabled : IEnabledAnnotation
        {
            public bool IsEnabled { get; set; }
        }
        
        public class TestAnnotator : IAnnotator
        {
            public double Priority => 0;
            public void Annotate(AnnotationCollection annotations)
            {
                // Disable 'X' of EnabledThings using multiple IEnabledAnnotations:
                var member = annotations.Get<IMemberAnnotation>()?.Member;
                if (member != null && member.Name == "X" && TypeData.FromType(typeof(EnabledThings)).GetMember("X") == member)
                {
                    annotations.Add(new MaybeEnabled(){IsEnabled = true});
                    annotations.Add(new MaybeEnabled(){IsEnabled = false});
                    annotations.Add(new MaybeEnabled(){IsEnabled = true});
                }
            }
        }

        [Test]
        public void TestMultipleEnabled()
        {
            var obj = new EnabledThings();
            var obj2 = new EnabledThings();
            var x2 = AnnotationCollection.Annotate(new[] { obj, obj2 }).GetMember("X");
            // after multi-selecting this should be disabled.
            // when this issue occured it was not.
            var enabled = x2.Get<IEnabledAnnotation>().IsEnabled;
            Assert.IsFalse(enabled);
        }

        public class AvailableValuesArrayUser
        {
            #region Test strings

            public List<string> AvailableStrings { get; set; } = new List<string>() { "A", "B", "C" };

            [AvailableValues(nameof(AvailableStrings))]
            public List<string> SelectedStringsList { get; set; } = new List<string>();

            [AvailableValues(nameof(AvailableStrings))]
            public string[] SelectedStringsArray { get; set; } = Array.Empty<string>();

            #endregion

            #region Test numbers

            public List<int> AvailableNumbers { get; set; } = new List<int>() { 7, 9, 13 };

            [AvailableValues(nameof(AvailableNumbers))]
            public List<int> SelectedNumbersList { get; set; } = new List<int>();

            [AvailableValues(nameof(AvailableNumbers))]
            public int[] SelectedNumbersArray { get; set; } = Array.Empty<int>();

            #endregion
        }

        [Test]
        public void TestAddToArrayWithAvailableValues()
        {   
            { // Test writing strings
                var av = new AvailableValuesArrayUser();
                var a = AnnotationCollection.Annotate(av);
                var selectedItems = a.GetMember(nameof(av.SelectedStringsArray));

                var availProxy = selectedItems.Get<IAvailableValuesAnnotationProxy>();
                var multiselect = selectedItems.Get<IMultiSelectAnnotationProxy>();

                {
                    multiselect.SelectedValues = availProxy.AvailableValues.Take(2);
                    a.Write();
                    a.Read();

                    Assert.AreEqual(2, av.SelectedStringsArray.Length);
                    Assert.AreEqual("A", av.SelectedStringsArray[0]);
                    Assert.AreEqual("B", av.SelectedStringsArray[1]);
                }

                {
                    multiselect.SelectedValues = availProxy.AvailableValues.Skip(2).Take(1);
                    a.Write();
                    a.Read();

                    Assert.AreEqual(1, av.SelectedStringsArray.Length);
                    Assert.AreEqual("C", av.SelectedStringsArray[0]);
                }
            }

            { // Test writing numbers
                var av = new AvailableValuesArrayUser();
                var a = AnnotationCollection.Annotate(av);
                var selectedItems = a.GetMember(nameof(av.SelectedNumbersArray));

                var availProxy = selectedItems.Get<IAvailableValuesAnnotationProxy>();
                var multiselect = selectedItems.Get<IMultiSelectAnnotationProxy>();

                {
                    multiselect.SelectedValues = availProxy.AvailableValues.Take(2);
                    a.Write();
                    a.Read();

                    Assert.AreEqual(2, av.SelectedNumbersArray.Length);
                    Assert.AreEqual(7, av.SelectedNumbersArray[0]);
                    Assert.AreEqual(9, av.SelectedNumbersArray[1]);
                }

                {
                    multiselect.SelectedValues = availProxy.AvailableValues.Skip(2).Take(1);
                    a.Write();
                    a.Read();

                    Assert.AreEqual(1, av.SelectedNumbersArray.Length);
                    Assert.AreEqual(13, av.SelectedNumbersArray[0]);
                }
            }
        }

        [Test]
        public void TestAddToListWithAvailableValues()
        {
            { // Test writing strings
                var av = new AvailableValuesArrayUser();

                var a = AnnotationCollection.Annotate(av);
                var selectedItems = a.GetMember(nameof(av.SelectedStringsList));

                var availProxy = selectedItems.Get<IAvailableValuesAnnotationProxy>();
                var multiselect = selectedItems.Get<IMultiSelectAnnotationProxy>();

                {
                    // Test writing two values
                    multiselect.SelectedValues = availProxy.AvailableValues.Take(2);
                    a.Write();
                    a.Read();

                    Assert.AreEqual(2, av.SelectedStringsList.Count);
                    Assert.AreEqual("A", av.SelectedStringsList[0]);
                    Assert.AreEqual("B", av.SelectedStringsList[1]);
                }

                {
                    multiselect.SelectedValues = availProxy.AvailableValues.Skip(2).Take(1);
                    a.Write();
                    a.Read();

                    Assert.AreEqual(1, av.SelectedStringsList.Count);
                    Assert.AreEqual("C", av.SelectedStringsList[0]);
                }
            }
            { // Test writing numbers
                var av = new AvailableValuesArrayUser();
                var a = AnnotationCollection.Annotate(av);
                var selectedItems = a.GetMember(nameof(av.SelectedNumbersList));

                var availProxy = selectedItems.Get<IAvailableValuesAnnotationProxy>();
                var multiselect = selectedItems.Get<IMultiSelectAnnotationProxy>();

                {
                    // Test writing strings
                    multiselect.SelectedValues = availProxy.AvailableValues.Take(2);
                    a.Write();
                    a.Read();

                    Assert.AreEqual(2, av.SelectedNumbersList.Count);
                    Assert.AreEqual(7, av.SelectedNumbersList[0]);
                    Assert.AreEqual(9, av.SelectedNumbersList[1]);
                }

                {
                    multiselect.SelectedValues = availProxy.AvailableValues.Skip(2).Take(1);
                    a.Write();
                    a.Read();

                    Assert.AreEqual(1, av.SelectedNumbersList.Count);
                    Assert.AreEqual(13, av.SelectedNumbersList[0]);
                }
            }
        }

        [AllowAnyChild]
        public class ReadOnlyDelays : TestStep
        {
            public ReadOnlyDelays()
            {
                for (int i = 0; i < 3; i++)
                {
                    // TestStep.IsReadOnly does not cause the settings to be read-only, but if the ChildTetSteps.IsReadOnly is set this is the case. 
                    ChildTestSteps.Add(new DelayStep() { IsReadOnly = true });
                }

                ChildTestSteps.IsReadOnly = true;
            }
            
            public override void Run()
            {
                
            }
        }

        [Test]
        public void TestReadOnlyStepSettings()
        {
            var step = new ReadOnlyDelays();
            var a = AnnotationCollection.Annotate(step.ChildTestSteps[0]);
            var member = a.GetMember(nameof(DelayStep.DelaySecs));
            Assert.IsTrue(member.GetAll<IEnabledAnnotation>().Any(x => x.IsEnabled == false));
        }

        public class DateTimeStep : TestStep
        {
            public DateTime MyDate { get; set; }
            public override void Run()
            {
                throw new NotImplementedException();
            }
        }

        [Test]
        public void TestTimeAnnotations()
        {
            var times = new DateTimeStep();
            var a = AnnotationCollection.Annotate(times);
            var members = a.Get<IMembersAnnotation>().Members.ToLookup(m => m.Name);

            var date = members[nameof(DateTimeStep.MyDate)].First();
            var dsv = date.Get<IStringValueAnnotation>();

            string[] GetErrors() => date.GetAll<IErrorAnnotation>().SelectMany(a => a.Errors).ToArray();

            Assert.AreEqual(default(DateTime), times.MyDate);
            StringAssert.StartsWith("0001-01-01", dsv.Value);
            times.MyDate = DateTime.Parse("1000-11-30", DateTimeFormatInfo.InvariantInfo);
            a.Read();
            StringAssert.StartsWith("1000-11-30", dsv.Value);

            dsv.Value = "1978-07-27";
            Assert.AreEqual(0, GetErrors().Count());
            a.Write();

            Assert.AreEqual(7, times.MyDate.Month);
            Assert.AreEqual(27, times.MyDate.Day);
            Assert.AreEqual(1978, times.MyDate.Year);

            dsv.Value = "garbage string";

            Assert.AreEqual(1, GetErrors().Count());

            dsv.Value = "0001-01-01";
            Assert.AreEqual(0, GetErrors().Count());
        }
    }
}
