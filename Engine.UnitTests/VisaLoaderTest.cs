using System;
using NUnit.Framework;

namespace OpenTap.UnitTests;

[TestFixture]
public class VisaLoaderTest
{
    [Test]
    public void DummyVisaTest()
    {
        DummyVisaLoader.StaticOrder = 1;
        Assert.That(Visa.viClear(0), Is.EqualTo(123));
        try 
        {
            Visa.viWaitOnEvent(0, 0, 0, out var evt, IntPtr.Zero);
            Assert.Fail("This should have thrown");
        }
        catch (NotSupportedException ex)
        {
            Assert.That(ex.Message, Is.EqualTo("DummyVisaLoader does not support viWaitOnEvent"));
        }
    }

    public class DummyVisaLoader : IVisaFunctionLoader
    {
        public override string ToString()
        {
            return nameof(DummyVisaLoader);
        }
        // Set static order in unittest to avoid loading this
        // implementation in debug builds
        public static double StaticOrder = 1000000;
        public double Order => StaticOrder;

        public VisaFunctions? Functions => loadFunctions();

        private VisaFunctions loadFunctions()
        {
            VisaFunctions functions = new();
            functions.ViClearRef = static _ => 123;
            return functions;
        }
    }
}

