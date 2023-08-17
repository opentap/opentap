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

