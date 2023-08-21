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
            if(AsStream)
                StepRun.PublishArtifacts(System.IO.File.OpenRead(File), File);
            else
                StepRun.PublishArtifacts(File);
        }
    }


}
