using System;
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
        
        public bool Rename { get; set; }
        public string RenameTo { get; set; }
        
        public override void Run()
        {
            if (AsStream)
            {
                if (Rename && string.IsNullOrWhiteSpace(RenameTo))
                    throw new InvalidOperationException("Cannot rename to nothing.");
                    
                var bytes = System.IO.File.ReadAllBytes(File);
                StepRun.PublishArtifact(new MemoryStream(bytes), Rename ? RenameTo : File);
            }
            else
            {
                StepRun.PublishArtifact(File);
            }
        }
    }


}
