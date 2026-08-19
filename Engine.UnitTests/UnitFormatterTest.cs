using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
namespace OpenTap.UnitTests;

public class UnitFormatterTest
{

    [TestCase("", "0x", 10, "0xa")]
    [TestCase("", "0X", 10, "0xA")]
    [TestCase("", "0x8", 10, "0x0000000a")]
    [TestCase("", "0X8", 10, "0x0000000A")]
    [TestCase("", "0x16", 10, "0x000000000000000a")]
    [TestCase("", "0X16", 10, "0x000000000000000A")]
    public void TestFormats(string unit, string format, object value, string expected)
    {
        var result = UnitFormatter.Format(BigFloat.Convert(value), false, unit, format, CultureInfo.InvariantCulture);
        Assert.AreEqual(expected, result);
        BigFloat flt = UnitFormatter.Parse(result, unit, format, CultureInfo.InvariantCulture);
        var result2 = flt.ConvertTo(value.GetType());
        Assert.AreEqual(value, result2);
    }

    [TestCase("10000.1", 10000.1)]
    [TestCase("0.1", 0.1)]
    [TestCase("0.1111111111", 0.1111111111)]
    [TestCase("-10000.1", -10000.1)]
    [TestCase("-99999.1", -99999.1)]
    [TestCase("-1000000000000.1000000000000", -1000000000000.1000000000000)]
    public void TestBigFloat(string strValue, double approxDouble)
    {
        var bf = UnitFormatter.Parse(strValue, "", "", CultureInfo.InvariantCulture);
        var result = (double)bf.ConvertTo(typeof(double));
        Assert.AreEqual(approxDouble, result, Math.Abs(approxDouble) * 0.00001);
    }

    /// <summary>
    /// Converting a BigFloat to a double must round to the nearest double, exactly like double.Parse does.
    /// Issue #2433: it used to truncate, so e.g. double.MaxValue lost its last bit of precision.
    /// </summary>
    [TestCase("1.7976931348623157E+308", double.MaxValue)]
    [TestCase("-1.7976931348623157E+308", double.MinValue)]
    [TestCase("5E-324", double.Epsilon)]
    [TestCase("-5E-324", -double.Epsilon)]
    [TestCase("1E-308", 1e-308)]
    [TestCase("2.2250738585072014E-308", 2.2250738585072014E-308)] // smallest normal double.
    [TestCase("2.225073858507201E-308", 2.225073858507201E-308)] // largest subnormal double.
    [TestCase("123456789012345.67", 123456789012345.67)]
    [TestCase("9007199254740992", 9007199254740992.0)] // 2^53, the largest exactly representable integer.
    [TestCase("9007199254740993", 9007199254740992.0)] // 2^53+1, a tie that must round down to even.
    [TestCase("9007199254740995", 9007199254740996.0)] // 2^53+3, a tie that must round up to even.
    [TestCase("0.1", 0.1)]
    [TestCase("0", 0.0)]
    [TestCase("1", 1.0)]
    [TestCase("-1", -1.0)]
    [TestCase("1E400", double.PositiveInfinity)] // overflow.
    [TestCase("-1E400", double.NegativeInfinity)]
    [TestCase("1E-400", 0.0)] // underflow.
    public void TestBigFloatToDouble(string strValue, double expected)
    {
        var bf = new BigFloat(strValue, CultureInfo.InvariantCulture);
        Assert.AreEqual(expected, bf.ToDouble());
        Assert.AreEqual(expected, (double)bf);
        Assert.AreEqual(expected, bf.ConvertTo(typeof(double)));
    }

    /// <summary> Any double must survive a BigFloat round trip unchanged. Issue #2433. </summary>
    [Test]
    public void TestDoubleBigFloatRoundTrip()
    {
        var rnd = new Random(2433);
        var buffer = new byte[8];
        for (int i = 0; i < 10000; i++)
        {
            rnd.NextBytes(buffer);
            var value = BitConverter.ToDouble(buffer, 0);
            if (double.IsNaN(value) || double.IsInfinity(value)) continue;
            var result = BigFloat.Convert(value).ToDouble();
            if (result != value)
                Assert.Fail($"{value:R} became {result:R}.");
        }
    }

    [TestCase("4,5,6", null)]
    [TestCase("1:5", "1,2,3,4,5")]
    [TestCase("1:2:5", "1,3,5")]
    [TestCase("5:-1:1", "5,4,3,2,1")]
    public void TestParseSequence(string sequence, string expected)
    {
        if (expected == null) expected = sequence;
        var values = expected.Split(",").Select(double.Parse).ToArray();
        var parser = new NumberFormatter(CultureInfo.InvariantCulture);
        var values2 = parser.Parse(sequence);
        Assert.IsTrue(values2.SequenceEqual(values));
    }
    
    [Test]
    public void TestDecimal()
    {
        string decimalValueString = "53634563090899.906123456789012";
        var decimalValue = decimal.Parse(decimalValueString, CultureInfo.InvariantCulture);
        var fmt = new NumberFormatter(CultureInfo.InvariantCulture);
        var strVal = fmt.FormatNumber(decimalValue);
        var decimalValueParsed = (decimal)fmt.ParseNumber(strVal, typeof(decimal));
        Assert.AreEqual(decimalValueParsed, decimalValue);
        Assert.AreEqual(decimalValueString, strVal);
    }
    
}

public class StepWithHexProperties : TestStep
{

    [Unit("", StringFormat: "0x")]
    public uint X { get; set; } = 0xAABBAABB;
    [Unit("", StringFormat: "0X")]
    public int X2 { get; set; } = 0x0ABBAABB;
    [Unit("", StringFormat:"X")]
    public int X3 { get; set; }= 0x0ABBAABB;
    [Unit("", StringFormat:"X8")]
    public int X4 { get; set; }= 0x0ABBAABB;
    [Unit("", StringFormat:"0x8")]
    public int X5 { get; set; }= 0x0ABBAABB;
    [Unit("", StringFormat:"0X8")]
    public int X6 { get; set; }= 0x0ABBAABB;

    [Unit("", StringFormat:"0x16")]
    public ulong X7 { get; set; }= 0x0ABBAABB;
    [Unit("", StringFormat:"0X16")]
    public ulong X8 { get; set; }= 0x0ABBAABB;

    public override void Run()
    {

    }
}
