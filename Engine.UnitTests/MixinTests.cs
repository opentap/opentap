using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;
using Tap.Shared;
namespace OpenTap.UnitTests
{
    [TestFixture]
    public class MixinTests
    {
        [Test]
        public void TestLoadingMixins()
        {
            var plan = new TestPlan();
            var step = new LogStep();
            plan.ChildTestSteps.Add(step);

            MixinFactory.LoadMixin(step, new MixinTestBuilder
            {
                TestMember = "123"
            });
            
            plan.Execute();

            var xml = plan.SerializeToString();

            var plan2 = (TestPlan)new TapSerializer().DeserializeFromString(xml);
            var step2 = plan2.ChildTestSteps[0];
            var onPostRunCalled = TypeData.GetTypeData(step2).GetMember("TestMixin.OnPostRunCalled").GetValue(step2);
            var onPreRunCalled = TypeData.GetTypeData(step2).GetMember("TestMixin.OnPreRunCalled").GetValue(step2);
            
            Assert.AreEqual(true, onPostRunCalled);
            Assert.AreEqual(true, onPreRunCalled);
        }

        [Test]
        public void TestMixinWithValidationRule()
        {
            var step = new LogStep();
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(step);
            
            var builder = new MixinBuilderUi(new IMixinBuilder[]
            {
                new MixinTestBuilder
                {
                    TestMember = null
                }
            });

            var builderType = TypeData.GetTypeData(builder);
            foreach (var rule in builder.Rules)
            {
                var member = builderType.GetMember(rule.PropertyName);
                Assert.IsNotNull(member);
                if (member.Name.Contains("TestMember"))
                {
                    Assert.AreEqual("Test member must not be null.", (builder as IValidatingObject)[member.Name]);
                }
            }
            
            Assert.AreEqual("Test member must not be null.", builder.Error);
            
            builder = new MixinBuilderUi(new IMixinBuilder[]
            {
                new MixinTestBuilder
                {
                    TestMember = "123"
                }
            });
            Assert.IsTrue(string.IsNullOrWhiteSpace(builder.Error));


        }
        
        
        public class ProcessStepMixin : ITestStepPostRunMixin
        {

            public void OnPostRun(TestStepPostRunEventArgs eventArgs)
            {
                var step = (ResultsFromAttributesStep)eventArgs.TestStep;
                step.X = step.RunIndex * 5 + 2;
                step.Y = step.RunIndex * 10 + 513;
                step.UpgradeVerdict(Verdict.Pass);
            }
        }
        
        public class ResultsFromAttributesStep : TestStep
        {
            [Result]
            public double RunIndex { get; set; }
            
            [Result]
            public double X { get; set; }
            
            [Result]
            public double Y { get; set; }

            // add a mixin which generates new values for X and Y.
            [EmbedProperties]
            public ProcessStepMixin ResultsProcessorMixin { get; set; } = new ProcessStepMixin();

            public override void Run()
            {
                RunIndex += 1;
            }
        }
        
        //
        [Test]
        public void DynamicResultsFromMixinTest()
        {
            var results = new ResultsFromAttributesStep();
            var loop = new RepeatStep();
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(loop);
            loop.ChildTestSteps.Add(results);
            var rl = new RecordAllResultListener();
            var run = plan.Execute(new[]
            {
                rl
            });
            
            Assert.AreEqual(Verdict.Pass, run.Verdict);
            Assert.AreEqual(loop.Count * 1, rl.Results.Count);
            for (int i = 0; i < loop.Count; i++)
            {
                var idx = (double)rl.Results[i].Columns.First(x => x.Name == "RunIndex").Data.GetValue(0);
                var x = (double)rl.Results[i].Columns.First(x => x.Name == "X").Data.GetValue(0);
                var y = (double)rl.Results[i].Columns.First(x => x.Name == "Y").Data.GetValue(0);
                Assert.AreEqual(idx * 5 + 2, x);
                Assert.AreEqual(idx * 10 + 513, y);
            }
        }

