
using System.Linq;
using NUnit.Framework;
namespace OpenTap.UnitTests
{
    [TestFixture]
    public class TypeFilterTest
    {
        public class StepWithTypeFilter : TestStep
        {
            public interface A: IInstrument
            {

            }

            public interface B : IInstrument
            {

            }

            public class SpecialResource1 : Instrument, A
            {

            }

            public class SpecialResource2 : Instrument, A, B
            {

            }

            [TypeFilter(typeof(B))]
            public A Resource1 { get; set; }
            [TypeFilter(typeof(A))]
            public B Resource2 { get; set; }
            public A Resource3 { get; set; }

            public override void Run()
            {
                throw new System.NotImplementedException();
            }
        }

        /// <summary>
        /// This tests that the TypeFilterAttribute works as intended. 
        /// </summary>
        [Test]
        public void TestTypeFilterSetting()
        {
            using var session = Session.Create();
            
            // setup the environment
            var res1 = new StepWithTypeFilter.SpecialResource1();
            var res2 = new StepWithTypeFilter.SpecialResource2();
            InstrumentSettings.Current.AddRange(new IInstrument[] {res1, res2});

            
            var step = new StepWithTypeFilter();
            
            
            // gather data about which values are available for the resources settings of step
            var a = AnnotationCollection.Annotate(step);
            var resource1 = a.GetMember(nameof(step.Resource1));
            var resource2 = a.GetMember(nameof(step.Resource2));
            var resource3 = a.GetMember(nameof(step.Resource3));
            var resource1Available = resource1.Get<IAvailableValuesAnnotation>().AvailableValues;
            var resource2Available = resource2.Get<IAvailableValuesAnnotation>().AvailableValues;
            var resource3Available = resource3.Get<IAvailableValuesAnnotation>().AvailableValues;
            
            // for Resource1/2 only res2 works. for Resource3, both works.
            Assert.AreEqual(res2, step.Resource1);
            Assert.AreEqual(res2, step.Resource2);
            Assert.IsTrue(res1 == step.Resource3 || res2 == step.Resource3);
            Assert.IsTrue(resource1Available.Cast<object>().SequenceEqual(new []{res2}));
            Assert.IsTrue(resource2Available.Cast<object>().SequenceEqual(new []{res2}));
            Assert.IsTrue(resource3Available.Cast<object>().ToHashSet().SetEquals(new object[]{res1, res2}));
        }
    }
}
