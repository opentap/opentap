using System.IO;
namespace OpenTap.Engine.UnitTests
{
    [Display("Artifact Step", Group: "Test")]
    public class ArtifactStep : TestStep
    {
        [FilePath]
        public string File { set; get; }
        
        [Display("As Stream")]
        public bool AsStream { get; set; }
        public override void Run()
        {
            if (AsStream)
            {
                var bytes = System.IO.File.ReadAllBytes(File);
                StepRun.PublishArtifact(new MemoryStream(bytes), File);
            }
            else
            {
                StepRun.PublishArtifact(File);
            }
        }
    }


}
