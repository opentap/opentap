using System.ComponentModel;
using System.Linq;
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class UserAddedMixins
    {

        class SomeClass
        {
            public double Test { get; set; }
        }

        class EmbeddingClass
        {
            public double Test2 { get; set; }
        }
        
        [Test]
        public void BigMixinTest()
        {
            var test = new SomeClass();
            var member = new UserDefinedDynamicMember
            {
                DeclaringType = TypeData.FromType(typeof(EmbeddingClass)),
                TypeDescriptor = TypeData.FromType(typeof(EmbeddingClass)),
                Writable = true,
                Readable = true,
                AttributesCode = "[EmbedProperties()]",
                Name = "E"
            };
            Assert.IsTrue(member.HasAttribute<EmbedPropertiesAttribute>());
            DynamicMember.AddDynamicMember(test, member);
            member.SetValue(test, new EmbeddingClass{Test2 = 10.0});

            var td = TypeData.GetTypeData(test);
            
            var test2mem = td.GetMember("E.Test2");
            Assert.IsNotNull(test2mem);
            Assert.AreEqual(10.0, test2mem.GetValue(test));
        }

        [Test]
        public void AddingMixinsTest()
        {
            var test = new SomeClass();
            var builders = MixinFactory.GetMixinBuilders(TypeData.GetTypeData(test)).ToArray();
            var builderUi = new MixinBuilderUi(builders);
            var type = TypeData.GetTypeData(builderUi);
            var members = type.GetMembers();
            var a = AnnotationCollection.Annotate(builderUi);
            var textNameMember = a.GetMember("OpenTap.TextMixinBuilder.Name");
            var enabled1 = textNameMember.Get<IAccessAnnotation>().IsVisible;

            var nameMember = a.GetMember("OpenTap.NumberMixinBuilder.Name");
            var enabled = nameMember.Get<IAccessAnnotation>().IsVisible;

            Assert.IsTrue(enabled);
            Assert.IsFalse(enabled1);

            var selectedMixin = builderUi.SelectedItem;
            var mem = selectedMixin.ToDynamicMember();
            DynamicMember.AddDynamicMember(test, mem);
            var td = TypeData.GetTypeData(test);
            var mem2 = td.GetMembers().ToArray();
        }
    }

    public class TestAddingMixinsStep : TestStep
    {
        [Browsable(true)]
        public void Test()
        {
            
        }
        
        
        public override void Run()
        {
            
        }
    }
}