        [Test]
        public void TestAddMixinRestrictions([Values] bool isLocked)
        {
            var plan = new TestPlan();
            var step = new DelayStep();
            plan.ChildTestSteps.Add(step);
            plan.Locked = isLocked;
            var memberAnnotation = AnnotationCollection.Annotate(step).GetMember(nameof(step.DelaySecs));
            var menu = memberAnnotation.Get<MenuAnnotation>().MenuItems.ToArray();
            var mixinItem = menu.FirstOrDefault(x => x.Get<IIconAnnotation>()?.IconName == IconNames.AddMixin);
            var access = mixinItem.GetAll<IAccessAnnotation>();
            var hidden = access.Any(x => x.IsVisible == false);
            if (isLocked)
                Assert.IsTrue(hidden);
            else
                Assert.IsFalse(hidden);
        }

        [Test]
        public void TestMixinFromReferencedTestPlan()
        {
            
            var planName = nameof(TestMixinFromReferencedTestPlan) + Guid.NewGuid() + ".TapPlan";
            var plan2Name = nameof(TestMixinFromReferencedTestPlan) + Guid.NewGuid() + ".TapPlan";
            
            {   // create plan with a mixin that has been parameterized.
                var plan1 = new TestPlan();
                var step1 = new DelayStep();
                plan1.ChildTestSteps.Add(step1);
                MixinFactory.LoadMixin(step1, new MixinTestBuilder()
                {
                    TestMember = nameof(step1.DelaySecs)
                });

                var member = TypeData.GetTypeData(step1).GetMember("TestMixin.OutputStringValue");
                member.Parameterize(plan1, step1, "A");

                plan1.Save(planName);
            }
            {
                var plan2 = new TestPlan();
                var tpr = new TestPlanReference();
                tpr.Filepath.Text = planName;
                plan2.ChildTestSteps.Add(tpr);
                tpr.LoadTestPlan();
                var aMember = TypeData.GetTypeData(tpr).GetMember("A");
                aMember.SetValue(tpr, "123");
                Assert.IsNotNull(aMember);
                plan2.Save(plan2Name);
            }
            {
                var plan = TestPlan.Load(plan2Name);
                var tpr2 = (TestPlanReference)plan.ChildTestSteps[0];
                var aMember = TypeData.GetTypeData(tpr2).GetMember("A");
                Assert.IsNotNull(aMember);
                var aMemberValue = aMember.GetValue(tpr2) as string;
                Assert.AreEqual("123", aMemberValue);
            }
        }
        
        [Test]
        public void TestMixinOnTestPlanDirect()
        {
            
            var planName = nameof(TestMixinOnTestPlanDirect) + Guid.NewGuid() + ".TapPlan";
            
            {   // create plan with a mixin that has been parameterized.
                var plan1 = new TestPlan();
                var step1 = new DelayStep();
                plan1.ChildTestSteps.Add(step1);
                MixinFactory.LoadMixin(plan1, new MixinTestBuilder()
                {
                    TestMember = nameof(plan1.Locked)
                });

                var member = TypeData.GetTypeData(plan1).GetMember("TestMixin.OutputStringValue");
                member.SetValue(plan1, "123");
                plan1.Save(planName);
            }
            
            {
                var plan = TestPlan.Load(planName);
                var member = TypeData.GetTypeData(plan).GetMember("TestMixin.OutputStringValue");
                Assert.IsNotNull(member);
                var aMemberValue = member.GetValue(plan) as string;
                Assert.AreEqual("123", aMemberValue);
            }
        }
        
