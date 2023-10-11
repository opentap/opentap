using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;
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
            if (string.IsNullOrWhiteSpace(TestMember))
                return null;
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
        
        public IMixinBuilder Clone()
        {
            return (IMixinBuilder)this.MemberwiseClone();
        }
    }
}

