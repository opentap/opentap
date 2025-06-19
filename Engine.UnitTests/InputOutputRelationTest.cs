using System;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using NUnit.Framework;
using OpenTap.Engine.UnitTests;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class InputOutputRelationTest
    {
        [Test]
        public void TestBasicInputOutputRelation()
        {
            var seq = new SequenceStep();
            var delay1 = new DelayStep();
            var delay2 = new DelayStep();
            seq.ChildTestSteps.AddRange([delay1, delay2]);
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
            [Output(OutputAvailability.AfterDefer)]
            public double Output { get; set; }
            
            [Output(OutputAvailability.AfterRun)]
            public double Output2 { get; set; }
            
            public double Input { get; set; }
            
            public bool CheckExpectedInput { get; set; }
            public double ExpectedInput { get; set; }
            
            public bool Defer { get; set; }
            public double DelaySecs { get; set; } = 0.0;
            
            public override void Run()
            {
                if(CheckExpectedInput && ExpectedInput != Input)
                    throw new Exception("Input has unexpected value");
                
                if(DelaySecs > 0.0)
                    TapThread.Sleep(TimeSpan.FromSeconds(DelaySecs));
                UpgradeVerdict(Verdict.Pass);
                this.Results.Defer(() => Output = Input);
                Output2 = Input;
            }

            public override string ToString() => $"{Name}";
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
        
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(8)] // connect 8 inputs/outputs and update across 8 threads.
        [TestCase(64)]
        public void TestInputOutputRelationsInTestPlanParallelN(int n)
        {
            var plan = new TestPlan();
            var parallel = new ParallelStep();
            
            plan.ChildTestSteps.Add(parallel);
            var step1 = new OutputInput {Output = 5, Input = 5, Name = "Step 1"};
            parallel.ChildTestSteps.Add(step1);
            for (int i = 0; i < n; i++)
            {
                var prevStep = parallel.ChildTestSteps.Last() as OutputInput;
                var newStep = new OutputInput {Output = 5, Input = 5, Name = "Step " + (i + 1)};
                parallel.ChildTestSteps.Add(newStep);
                // setup the connections.
                var outputMember = TypeData.GetTypeData(prevStep).GetMember(nameof(OutputInput.Output));
                var inputMember = TypeData.GetTypeData(newStep).GetMember(nameof(OutputInput.Input));
                InputOutputRelation.Assign(newStep, inputMember, prevStep, outputMember );
            }
            var r = plan.Execute();
            Assert.AreEqual(Verdict.Pass, r.Verdict);
        }
        [Test]
        public void TestInputOutputLoop()
        {
            var plan = new TestPlan();
            var parallel = new ParallelStep();
            
            plan.ChildTestSteps.Add(parallel);
            var step1 = new OutputInput {Output = 5, Input = 5, Name = "Step 1"};
            var step2 = new OutputInput {Output = 5, Input = 5, Name = "Step 1"};
            var step3 = new OutputInput {Output = 5, Input = 5, Name = "Step 1"};
            parallel.ChildTestSteps.AddRange(new [] {step1, step2, step3});
            var a = TypeData.GetTypeData(step1).GetMember(nameof(step1.Input));
            var b = TypeData.GetTypeData(step1).GetMember(nameof(step1.Output));
            InputOutputRelation.Assign(step2, a, step1, b);
            InputOutputRelation.Assign(step3, a, step2, b);
            InputOutputRelation.Assign(step1, a, step3, b);

           
            var r = plan.Execute();
            Assert.AreEqual(Verdict.Error, r.Verdict);
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
                menu.MenuItems.First(x => x.Get<IconAnnotationAttribute>()?.IconName == IconNames.AssignOutput).Get<IMethodAnnotation>().Invoke();
                request.SelectName = "Step 2";
                menu = AnnotationCollection.Annotate(step3).GetMember(nameof(OutputInput.Input))
                    .Get<MenuAnnotation>();
                menu.MenuItems.First(x => x.Get<IconAnnotationAttribute>()?.IconName == IconNames.AssignOutput).Get<IMethodAnnotation>().Invoke();

                var input1 = AnnotationCollection.Annotate(step3).GetMember(nameof(OutputInput.Input)).GetAll<IIconAnnotation>()
                    .FirstOrDefault(x => x.IconName == IconNames.Input);
                
                var input2 = AnnotationCollection.Annotate(step2).GetMember(nameof(OutputInput.Input)).GetAll<IIconAnnotation>()
                    .FirstOrDefault(x => x.IconName == IconNames.Input);

                Assert.IsNotNull(input1);
                Assert.IsNotNull(input2);
                
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
        public void IconAnnotationForInputOutputRelations()
        {
            var step1 = new OutputInput { Output = 5, Input = 5, Name = "Step 1" };
            var step2 = new OutputInput { ExpectedInput = 5, CheckExpectedInput = true, Name = "Step 2" };
            var step3 = new OutputInput { ExpectedInput = 5, CheckExpectedInput = true, Name = "Step 3" };
            var plan = new TestPlan();
            plan.ChildTestSteps.AddRange(new[] { step1, step2, step3 });
            var outputMember = TypeData.GetTypeData(step1).GetMember(nameof(OutputInput.Output));
            var inputMember = TypeData.GetTypeData(step1).GetMember(nameof(OutputInput.Input));
            InputOutputRelation.Assign(step2, inputMember, step1, outputMember);
            InputOutputRelation.Assign(step3, inputMember, step2, outputMember);


            var a = AnnotationCollection.Annotate(step2);
            var im = a.GetMember(inputMember.Name);
            var iicon = im.Get<ISettingReferenceIconAnnotation>();
            Assert.NotNull(iicon);
            Assert.AreEqual(IconNames.Input, iicon.IconName);
            Assert.AreEqual(step1.Id, iicon.TestStepReference);
            Assert.AreEqual(outputMember.Name, iicon.MemberName);

            var om = a.GetMember(outputMember.Name);
            var oicon = om.Get<ISettingReferenceIconAnnotation>();
            Assert.NotNull(oicon);
            Assert.AreEqual(IconNames.OutputAssigned, oicon.IconName);
            Assert.AreEqual(step3.Id, oicon.TestStepReference);
            Assert.AreEqual(inputMember.Name, oicon.MemberName);
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
                menu.MenuItems.First(x => x.Get<IconAnnotationAttribute>()?.IconName == IconNames.AssignOutput).Get<IMethodAnnotation>().Invoke();

                var input1 = AnnotationCollection.Annotate(step3).GetMember(nameof(OutputInput.Input)).GetAll<IIconAnnotation>()
                    .FirstOrDefault(x => x.IconName == IconNames.Input);
                Assert.IsNotNull(input1);

                step2.Input = 0;
                step3.Input = 0;
                var run = plan.Execute();
                Assert.AreEqual(Verdict.Pass, run.Verdict);
                Assert.IsTrue(request.WasInvoked);
                
                menu = AnnotationCollection.Annotate(new []{step2, step3}).GetMember(nameof(OutputInput.Input))
                    .Get<MenuAnnotation>();
                menu.MenuItems.First(x => x.Get<IconAnnotationAttribute>().IconName == IconNames.UnassignOutput).Get<IMethodAnnotation>().Invoke();
                var input2 = AnnotationCollection.Annotate(step3).GetMember(nameof(OutputInput.Input)).GetAll<IIconAnnotation>()
                    .FirstOrDefault(x => x.IconName == IconNames.Input);
                Assert.IsNull(input2);
                step2.Input = 0;
                step3.Input = 0;
                run = plan.Execute();
                Assert.AreEqual(Verdict.Error, run.Verdict);
                
                
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
            var member = menu.MenuItems.FirstOrDefault(x => x.Get<IconAnnotationAttribute>()?.IconName == IconNames.AssignOutput);
            member.Get<IMethodAnnotation>().Invoke(); // this previously failed with an null reference exception.
        }

        [Test]
        public void TestAutoRemoveInputOutputRelation()
        {
            var plan = new TestPlan();
            var repeat = new RepeatStep();
            plan.ChildTestSteps.Add(repeat);
            var log = new LogStep();
            repeat.ChildTestSteps.Add(log);
            {
                var member = TypeData.GetTypeData(log).GetMember(nameof(LogStep.LogMessage));
                var outputMember = TypeData.GetTypeData(repeat).GetMember(nameof(RepeatStep.IterationInfo));
                InputOutputRelation.Assign(log, member, repeat, outputMember);
                Assert.IsTrue(InputOutputRelation.IsInput(log, member));
                Assert.IsTrue(InputOutputRelation.IsOutput(repeat, outputMember));
            }
            plan.ChildTestSteps.Remove(repeat);
            {
                var member = TypeData.GetTypeData(log).GetMember(nameof(LogStep.LogMessage));
                var outputMember = TypeData.GetTypeData(repeat).GetMember(nameof(RepeatStep.IterationInfo));
                Assert.IsTrue(InputOutputRelation.IsInput(log, member));
                Assert.IsTrue(InputOutputRelation.IsOutput(repeat, outputMember));
            }

            repeat.ChildTestSteps.Remove(log);
            {
                var member = TypeData.GetTypeData(log).GetMember(nameof(LogStep.LogMessage));
                var outputMember = TypeData.GetTypeData(repeat).GetMember(nameof(RepeatStep.IterationInfo));
                Assert.IsFalse(InputOutputRelation.IsInput(log, member));
                Assert.IsFalse(InputOutputRelation.IsOutput(repeat, outputMember));
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
            var seq = new SequenceStep();
            seq.ChildTestSteps.AddRange([a,b]);
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

        [Test]
        public void LogPrintOutputOrderTest()
        {
            // previously, the value of the {Log Message} in GetFormattedName
            // would be based on the previous value of the input, not the current.
            // It should be based on the current value. E.g the order of the messages should be correct.
            var plan = new TestPlan();
            var repeat = new RepeatStep {Count = 3, Action =  RepeatStep.RepeatStepAction.Fixed_Count};
            var logOutput = new LogStep{Name = "Log Step {Log Message}"};
            var x = logOutput.GetFormattedName();
            plan.ChildTestSteps.Add(repeat);
            repeat.ChildTestSteps.Add(logOutput);
            InputOutputRelation.Assign(logOutput, TypeData.GetTypeData(logOutput).GetMember(nameof(logOutput.LogMessage)),
                repeat, TypeData.GetTypeData(repeat).GetMember(nameof(repeat.IterationInfo))
            );

            var rl = new PlanRunCollectorListener();
            plan.Execute(new IResultListener[] {}); // create the initial condition that repeat.IterationInfo is "3 of 3".
            plan.Execute(new IResultListener[] {rl});
            int i1 = rl.LogString.IndexOf("Log Step 1 of 3\" started");
            int i2 = rl.LogString.IndexOf("Log Step 2 of 3\" started");
            int i3 = rl.LogString.IndexOf("Log Step 3 of 3\" started");
            Assert.IsTrue(i1 < i2);
            Assert.IsTrue(i2 < i3);
        }

        public class InputOutputTypesStep : TestStep
        {
            [Output]
            public int IntInput { get; set; }
            [Output]
            public double DoubleInput { get; set; }
            [Output]
            public string StringInput { get; set; }
            [Output]
            public float FloatInput { get; set; }

            public override void Run()
            {
                
            }
        }

        [Test]
        [TestCase("Iteration", "3 of 3")]
        [TestCase("Outputs \\ Iteration", "3")]
        public void TestInputOutputNameClash(string output, string expected)
        {
            var plan = new TestPlan();
            var repeat = new RepeatStep();
            var step = new InputOutputTypesStep();
            plan.ChildTestSteps.Add(repeat);
            plan.ChildTestSteps.Add(step);

            var a = AnnotationCollection.Annotate(step);
            var im = a.GetMember(nameof(step.StringInput));
            var menu = im.Get<MenuAnnotation>();

            var request = new SelectAssignmentFromAnnotationMenu();
            UserInput.SetInterface(request);
            request.SelectName = output;
            menu.MenuItems.First(x => x.Get<IconAnnotationAttribute>()?.IconName == IconNames.AssignOutput)
                .Get<IMethodAnnotation>().Invoke();

            plan.Execute();
            Assert.AreEqual(step.StringInput, expected);
        }

        [Test]
        public void TestInputOutputTypes()
        {
            var step1 = new InputOutputTypesStep();
            var step2 = new InputOutputTypesStep();
            var plan = new TestPlan();
            plan.ChildTestSteps.AddRange(new[]
            {
                step1, step2
            });
            {
                var intMember = TypeData.GetTypeData(step1).GetMember(nameof(InputOutputTypesStep.IntInput));
                var doubleMember = TypeData.GetTypeData(step1).GetMember(nameof(InputOutputTypesStep.DoubleInput));
                var stringMember = TypeData.GetTypeData(step1).GetMember(nameof(InputOutputTypesStep.StringInput));
                var floatMember = TypeData.GetTypeData(step1).GetMember(nameof(InputOutputTypesStep.FloatInput));
                InputOutputRelation.Assign(step2, doubleMember, step1, stringMember);
                InputOutputRelation.Assign(step2, stringMember, step1, doubleMember);
                InputOutputRelation.Assign(step2, intMember, step1, doubleMember);
                InputOutputRelation.Assign(step2, floatMember, step1, floatMember);
            }
            {
                step1.IntInput = 1;
                step1.DoubleInput = 2;
                step1.StringInput = "3";
                step1.FloatInput = 4;
                InputOutputRelation.UpdateInputs(step2);
                Assert.AreEqual(2, step2.IntInput);
                Assert.AreEqual(3.0, step2.DoubleInput);
                Assert.AreEqual("2", step2.StringInput);
                Assert.AreEqual(4, step2.FloatInput);
                
                // When one of the inputs is assigned an invalid value, an exception should be thrown on UpdateInputs.
                step1.StringInput = "abc";
                Assert.Throws<Exception>(() => InputOutputRelation.UpdateInputs(step2));
            }
        }

        enum SpecialValue
        {
            UnspecifiedResult
        }

        [TestCase("A", typeof(double), null, false)]
        [TestCase("A", typeof(float), null, false)]
        [TestCase("A", typeof(int), null, false)]
        [TestCase("1", typeof(int), null, false)]
        [TestCase("True", typeof(int), null, false)]
        [TestCase("True", typeof(bool), null, false)]
        [TestCase("A", typeof(string), "A", true)]
        [TestCase(true, typeof(int), 1, true)]
        [TestCase(true, typeof(double), 1.0, true)]
        [TestCase(false, typeof(double), 0.0, true)]
        [TestCase(false, typeof(int), 0, true)]
        [TestCase(false, typeof(string), "False", true)]
        [TestCase(long.MaxValue, typeof(float), SpecialValue.UnspecifiedResult, true)]
        [TestCase(long.MaxValue, typeof(double), SpecialValue.UnspecifiedResult, true)]
        [TestCase(long.MaxValue, typeof(long), long.MaxValue, true)]
        [TestCase(long.MaxValue, typeof(int), typeof(OverflowException), true)]
        [TestCase(ulong.MaxValue, typeof(long), typeof(OverflowException), true)]
        [TestCase(100000, typeof(byte), typeof(OverflowException), true)]
        [TestCase(255, typeof(byte), 255, true)]
        [TestCase(256, typeof(byte), typeof(OverflowException), true)]
        [TestCase(1e20, typeof(int), typeof(OverflowException), true)]
        [TestCase(1e20, typeof(byte), typeof(OverflowException), true)]
        [TestCase(1.0, typeof(byte), 1, true)]
        [TestCase(1.0, typeof(int), 1, true)]
        [TestCase(1.0, typeof(float), 1.0, true)]
        [TestCase(1.0, typeof(double), 1.0, true)]
        [TestCase(1.0, typeof(string), "1", true)]
        public void TestConvertOutputToInput(object from, Type intoType, object exceptionOrResult, bool canConvert)
        {
            var toTypeData = TypeData.FromType(intoType);
            var fromTypeData = TypeData.GetTypeData(from);
            Assert.IsTrue(canConvert == InputOutputRelation.CanConvert(toTypeData, fromTypeData));
            if (canConvert)
            {
                try
                {
                    var to = InputOutputRelation.ConvertValue(from, toTypeData);
                    Assert.IsFalse(exceptionOrResult is Exception);
                    if (Equals(exceptionOrResult, SpecialValue.UnspecifiedResult))
                    {
                        var to2 = (double)InputOutputRelation.ConvertValue(to, TypeData.FromType(typeof(double)));
                        Assert.IsTrue(to2 != 0.0);
                    }
                    else
                    {
                        Assert.AreEqual(exceptionOrResult, to);
                    }
                }
                catch (Exception e)
                {
                    Assert.AreEqual(exceptionOrResult, e.GetType());
                }
            }
        }

        
        [TestCase(typeof(Verdict), typeof(string), true)]
        [TestCase(typeof(Verdict), typeof(double), false)]
        [TestCase(typeof(double), typeof(Verdict), false)]
        [TestCase(typeof(string), typeof(Verdict), false)]
        public void TestCanConvertBehavior(Type from, Type to, bool canConvert)
        {
            var from2 = TypeData.FromType(from);
            var to2 = TypeData.FromType(to);
            var canConvertValue = InputOutputRelation.CanConvert(to2, from2);
            Assert.AreEqual(canConvert, canConvertValue);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestInputAndOutputWithRemovedMixins(bool way)
        {
            var seq1 = new SequenceStep();
            var b1 = new TestNumberMixinBuilder()
            {
                Name = "Test",
                IsOutput = true
            };
            var seq2 = new SequenceStep();
            var b2 = new TestNumberMixinBuilder
            {
                Name = "Test"
            };
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(seq1);
            plan.ChildTestSteps.Add(seq2);

            MixinFactory.LoadMixin(seq1, b1);
            MixinFactory.LoadMixin(seq2, b2);
            
            var member1 = (MixinMemberData) TypeData.GetTypeData(seq1).GetMember("Number.Test");
            var member2 = (MixinMemberData) TypeData.GetTypeData(seq2).GetMember("Number.Test");
            InputOutputRelation. Assign(seq2, member2, seq1, member1);
            
            var x1 = InputOutputRelation.GetRelations(seq1).Count();
            var x12 = InputOutputRelation.GetRelations(seq2).Count();
            Assert.AreEqual(1, x1);
            Assert.AreEqual(1, x12);
            
            // Now the input/output relation has been set up.
            // Two things can happen:
            // 1. The mixin is removed from seq1 -> the input member is lost.
            // 2. The mixin is removed from seq2 -> the output member is lost.
            // in either case the relation should be removed.
            
            if(way)
                MixinFactory.UnloadMixin(seq1, member1);
            else
                MixinFactory.UnloadMixin(seq2, member2);
            var x2 = InputOutputRelation.GetRelations(seq1).Count();
            var x22 = InputOutputRelation.GetRelations(seq2).Count();
            Assert.AreEqual(0, x2);
            Assert.AreEqual(0, x22);
        }
    }
}