        [Test]
        public void TestParameterizeAndRemoveMixin()
        {
            // verify that a parameterized mixin member has the parameter removed when the mixin is removed.
            var plan1 = new TestPlan();
            var step1 = new DelayStep();
            plan1.ChildTestSteps.Add(step1);
            var numberMember = MixinFactory.LoadMixin(step1, new TestNumberMixinBuilder { Name = "A" });
            
            numberMember.Parameterize(plan1, step1, "A");
            
            var paramMember0 = TypeData.GetTypeData(plan1).GetMember("A");
            // now this should _not_ be null.
            Assert.IsNotNull(paramMember0);
            
            MixinFactory.UnloadMixin(step1, numberMember);
            var paramMember = TypeData.GetTypeData(plan1).GetMember("A");
            // now this should be null (removed).
            Assert.IsNull(paramMember);
            
            // now try with an embedded member mixin.
            var embeddedMixin = MixinFactory.LoadMixin(step1, new MixinTestBuilder()
            {
                TestMember = nameof(step1.DelaySecs)
            });
            var embeddedMember = TypeData.GetTypeData(step1).GetMember("TestMixin.OutputStringValue");
            embeddedMember.Parameterize(plan1, step1, "B");
            
            var paramMember2 = TypeData.GetTypeData(plan1).GetMember("B");
            // now this should _not_ be null.
            Assert.IsNotNull(paramMember2);
            MixinFactory.UnloadMixin(step1, embeddedMixin);
            
            var paramMember3 = TypeData.GetTypeData(plan1).GetMember("B");
            // now this should be null.
            Assert.IsNull(paramMember3);
        }

        public class ActionStep : TestStep
        {
            public Action action { get; set; }
            
            public override void Run()
            {
                action();
            }
            public static ActionStep FromAction(Action action) => new ActionStep()
            {
                action = action
            };
        }
        
        [Test]
        public void CannotAddMixinWhilePlanRunning()
        {
            ManualResetEvent continueEvt = new ManualResetEvent(false);
            ManualResetEvent started = new ManualResetEvent(false);
            var step = ActionStep.FromAction(() =>
            {
                started.Set();
                continueEvt.WaitOne();
            });
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(step);
            var planRun = plan.ExecuteAsync();
            started.WaitOne();
            // ok, now the test plan is waiting until we set continueEvt.

            var a = AnnotationCollection.Annotate(step);
            var addMixin = a.GetIcon(IconNames.AddMixin);
            Assert.IsNotNull(addMixin);
            var enabled = addMixin.GetAll<IEnabledAnnotation>();
            Assert.IsTrue(enabled.Any());
            var isDisabled = enabled.Any(x => x.IsEnabled == false);
            Assert.IsTrue(isDisabled);
            continueEvt.Set();
            planRun.Wait();
            a.Read();
            var isDisabled2 = enabled.Any(x => x.IsEnabled == false);
            Assert.IsFalse(isDisabled2);

        } 
        public class PrerunMixinModifyingParametersBuilder : IMixinBuilder
        {
            public void Initialize(ITypeData targetType)
            {
            }
            
            IEnumerable<Attribute> GetAttributes()
            {
                yield return new EmbedPropertiesAttribute();
            }

            public MixinMemberData ToDynamicMember(ITypeData targetType)
            {
                return new MixinMemberData(this, () => new PrerunMixinModifyingParameters())
                {
                    TypeDescriptor = TypeData.FromType(typeof(PrerunMixinModifyingParameters)),
                    Writable = true,
                    Readable = true,
                    DeclaringType = targetType,
                    Attributes = GetAttributes(),
                    Name = nameof(PrerunMixinModifyingParameters) 
                };
            }
        }
        
        public class PrerunMixinModifyingParameters : IMixin, ITestStepPreRunMixin
        {
            public PrerunMixinModifyingParameters()
            {
                
            }
            public void OnPreRun(TestStepPreRunEventArgs eventArgs)
            {
                var step = eventArgs.TestStep as LogStep;
                step.LogMessage = "Hello Prerun";
                step.StepRun.Parameters["Additional Parameter"] = "More Info";
            }
        }

