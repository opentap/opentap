using NUnit.Framework;
namespace OpenTap.UnitTests;

public class TestStepNameFormattingTests
{
    class FormatFormattedNameStep : OpenTap.TestStep, IFormatName
    {
        public string ReplaceWith { get; set; } = "123";
        public FormatFormattedNameStep()
        {
            Name = "__CUSTOM__";
        }
        public override void Run()
        {

        }

        public string GetFormattedName()
        {
            return Name.Replace("__CUSTOM__", ReplaceWith);
        }
    }

    [TestCase("<__CUSTOM__>", "123", "<123>")]
    [TestCase("__CUSTOM____CUSTOM__", "A", "AA")]
    [TestCase("55555", "123123", "55555")]
    public void TestICustomNameFormatter(string name, string replace, string expected)
    {
        var fmt = new FormatFormattedNameStep()
        {
            Name = name,
            ReplaceWith = replace
        };
        Assert.AreEqual(expected, fmt.GetFormattedName());
    }

}
