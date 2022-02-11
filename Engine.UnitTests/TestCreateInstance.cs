using System;
using NUnit.Framework;

namespace OpenTap.UnitTests
{
    public class ParameterlessConstructor
    {
    }
    public class ConstructorWithDefaultValues
    {
        public ConstructorWithDefaultValues(int a = 1)
        {
            // This should be instantiated with the default value
            Assert.AreEqual(1, a);
        }
    }
    public class ConstructorWithSomeDefaultValues
    {
        public ConstructorWithSomeDefaultValues(string s, int a = 1)
        {
            // This should not be possible to instantiate
            Assert.Fail();
        }
    }

    public class ConstructorWithoutDefaultValues
    {
        public ConstructorWithoutDefaultValues(int a)
        {
            // This should not be possible to instantiate
            Assert.Fail();
        }
    }

    public class MultipleValidConstructors
    {
        public MultipleValidConstructors()
        {

        }

        public MultipleValidConstructors(string s = "123")
        {
            // We should resolve the parameterless constructor first
            Assert.Fail();
        }
    }

    public class ConstructorWithNullAsDefault
    {
        public ConstructorWithNullAsDefault(ConstructorWithNullAsDefault a = null)
        {
            // We should be able to construct this type with 'null' as the argument
            Assert.IsNull(a);
        }
    }


    [TestFixture]
    public class TestCreateInstance
    {
        [Test]
        public void TestCreateInstanceScenarios()
        {
            void testConstructor<T>(bool expected)
            {
                var td = TypeData.FromType(typeof(T));
                if (expected)
                {
                    Assert.IsTrue(td.CanCreateInstance);
                    Assert.NotNull(td.CreateInstance());
                }
                else
                {
                    Assert.IsFalse(td.CanCreateInstance);
                    Assert.Throws<MissingMethodException>(() => td.CreateInstance());
                }
            }

            testConstructor<ParameterlessConstructor>(true);
            testConstructor<ConstructorWithDefaultValues>(true);
            testConstructor<MultipleValidConstructors>(true);
            testConstructor<ConstructorWithNullAsDefault>(true);
            testConstructor<ConstructorWithSomeDefaultValues>(false);
            testConstructor<ConstructorWithoutDefaultValues>(false);
        }
    }
}