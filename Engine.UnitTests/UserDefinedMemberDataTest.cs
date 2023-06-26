using System;
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
                Writable = true
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
        }

        class UserInputTest : IUserInputInterface
        {
            public Action<object> Func { get; set; } 

            public void RequestUserInput(object dataObject, TimeSpan Timeout, bool modal)
            {
                Func(dataObject);
            }
        }
        
        [Test]
        public void UserDefinedMemberAnnotationTest()
        {
            var user = new UserInputTest();
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