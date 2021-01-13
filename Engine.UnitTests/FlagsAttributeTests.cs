using NUnit.Framework;
using System;
using System.Globalization;
using System.Linq;
using static OpenTap.UnitTests.FlagsAttributeTestStep;

namespace OpenTap.UnitTests
{
    public class FlagsAttributeTestStep : TestStep
    {
        [Flags]
        public enum NonPowers2Modes
        {
            None=0,
            A = 1,
            B = 2,
            AB = A|B,
            C = 4,
            AC = A|C,
            BC = B|C,
            ABC = A|B|C,
            D = 8,
            AD = A|D,
            BD = B|D,
            ABD = A|B|D,
            CD = C|D,
            ACD = A|C|D,
            BCD = B|C|D,
            All = A|B|C|D
        }

        [Flags]
        public enum Powers2Modes
        {
            None = 0,
            [Display("A-Name")]
            A = 1,
            B = 2,
            [Display("C-Name")]
            C = 4,
            D = 8
        }
        
        [Flags]
        public enum LargeFlagValues : long
        {
            None = 0,
            // shortA aliases with LongA in 32 bit systems
            ShortA = 0x0000_00FF, 
            LongA = 0x0000_00FF_0000_0000L,
            [Display("Long Long B")]
            LongB = 1L << 60,
        }

        #region Settings

        public NonPowers2Modes MyNonPowers2Mode { get; set; }
        public Powers2Modes MyPowers2Mode { get; set; }
        public LargeFlagValues MyLargeFlagValues { get; set; }

        #endregion
        public override void Run() { }
    }

    [TestFixture]
    public class FlagsEnumTest
    {
        [Test]
        public void NonPowers2EnumTest()
        {
            var flagStep = new FlagsAttributeTestStep();
            var stepAnnotation = AnnotationCollection.Annotate(flagStep);
            var mem = stepAnnotation.GetMember(nameof(FlagsAttributeTestStep.MyNonPowers2Mode));
            var select = mem.Get<IMultiSelect>();

            Assert.True(select.Selected.Cast<Enum>().Contains(NonPowers2Modes.None));
            Assert.AreEqual(select.Selected.Cast<Enum>().Count(), 1);

            flagStep.MyNonPowers2Mode = NonPowers2Modes.AC;
            Assert.True(select.Selected.Cast<Enum>().Contains(NonPowers2Modes.A | NonPowers2Modes.C | NonPowers2Modes.AC));
            Assert.AreEqual(select.Selected.Cast<Enum>().Count(), 3);

            flagStep.MyNonPowers2Mode = NonPowers2Modes.BC;
            Assert.False(select.Selected.Cast<Enum>().Contains(NonPowers2Modes.A)); // A should be unselected
            Assert.True(select.Selected.Cast<Enum>().Contains(NonPowers2Modes.B | NonPowers2Modes.C | NonPowers2Modes.BC));
            Assert.AreEqual(select.Selected.Cast<Enum>().Count(), 3);

            flagStep.MyNonPowers2Mode |= NonPowers2Modes.D;
            Assert.True(select.Selected.Cast<Enum>().Contains(NonPowers2Modes.D | NonPowers2Modes.B | NonPowers2Modes.C | NonPowers2Modes.BC));
            Assert.True(select.Selected.Cast<Enum>().Contains(NonPowers2Modes.BD | NonPowers2Modes.CD | NonPowers2Modes.BCD));
            Assert.AreEqual(select.Selected.Cast<Enum>().Count(), 7);

            // Unselection
            flagStep.MyNonPowers2Mode ^= NonPowers2Modes.B;  // Remove B from previous selection
            Assert.True(select.Selected.Cast<Enum>().Contains(NonPowers2Modes.D | NonPowers2Modes.C | NonPowers2Modes.CD));
            Assert.AreEqual(select.Selected.Cast<Enum>().Count(), 3);

            //Select All
            flagStep.MyNonPowers2Mode = NonPowers2Modes.All;
            Assert.False(select.Selected.Cast<Enum>().Contains(NonPowers2Modes.None));
            Assert.AreEqual(select.Selected.Cast<Enum>().Count(), 15);
        }

