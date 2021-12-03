using System;
using System.Linq;
using NUnit.Framework;
using OpenTap.MemberDataPlugin;

namespace OpenTap.UnitTests
{
    public class TestMembers
    {
        public const float defaultValue = 12.34f;

        internal static readonly ProvidedMember Cardinality = ProvidedMember.Register<float>(() => defaultValue)
            .WithAttributes(new AvailableValuesAttribute(nameof(Cardinalities)));

        internal static readonly ProvidedMember Cardinalities =
            ProvidedMember.Register<float[]>(() => new float[] { 1, 2, 3, 4 });
    }

    public class TestMemberProvider : IMemberProvider
    {
        public ITypeData[] SupportedTypes { get; } = new ITypeData[] { TypeData.FromType(typeof(TestPlan)) };
        public ProvidedMember[] GetMembers(object owner)
        {
            if (owner is TestPlan tp)
            {
                return new ProvidedMember[]
                {
                    TestMembers.Cardinality,
                    TestMembers.Cardinalities
                };
            }

            return Array.Empty<ProvidedMember>();
        }
    }

    [TestFixture]
    public class MemberDataPluginTest
    {
        [Test]
        public void TestSetValue()
        {
            var plan1 = new TestPlan();
            var plan2 = new TestPlan();
            { // Test get value
                Assert.AreEqual(TestMembers.Cardinality.GetValue(plan1),
                    TestMembers.defaultValue);
                Assert.AreEqual(TestMembers.Cardinality.GetValue(plan2),
                    TestMembers.defaultValue);
            }
            { // Test set value
                var newValue = 5f;
                TestMembers.Cardinality.SetValue(plan1, newValue);
                Assert.AreEqual(TestMembers.Cardinality.GetValue(plan1),
                    newValue);
                // Ensure setting the value of plan1 does not affect plan2
                Assert.AreEqual(TestMembers.Cardinality.GetValue(plan2),
                    TestMembers.defaultValue);
            }

            { // Test available values
                var a = AnnotationCollection.Annotate(plan1);
                var mem = a.Get<IMembersAnnotation>().Members.ToLookup(m => m.Get<IMemberAnnotation>().Member.Name);
                var cardinality = mem[nameof(TestMembers.Cardinality)].First();
                var avail = cardinality.Get<IAvailableValuesAnnotation>().AvailableValues;
                CollectionAssert.AreEqual(new float[] { 1, 2, 3, 4 }, avail);

                var cardinalities = mem[nameof(TestMembers.Cardinalities)].First();
                Assert.AreEqual(avail, cardinalities.Get<IObjectValueAnnotation>().Value);
            }
        }
    }
}