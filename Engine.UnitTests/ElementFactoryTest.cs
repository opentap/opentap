using System.Collections.Generic;
using NUnit.Framework;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class ElementFactoryTest
    {
        public class ClassWithComplexList
        {
            public class ComplexElement
            {
                readonly ClassWithComplexList Parent;
                
                internal ComplexElement(ClassWithComplexList parent)
                {
                    this.Parent = parent;
                }
            }

            public class ComplexList : List<ComplexElement>
            {
                readonly ClassWithComplexList parent;
                public ComplexList(ClassWithComplexList lst)
                {
                    this.parent = lst;
                }
            }

            [Factory(nameof(NewElement))]
            public List<ComplexElement> Items { get; set; } = new List<ComplexElement>();
            
            [ElementFactory(nameof(NewElement))]
            [Factory(nameof(NewComplexList))]
            public ComplexList Items2 { get; set; }

            internal ComplexElement NewElement()
            {
                return new ComplexElement(this);
            }

            internal ComplexList NewComplexList()
            {
                return new ComplexList(this);
            }
            
            public ClassWithComplexList()
            {
                Items2 = new ComplexList(this);
            }

        }

        [Test]
        public void SerializeWithElementFactory()
        {
        
            var test = new ClassWithComplexList();
            test.Items.Add(test.NewElement());
            test.Items2.Add(test.NewElement());

            var xml = new TapSerializer().SerializeToString(test);

            var test2 =(ClassWithComplexList) new TapSerializer().DeserializeFromString(xml);
            Assert.AreEqual(1, test2.Items2.Count);
            Assert.IsNotNull(test2.Items2[0]);
            Assert.AreEqual(1, test2.Items.Count);
            Assert.IsNotNull(test2.Items[0]);
        }

        [Test]
        public void AnnotateWithElementFactory()
        {
            var test = new ClassWithComplexList();
            var a = AnnotationCollection.Annotate(test);
            var complexItems = a.GetMember(nameof(test.Items2));
            var collection = complexItems.Get<ICollectionAnnotation>();
            collection.AnnotatedElements = collection.AnnotatedElements.Append(collection.NewElement());
            int preCount = test.Items2.Count;
            a.Write();
            int postCount = test.Items2.Count;
            
            Assert.AreEqual(0, preCount);
            Assert.AreEqual(1, postCount);

        }

    }
}