
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

    [Display("Create Directory", Description: "Creates a new directory.", Group: "Tests")]
    public class CreateDirectoryStep : TestStep
    {
        public string Directory { get; set; }
        public override void Run()
        {
            System.IO.Directory.CreateDirectory(Directory);
        }
    }

    [Display("Replace In File", Description: "Replaces some text in a file.", Group: "Tests")]
    public class ReplaceInFileStep : TestStep
    {
        [FilePath]
        [Display("File", Order: 0)]
        public string File { get; set;}
        [Layout(LayoutMode.Normal, rowHeight: 5)]
        [Display("Search For", Order: 1)]
        public string Search { get; set; }
        [Layout(LayoutMode.Normal, rowHeight: 5)]
        [Display("Replace With", Order: 2)]
        public string Replace { get; set; }

        public override void Run()
        {
            var content = System.IO.File.ReadAllText(File);
            content = content.Replace(Search, Replace);
            System.IO.File.WriteAllText(File, content);
        }
    }

    [Display("Expect", Description: "Expects  verdict in the child step.", Group: "Tests")]
    [AllowAnyChild]
    public class ExpectStep : TestStep
    {
        public Verdict ExpectedVerdict { get; set; }
        public override void Run()
        {
            RunChildSteps();
            if (Verdict == ExpectedVerdict)
                Verdict = Verdict.Pass;
            else
            {
                Verdict = Verdict.Fail;
            }
        }
    }
    
}