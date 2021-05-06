using System;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    public class EitherTest
    {
        [Test]
        public void TestUnpack()
        {
            int integer = 3;
            double real = 5.5;
            
            var doubleEither = new Either<int, double>(real);
            var integerEither = new Either<int, double>(integer);
            
            Assert.IsFalse(doubleEither.IsLeft);
            Assert.IsTrue(integerEither.IsLeft);
            
            Assert.AreEqual((double) doubleEither.Unpack(), real, 0.1);
            Assert.AreEqual(doubleEither.Unpack<double>(), real, 0.1);
            
            Assert.AreEqual((int) integerEither.Unpack(), integer);
            Assert.AreEqual(integerEither.Unpack<int>(), integer);
            
            var plan = new TestPlan();
            var step = new DelayStep();

            var planEither = new Either<ITestStepParent, ITestStep>(plan);
            var stepEither = new Either<ITestStepParent, ITestStep>(step);
            
            Assert.IsTrue(planEither.IsLeft);
            Assert.IsFalse(stepEither.IsLeft);
            
            Assert.AreSame(planEither.Unpack(), plan);
            Assert.AreSame(stepEither.Unpack(), step);
        }

        enum TestEnum1
        {
            One = 1,
            Two,
            Three
        }

        enum TestEnum2
        {
            Four = 4,
            Five,
            Six
        }

        [Test]
        public void TestEnumUnpack()
        {
            var either1 = new Either<TestEnum1, TestEnum2>(TestEnum1.One);
            var either4 = new Either<TestEnum1, TestEnum2>(TestEnum2.Four);
            
            Assert.AreEqual(either1.Unpack<int>(), (int) TestEnum1.One);
            Assert.AreEqual(either4.Unpack<int>(), (int) TestEnum2.Four);
            
            Assert.IsTrue(either1.IsLeft);
            Assert.IsFalse(either4.IsLeft);

            Assert.AreEqual(new Either<TestEnum1, TestEnum2>(TestEnum1.Two).Match(v => v, null), TestEnum1.Two);
            Assert.AreEqual(new Either<TestEnum1, TestEnum2>(TestEnum2.Five).Match(null,v => v), TestEnum2.Five);

            Assert.Throws<NullReferenceException>(() => either1.Match<object>(null, null));

            {
                var a = either1.Left;
                Assert.Throws<Exception>(() =>
                {
                    var b = either1.Right;
                });
            }
            {
                var a = either4.Right;
                Assert.Throws<Exception>(() =>
                {
                    var b = either4.Left;
                });
            }
            
            var eitherMix = new Either<Enum, int>(6);
            var eitherMix2 = new Either<Enum, int>(TestEnum2.Six);
            
            Assert.AreEqual(eitherMix.Unpack<int>(), eitherMix2.Unpack<int>());
        }
    }
}
