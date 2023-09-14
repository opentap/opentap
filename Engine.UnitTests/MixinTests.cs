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
    public class MixinTestBuilder : IMixinBuilder
    {
        public string TestMember { get; set; }
        public void Initialize(ITypeData targetType)
        {
            
        }
        public MixinMemberData ToDynamicMember(ITypeData targetType)
        {
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

