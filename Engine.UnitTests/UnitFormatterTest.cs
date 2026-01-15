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
    [Test]
    public void TestBigFloatPerf()
    {
        var culture = CultureInfo.InvariantCulture;
        for (int i = 0; i < 100000000; i++)
        {
            new BigFloat("123456123456", culture);
        }
    }
    
    [Test]
    public void TestBigFloatPerf2()
    {
        var culture = CultureInfo.InvariantCulture;
        for (int i = 0; i < 100000000; i++)
        {
            UnitFormatter.Parse("123456123456", "", "",culture );
        }
    }
    [Test]
    public void TestBigFloatsequencePerf()
    {
        var culture = CultureInfo.InvariantCulture;
        var parser = new NumberFormatter(culture);
        var strToParse = string.Join(",", Enumerable.Range(0, 100));
        for (int i = 0; i < 100000; i++)
        {
            var parseRange = parser.Parse(strToParse);
        }
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