        [Test]
        public void PrerunMixinModifyParametersTest()
        { 
            var plan = new TestPlan();
            var step = new LogStep() { LogMessage = "No Prerun" };
            plan.ChildTestSteps.Add(step);

            { /* no mixin */
                var rl = new RecordAllResultListener();
                plan.Execute(new[] { rl });
                var run = rl.RunStart.First(r => (r.Value as TestStepRun)?.TestStepId == step.Id).Value; 
                Assert.AreEqual("No Prerun", run.Parameters["Log Message"]);
            }

            { /* prerun mixin */
                MixinFactory.LoadMixin(step, new PrerunMixinModifyingParametersBuilder());
                var rl = new RecordAllResultListener();
                plan.Execute(new[] { rl });
                var run = rl.RunStart.First(r => (r.Value as TestStepRun)?.TestStepId == step.Id).Value; 
                Assert.AreEqual("Hello Prerun", run.Parameters["Log Message"]);
                Assert.AreEqual("More Info", run.Parameters["Additional Parameter"]);
            }
        }

        [Test]
        public void CanAddMixinToTestPlanReference()
        {
            var plan1 = new TestPlan();
            plan1.ChildTestSteps.Add(new DelayStep());
            var plan1File = PathUtils.GetTempFileName("TapPlan");
            plan1.Save(plan1File);

            var plan2 = new TestPlan();
            var tpr = new TestPlanReference();
            tpr.Filepath.Text = plan1File;

            plan2.ChildTestSteps.Add(tpr);
            tpr.LoadTestPlan();
            {
                var a = AnnotationCollection.Annotate(tpr);
                var addMixin = a.GetIcon(IconNames.AddMixin);
                var enabled = addMixin.Get<IAccessAnnotation>();
                // it _is_ allowed to add mixins to test plan reference.
                Assert.IsTrue(enabled.IsVisible);
            }
            {
                var a = AnnotationCollection.Annotate(tpr.ChildTestSteps[0]);
                var addMixin = a.GetIcon(IconNames.AddMixin);
                var enabled = addMixin.Get<IAccessAnnotation>();
                // it is not allowed to add mixins to the child steps of test plan reference.
                Assert.IsFalse(enabled.IsVisible);
            }

        }
        
        

        [Test]
        public void CannotModifyMixinTest()
        {

            var plan1 = new TestPlan();
            var step1 = new DelayStep();
            plan1.ChildTestSteps.Add(step1);
            MixinFactory.LoadMixin(step1, new MixinTestBuilder()
            {
                TestMember = nameof(step1.DelaySecs)
            });

            var memberAnnotation = AnnotationCollection.Annotate(step1).GetMember("TestMixin.MixinLoadValue");
            var menuItems = memberAnnotation.Get<MenuAnnotation>().MenuItems.ToArray();
            var menuModel = menuItems.Select(x => x.Source).OfType<MixinMemberMenuModel>().First() as IMemberMenuModel;
            Assert.IsNotNull(menuModel.Member);
        }

        public class UserInputOverride : IUserInputInterface
        {
            public void RequestUserInput(object dataObject, TimeSpan Timeout, bool modal)
            {

            }
        }

        [Test]
        public void ModifyMixinTest()
        {
            var plan1 = new TestPlan();
            var step1 = new DelayStep();
            plan1.ChildTestSteps.Add(step1);
            var mixinMember = MixinFactory.LoadMixin(step1, new MixinTestBuilder()
            {
                TestMember = nameof(step1.DelaySecs)
            });
            var memberMenuModel = new MixinMemberMenuModel(mixinMember)
            {
                Source = new object[]{step1}
            };
            using (Session.Create(SessionOptions.None))
            {
                UserInput.SetInterface(new UserInputOverride());
                
                // previously this would have thrown an exception
                // because the mixin does not allow itself to be defined twice.
                // this was really only because we added the new one before removing the old.
                memberMenuModel.ModifyMixin();
            }
        }

        public class SimpleOutputStep : TestStep
        {
            [Output]
            public double Output { get; set; }
            public override void Run()
            {
                
            }
        }