        [Test]
        public void Powers2EnumTest()
        {
            var flagStep = new FlagsAttributeTestStep();
            var stepAnnotation = AnnotationCollection.Annotate(flagStep);
            var mem = stepAnnotation.GetMember(nameof(FlagsAttributeTestStep.MyPowers2Mode));
            var select = mem.Get<IMultiSelect>();

            Assert.True(select.Selected.Cast<Enum>().Contains(Powers2Modes.None));
            Assert.AreEqual(select.Selected.Cast<Enum>().Count(), 1);

            flagStep.MyPowers2Mode = Powers2Modes.C;
            Assert.True(select.Selected.Cast<Enum>().Contains(Powers2Modes.C));
            Assert.AreEqual(select.Selected.Cast<Enum>().Count(), 1);

            flagStep.MyPowers2Mode |= Powers2Modes.D;
            Assert.True(select.Selected.Cast<Enum>().Contains(Powers2Modes.C));
            Assert.True(select.Selected.Cast<Enum>().Contains(Powers2Modes.D));
            Assert.AreEqual(select.Selected.Cast<Enum>().Count(), 2);
            Assert.False(select.Selected.Cast<Enum>().Contains(Powers2Modes.C | Powers2Modes.D));    // C|D is not a defined enum constant

            flagStep.MyPowers2Mode = Powers2Modes.A;
            Assert.True(select.Selected.Cast<Enum>().Contains(Powers2Modes.A));
            Assert.AreEqual(select.Selected.Cast<Enum>().Count(), 1);
        }

        [Test]
        public void LargeFlagValuesTest()
        {
            var flagStep = new FlagsAttributeTestStep();
            var stepAnnotation = AnnotationCollection.Annotate(flagStep);
            var mem = stepAnnotation.GetMember(nameof(FlagsAttributeTestStep.MyLargeFlagValues));
            var select = mem.Get<IMultiSelect>();

            Assert.True(select.Selected.Cast<Enum>().Contains(LargeFlagValues.None));
            Assert.AreEqual(select.Selected.Cast<Enum>().Count(), 1);
            
            flagStep.MyLargeFlagValues = LargeFlagValues.ShortA;
            Assert.True(select.Selected.Cast<Enum>().Contains(LargeFlagValues.ShortA));
            Assert.AreEqual(select.Selected.Cast<Enum>().Count(), 1);
            
            flagStep.MyLargeFlagValues = LargeFlagValues.LongA;
            Assert.True(select.Selected.Cast<Enum>().Contains(LargeFlagValues.LongA));
            Assert.AreEqual(select.Selected.Cast<Enum>().Count(), 1);

            flagStep.MyLargeFlagValues = LargeFlagValues.LongB;
            Assert.True(select.Selected.Cast<Enum>().Contains(LargeFlagValues.LongB));
            Assert.AreEqual(select.Selected.Cast<Enum>().Count(), 1);
            
            flagStep.MyLargeFlagValues |= LargeFlagValues.LongA;
            Assert.True(select.Selected.Cast<Enum>().Contains(LargeFlagValues.LongB) &&
                        select.Selected.Cast<Enum>().Contains(LargeFlagValues.LongA));
            Assert.AreEqual(select.Selected.Cast<Enum>().Count(), 2);
            
            flagStep.MyLargeFlagValues = LargeFlagValues.ShortA | LargeFlagValues.LongA;
            Assert.True(select.Selected.Cast<Enum>().Contains(LargeFlagValues.ShortA));
            Assert.True(select.Selected.Cast<Enum>().Contains(LargeFlagValues.LongA));
            Assert.AreEqual(select.Selected.Cast<Enum>().Count(), 2);
            
        }

        [Test]
        public void EnumReadableStringTest()
        {
            void testConversions(string a, Enum b)
            {
                Assert.AreEqual(a, Utils.EnumToReadableString(b));
                var c = (Enum)StringConvertProvider.FromString(a, TypeData.FromType(b.GetType()), null, CultureInfo.InvariantCulture);
                Assert.AreEqual(c, b);
            }

            testConversions("None", (Powers2Modes)0);
            
            testConversions("A-Name", Powers2Modes.A);
            testConversions("A-Name", (Powers2Modes)1);
            
            testConversions("B", Powers2Modes.B);
            testConversions("B", (Powers2Modes)2);
            
            testConversions("A-Name | B", Powers2Modes.A | Powers2Modes.B);
            testConversions("A-Name | B", (Powers2Modes)3);
            
            testConversions("A-Name | B | C-Name", Powers2Modes.A | Powers2Modes.B | Powers2Modes.C);
            testConversions("A-Name | B | C-Name", (Powers2Modes)7);
            
            testConversions("None", (LargeFlagValues)0);
            
            testConversions("ShortA", LargeFlagValues.ShortA);
            testConversions("LongA", LargeFlagValues.LongA);
            testConversions("Long Long B", LargeFlagValues.LongB);
            testConversions("LongA | Long Long B", LargeFlagValues.LongA | LargeFlagValues.LongB);

            Assert.AreEqual("16", Utils.EnumToReadableString((Powers2Modes)16));
            Assert.AreEqual("32", Utils.EnumToReadableString((Powers2Modes)32));
            Assert.AreEqual("16", Utils.EnumToReadableString((LargeFlagValues)16));
        }
    }
}
