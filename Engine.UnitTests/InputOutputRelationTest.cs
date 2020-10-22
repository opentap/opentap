using System;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class InputOutputRelationTest
    {
        [Test]
        public void TestBasicInputOutputRelation()
        {
            
            var delay1 = new DelayStep();
            var delay2 = new DelayStep();
            {
                var member = TypeData.GetTypeData(delay1).GetMember(nameof(DelayStep.DelaySecs));
                InputOutputRelation.Assign(delay2, member, delay1, member);
            }
            {
                double testValue = 10;
                delay1.DelaySecs = testValue;
                delay2.DelaySecs = 0;
                InputOutputRelation.UpdateInputs(delay2);
                Assert.AreEqual(delay1.DelaySecs, delay2.DelaySecs);
                Assert.AreEqual(testValue, delay1.DelaySecs);
            }
            {
                var member = TypeData.GetTypeData(delay1).GetMember(nameof(DelayStep.DelaySecs));
                InputOutputRelation.Unassign(delay2, member, delay1, member);
            }
            {
                double testValue = 10;
                delay1.DelaySecs = testValue;
                delay2.DelaySecs = 0;
                InputOutputRelation.UpdateInputs(delay2);
                Assert.AreNotEqual(delay1.DelaySecs, delay2.DelaySecs);
                Assert.AreEqual(testValue, delay1.DelaySecs);
            }
        }

        // The output is assigned to the input.
        public class OutputInput : TestStep
        {
            [Output]
            public double Output { get; set; }
            
            public double Input { get; set; }
            
            public bool CheckExpectedInput { get; set; }
            public double ExpectedInput { get; set; }
            public override void Run()
            {
                if(CheckExpectedInput && ExpectedInput != Input)
                    throw new Exception("Input has unexpected value");
                UpgradeVerdict(Verdict.Pass);
                Output = Input;
            }
        }

        
        [Test]
        public void TestInputOutputRelationsInTestPlan()
        {

            var step1 = new OutputInput {Output = 5, Input = 5, Name = "Step 1"};
            var step2 = new OutputInput {ExpectedInput = 5, CheckExpectedInput = true, Name = "Step 2"};
            var step3 = new OutputInput {ExpectedInput = 5, CheckExpectedInput = true, Name = "Step 3"};
            var plan = new TestPlan();
            plan.ChildTestSteps.AddRange(new []{step1, step2, step3});
            {
                var outputMember = TypeData.GetTypeData(step1).GetMember(nameof(OutputInput.Output));
                var inputMember = TypeData.GetTypeData(step1).GetMember(nameof(OutputInput.Input));
                InputOutputRelation.Assign(step2, inputMember, step1, outputMember );
                InputOutputRelation.Assign(step3, inputMember, step2, outputMember);
                
                Assert.IsTrue(InputOutputRelation.IsOutput(step1, outputMember));
                Assert.IsFalse(InputOutputRelation.IsInput(step1, outputMember));
                Assert.IsFalse(InputOutputRelation.IsOutput(step1, inputMember));
                Assert.IsFalse(InputOutputRelation.IsInput(step1, inputMember));

                Assert.IsTrue(InputOutputRelation.IsInput(step2, inputMember));
                Assert.IsTrue(InputOutputRelation.IsOutput(step2, outputMember));
                Assert.IsTrue(InputOutputRelation.IsInput(step3, inputMember));
                Assert.IsFalse(InputOutputRelation.IsOutput(step3, outputMember));
            }

            {
                var run = plan.Execute();
                Assert.AreEqual(Verdict.Pass, run.Verdict);
            }
        }

        [Test]
        public void TestSerializingConnectionsBetweenSteps()
        {
            var step1 = new OutputInput {Output = 5, Input = 5, Name = "Step 1"};
            var step2 = new OutputInput {ExpectedInput = 5, CheckExpectedInput = true, Name = "Step 2"};
            var step3 = new OutputInput {ExpectedInput = 5, CheckExpectedInput = true, Name = "Step 3"};
            var plan = new TestPlan();
            plan.ChildTestSteps.AddRange(new []{step1, step2, step3});
            var outputMember = TypeData.GetTypeData(step1).GetMember(nameof(OutputInput.Output));
            var inputMember = TypeData.GetTypeData(step1).GetMember(nameof(OutputInput.Input));
            InputOutputRelation.Assign(step2, inputMember, step1, outputMember);
            InputOutputRelation.Assign(step3, inputMember, step2, outputMember);

            var xml = plan.SerializeToString();

            var plan2 = Utils.DeserializeFromString<TestPlan>(xml);

            step1 = (OutputInput)plan2.Steps[0];
            step2 = (OutputInput)plan2.Steps[1];
            step3 = (OutputInput)plan2.Steps[2];
            Assert.IsTrue(InputOutputRelation.IsOutput(step1, outputMember));
            Assert.IsFalse(InputOutputRelation.IsInput(step1, outputMember));
            Assert.IsFalse(InputOutputRelation.IsOutput(step1, inputMember));
            Assert.IsFalse(InputOutputRelation.IsInput(step1, inputMember));

            Assert.IsTrue(InputOutputRelation.IsInput(step2, inputMember));
            Assert.IsTrue(InputOutputRelation.IsOutput(step2, outputMember));
            Assert.IsTrue(InputOutputRelation.IsInput(step3, inputMember));
            Assert.IsFalse(InputOutputRelation.IsOutput(step3, outputMember));
        }

        class SelectAssignmentFromAnnotationMenu : IUserInputInterface, IUserInterface
        {
            public bool WasInvoked;
            public string SelectName { get; set; }
            public void RequestUserInput(object dataObject, TimeSpan Timeout, bool modal)
            {
                var datas = AnnotationCollection.Annotate(dataObject);
                var selectedName = datas.GetMember("Output");
                var avail = selectedName.Get<IAvailableValuesAnnotationProxy>();
                avail.SelectedValue = avail.AvailableValues.First(x => (x.Source.ToString().Contains(SelectName)));
                
                var response = datas.GetMember("Response");
                var availRespons = response.Get<IAvailableValuesAnnotationProxy>();
                availRespons.SelectedValue =
                    availRespons.AvailableValues.FirstOrDefault(x => x.Source.ToString().Contains("Cancel") == false);
                
                datas.Write();
                
                WasInvoked = true;
            }

            public void NotifyChanged(object obj, string property) { }
        }

        
        [Test]
        public void TestMenuAnnotationForInputOutputRelations()
        {
            var userInput = UserInput.GetInterface();
            var request = new SelectAssignmentFromAnnotationMenu();
            UserInput.SetInterface(request);
            try
            {
                var step1 = new OutputInput {Output = 5, Input = 5, Name = "Step 1"};
                var step2 = new OutputInput {ExpectedInput = 5, CheckExpectedInput = true, Name = "Step 2"};
                var step3 = new OutputInput {ExpectedInput = 5, CheckExpectedInput = true, Name = "Step 3"};
                var plan = new TestPlan();
                plan.ChildTestSteps.AddRange(new[] {step1, step2, step3});

                var menu = AnnotationCollection.Annotate(step2).GetMember(nameof(OutputInput.Input))
                    .Get<MenuAnnotation>();
                request.SelectName = "Step 1";
                menu.MenuItems.First(x => x.Get<IconAnnotationAttribute>().IconName == IconNames.AssignOutput).Get<IMethodAnnotation>().Invoke();
                request.SelectName = "Step 2";
                menu = AnnotationCollection.Annotate(step3).GetMember(nameof(OutputInput.Input))
                    .Get<MenuAnnotation>();
                menu.MenuItems.First(x => x.Get<IconAnnotationAttribute>().IconName == IconNames.AssignOutput).Get<IMethodAnnotation>().Invoke();

                var run = plan.Execute();
                Assert.AreEqual(Verdict.Pass, run.Verdict);
                Assert.IsTrue(request.WasInvoked);
            }
            finally
            {
                UserInput.SetInterface(userInput);
            }
        }
        
        [Test]
        public void TestMenuAnnotationForInputOutputRelationsMultiSelect()
        {
            var userInput = UserInput.GetInterface();
            var request = new SelectAssignmentFromAnnotationMenu();
            UserInput.SetInterface(request);
            try
            {
                var seq1 = new SequenceStep();
                var seq2 = new SequenceStep();
                var step1 = new OutputInput {Output = 5, Input = 5, Name = "Step 1"};
                var step2 = new OutputInput {ExpectedInput = 5, CheckExpectedInput = true, Name = "Step 2"};
                seq1.ChildTestSteps.Add(step2);
                var step3 = new OutputInput {ExpectedInput = 5, CheckExpectedInput = true, Name = "Step 3"};
                seq2.ChildTestSteps.Add(step3);
                var plan = new TestPlan();
                plan.ChildTestSteps.AddRange(new ITestStep[] {step1, seq1, seq2});

                var menu = AnnotationCollection.Annotate(new []{step2, step3}).GetMember(nameof(OutputInput.Input))
                    .Get<MenuAnnotation>();
                request.SelectName = "Step 1";
                menu.MenuItems.First(x => x.Get<IconAnnotationAttribute>().IconName == IconNames.AssignOutput).Get<IMethodAnnotation>().Invoke();

                var run = plan.Execute();
                Assert.AreEqual(Verdict.Pass, run.Verdict);
                Assert.IsTrue(request.WasInvoked);
            }
            finally
            {
                UserInput.SetInterface(userInput);
            }
        }
        

        [Test]
        public void InputSweepTest()
        {
            var plan = new TestPlan();
            var sweep = new SweepLoop();
            plan.ChildTestSteps.Add(sweep);
            var menu = AnnotationCollection.Annotate(sweep).GetMember(nameof(SweepLoop.SweepParameters)).Get<MenuAnnotation>();
            var member = menu.MenuItems.FirstOrDefault(x => x.Get<IconAnnotationAttribute>().IconName == IconNames.AssignOutput);
            member.Get<IMethodAnnotation>().Invoke(); // this previously failed with an null reference exception.
        }

        [Test]
        public void TestAutoRemoveInputOutputRelation()
        {

            var plan = new TestPlan();
            var delay1 = new DelayStep();
            var delay2 = new DelayStep();
            plan.ChildTestSteps.Add(delay1);
            plan.ChildTestSteps.Add(delay2);
            {
                var member = TypeData.GetTypeData(delay1).GetMember(nameof(DelayStep.DelaySecs));
                InputOutputRelation.Assign(delay2, member, delay1, member);
                Assert.IsTrue(InputOutputRelation.IsInput(delay2, member));
                Assert.IsTrue(InputOutputRelation.IsOutput(delay1, member));
            }
            plan.ChildTestSteps.Remove(delay2);
            {
                var member = TypeData.GetTypeData(delay1).GetMember(nameof(DelayStep.DelaySecs));
                Assert.IsFalse(InputOutputRelation.IsInput(delay2, member));
                Assert.IsFalse(InputOutputRelation.IsOutput(delay1, member));
            }
        }

        public class ReadOnlyMemberOutput : TestStep
        {
            [Output]
            public double X { get; private set; }
            
            [XmlIgnore]
            [Browsable(true)]
            public double Y { get; set; }
            public override void Run()
            {
                
            }
        }
        
        [Test]
        public void TestSerializeReadOnlyMemberInputOutput()
        {
            var plan = new TestPlan();
            var a = new ReadOnlyMemberOutput();
            var b = new ReadOnlyMemberOutput();
            plan.ChildTestSteps.Add(a);
            plan.ChildTestSteps.Add(b);
            var readonlyMember = TypeData.GetTypeData(a).GetMember(nameof(a.X));
            var writableMember = TypeData.GetTypeData(a).GetMember(nameof(a.Y));
            InputOutputRelation.Assign(b, writableMember, a, readonlyMember);
            Assert.IsTrue(InputOutputRelation.IsOutput(a, readonlyMember));
            Assert.IsTrue(InputOutputRelation.IsInput(b, writableMember));

            var xml = plan.SerializeToString();
            plan = Utils.DeserializeFromString<TestPlan>(xml);
            a = (ReadOnlyMemberOutput)plan.ChildTestSteps[0];
            b = (ReadOnlyMemberOutput)plan.ChildTestSteps[1];
            Assert.IsTrue(InputOutputRelation.IsOutput(a, readonlyMember));
            Assert.IsTrue(InputOutputRelation.IsInput(b, writableMember));
        }

        [Test]
        public void TestMultipleOutputFromTheSameProperty()
        {
            var a = new OutputInput();
            var b = new OutputInput();
            var inputMember = TypeData.GetTypeData(a).GetMember(nameof(a.Input));
            var outputMember= TypeData.GetTypeData(b).GetMember(nameof(b.Output));
            var inputMember2= TypeData.GetTypeData(a).GetMember(nameof(a.ExpectedInput)); 
            InputOutputRelation.Assign(a, inputMember, b, outputMember);
            InputOutputRelation.Assign(a, inputMember2, b, outputMember);

            Assert.IsTrue(InputOutputRelation.IsInput(a, inputMember));
            Assert.IsTrue(InputOutputRelation.IsInput(a, inputMember2));
            Assert.Throws<ArgumentException>(() => InputOutputRelation.Assign(a, inputMember, b, outputMember));
            Assert.Throws<ArgumentException>(() => InputOutputRelation.Assign(a, inputMember2, b, outputMember));
        }
    }
}