        [Test]
        public void MixinAsInput()
        {
            var plan = new TestPlan();
            var step1 = new SimpleOutputStep();
            var step2 = new SequenceStep();
            plan.ChildTestSteps.Add(step1);
            plan.ChildTestSteps.Add(step2);
            
            var mixinMember = MixinFactory.LoadMixin(step2, new TestNumberMixinBuilder()
            {
                Name = "Number"
            });
            
            InputOutputRelation.Assign(step2, mixinMember, step1, TypeData.GetTypeData(step1).GetMember(nameof(step1.Output)));
            var s = new TapSerializer().SerializeToString(plan);
            var i1 = s.IndexOf("OpenTap.UnitTests.TestNumberMixinBuilder", StringComparison.Ordinal);
            var i2 = s.IndexOf("OpenTap.UnitTests.TestNumberMixinBuilder", i1 + 1, StringComparison.Ordinal);
            //  the xml should only contain the once.
            // this means it is included at least two times.
            Assert.IsTrue(i1 != -1);
            Assert.IsTrue(i2 == -1);
        }
        

    }

    public class MixinTest : IMixin, ITestStepPostRunMixin, ITestStepPreRunMixin, IAssignOutputMixin
    {
        public bool OnPostRunCalled { get; set; }
        public bool OnPreRunCalled { get; set; }
        public bool OnAssignOutputCalled { get; set; }
        public string OutputStringValue { get; set; }
        
        [Browsable(true)]
        public string MixinLoadValue { get; }

        public MixinTest(string loadValue) => MixinLoadValue = loadValue;

        public void OnPostRun(TestStepPostRunEventArgs eventArgs)
        {
            OnPostRunCalled = true;
        }
        public void OnPreRun(TestStepPreRunEventArgs eventArgs)
        {
            OnPreRunCalled = true;
        }
        public void OnAssigningOutput(AssignOutputEventArgs args)
        {
            OnAssignOutputCalled = true;
            OutputStringValue = args.Value.ToString();
        }
    }
    
    [MixinBuilder(typeof(ITestStepParent))]
    public class MixinTestBuilder : ValidatingObject, IMixinBuilder
    {
        public MixinTestBuilder()
        {
            Rules.Add(() => TestMember != null, "Test member must not be null.", nameof(TestMember));
        }
        
        public string TestMember { get; set; }
        public void Initialize(ITypeData targetType)
        {
            
        }
        public MixinMemberData ToDynamicMember(ITypeData targetType)
        {
            if (!string.IsNullOrWhiteSpace(Error))
                throw new Exception($"{Error}");
            // targetType is wrapped in an EmbeddedMember data object that hides TestMixin on the outside.
            if (targetType.BaseType.GetMember("TestMixin") != null)
            {
                throw new Exception("Target type already has a TestMixin member.");
            }
            return new MixinMemberData(this, () => new MixinTest(TestMember))
            {
                TypeDescriptor = TypeData.FromType(typeof(MixinTest)),
                Attributes = GetAttributes().ToArray(),
                Writable = true,
                Readable = true,
                DeclaringType = targetType,
                Name = "TestMixin"
            };
        }
        
        IEnumerable<Attribute> GetAttributes()
        {
            yield return new EmbedPropertiesAttribute();
            yield return new DisplayAttribute("Test Mixin", Order: 19999);
        }
    }
    
    [MixinBuilder(typeof(ITestStepParent))]
    public class TestNumberMixinBuilder : ValidatingObject, IMixinBuilder
    {
        public string Name { get; set; }
        public bool IsOutput { get; set; }
        public TestNumberMixinBuilder()
        {
            
        }
        
        public void Initialize(ITypeData targetType)
        {
            
        }
        public MixinMemberData ToDynamicMember(ITypeData targetType)
        {
            return new MixinMemberData(this, () => 0)
            {
                TypeDescriptor = TypeData.FromType(typeof(double)),
                Attributes = GetAttributes().ToArray(),
                Writable = true,
                Readable = true,
                DeclaringType = targetType,
                Name = "Number." + Name 
            };
        }
        
        IEnumerable<Attribute> GetAttributes()
        {
            yield return new DisplayAttribute(Name, Order: 19999);

            if (IsOutput)
                yield return new OutputAttribute();
        }
    }
}

