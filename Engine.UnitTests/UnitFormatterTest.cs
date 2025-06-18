using System.Globalization;
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
