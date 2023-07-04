using System.Linq;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.Engine.UnitTests
{
    public class UserDefinedMemberDataTest
    {
        [Test]
        public void UseUserDefinedMemberData()
        {
            var step = new LogStep();
            var plan = new TestPlan();  
            plan.Steps.Add(step);
            var member = new UserDefinedDynamicMember()
            {
                Name = "Test", 
                TypeDescriptor = TypeData.FromType(typeof(int)),
                Readable = true,
                Writable = true,
                AttributesCode = "[Unit(\"Hz\")]"
            };
            
            DynamicMember.AddDynamicMember(step, member);
            var userdefined = UserDefinedDynamicMember.GetUserDefinedMembers(step);
            Assert.AreEqual(1, userdefined.Length);
            member.SetValue(step, 10);
            var testMemberValue = member.GetValue(step);

            var xml = plan.SerializeToString();
            var serializer = new TapSerializer();
            var plan2 = (TestPlan)serializer.DeserializeFromString(xml);
            var step2 = (LogStep)plan2.Steps[0];

            var testMemberValue2 = member.GetValue(step2);
            
            Assert.AreEqual("", string.Join(",", serializer.Errors));
            Assert.AreEqual(10, testMemberValue);
            Assert.AreEqual(10, testMemberValue2);
            Assert.IsTrue(member.HasAttribute<UnitAttribute>());
        }

        [Test]
        public void UseUserDefinedParameterizedMemberData()
        {
            var step = new LogStep();
            var seq = new SequenceStep();
            var plan = new TestPlan();
            plan.Steps.Add(seq);
            seq.ChildTestSteps.Add(step);
            var stepMember = new UserDefinedDynamicMember()
            {
                Name = "Test",
                TypeDescriptor = TypeData.FromType(typeof(int)),
                Readable = true,
                Writable = true,
                AttributesCode = "[Unit(\"Hz\")]"
            };
            string propName = "Z";
            DynamicMember.AddDynamicMember(step, stepMember);
            ParameterManager.Parameterize(seq, stepMember, new []{step}, propName);
            var seqMember = TypeData.GetTypeData(seq).GetMember(propName);
            seqMember.SetValue(seq, 10);
            
            var planXml = plan.SerializeToString();

            var plan2 = TapSerializer.DeserializeFromXml<TestPlan>(planXml);
            var seq2 = plan2.ChildTestSteps[0];
            var step2 = seq2.ChildTestSteps[0];
            var seq2Member = TypeData.GetTypeData(seq2).GetMember(propName);
            var step2Member = TypeData.GetTypeData(step2).GetMember(stepMember.Name);
            

            Assert.AreEqual(10, stepMember.GetValue(step));
            Assert.AreEqual(10, seqMember.GetValue(seq));
            
            Assert.AreEqual(10, step2Member.GetValue(step2));
            Assert.AreEqual(10, seq2Member.GetValue(seq2));
            Assert.IsTrue(stepMember.GetAttribute<UnitAttribute>().Unit == "Hz");
            Assert.IsTrue(step2Member.GetAttribute<UnitAttribute>().Unit == "Hz");
            Assert.IsTrue(seqMember.GetAttribute<UnitAttribute>().Unit == "Hz");
            Assert.IsTrue(seq2Member.GetAttribute<UnitAttribute>().Unit == "Hz");


        }

        
        
        [Test]
        public void UserDefinedMemberAnnotationTest()
        {
            var user = new UserInputTestImpl();
            var input = UserInput.GetInterface();
            UserInput.SetInterface(user);
            try
            {
                var step = new LogStep();
                var plan = new TestPlan();
                plan.Steps.Add(step);

                var a = AnnotationCollection.Annotate(step);
                var member = a.GetMember(nameof(step.LogMessage));
                var menu = member.Get<MenuAnnotation>();
                var items = menu.MenuItems.ToArray();
                var dyn = items.FirstOrDefault(mem => mem.Get<IIconAnnotation>().IconName == IconNames.AddDynamicProperty);

                user.Func = (obj) =>
                {
                    var a = AnnotationCollection.Annotate(obj);
                    var attributes = a.GetMember("Attributes");
                    var str = attributes.Get<IStringValueAnnotation>();
                    str.Value = "[Output()][Unit(\"Hz\")][EnabledIf(\"B>-1\")]";
                    var memberName = a.GetMember("PropertyName");
                    var str2 = memberName.Get<IStringValueAnnotation>();
                    str2.Value = "B";

                    a.Write();
                };
                    
                dyn.Get<IMethodAnnotation>()?.Invoke();


                var newmem = TypeData.GetTypeData(step).GetMember("B");
                Assert.IsTrue(newmem.HasAttribute<OutputAttribute>());
                Assert.IsTrue(newmem.GetAttribute<UnitAttribute>()?.Unit == "Hz");
                Assert.IsTrue(newmem.HasAttribute<EnabledIfAttribute>());

            }
            finally
            {
                UserInput.SetInterface(input);
            }
        }

    }
}