
namespace OpenTap.Engine.UnitTests.TestTestSteps
{
    [Display("Write File", Description: "Writes a string to a file.", Group: "Tests")]
    public class WriteFileStep : TestStep
    {
        [Layout(LayoutMode.Normal, rowHeight: 5)]
        public string String { get; set; }
        public string File { get; set; }
        public override void Run() => System.IO.File.WriteAllText(File, String);
    }
}