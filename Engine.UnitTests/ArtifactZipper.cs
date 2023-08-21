using System;
using System.IO;
using System.IO.Compression;
using OpenTap.Plugins;
namespace OpenTap.Engine.UnitTests
{
    [Display("Artifacts Zip")]
    public class ArtifactZipper : ResultListener, IArtifactListener
    {
        [FilePath]
        public MacroString ZipFile { get; set; } = new MacroString()
        {
            Text = "<Date>.zip"
        };

        public ArtifactZipper()
        {
            Name = "Zip";
        }
        
        ZipArchive archive;
        string name = "";
        public override void Open()
        {
            base.Open();

            var id = Guid.NewGuid();
            var fstr = File.OpenWrite(name = $"tmp.artifacts.{id}.zip");
            archive = new ZipArchive(fstr, ZipArchiveMode.Create, false);
        }


        public override void Close()
        {
            
            base.Close();
        }

        public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
        {
            base.OnTestPlanRunCompleted(planRun, logStream);
            
            archive.Dispose();
            archive = null;
            var newName = ZipFile.Expand(planRun);
            File.Move(name,newName);
            this.Log.Debug("Wrote file: {0}", Path.GetFullPath(newName));
            
            planRun.PublishArtifacts(newName);
        }

        public void OnArtifactPublished(TestRun run, Stream s, string filename)
        {
            if (archive == null) return;
            var entry = archive.CreateEntry(filename);
            using var s2 = entry.Open();
            s.CopyTo(s2);
            s.Close();
        }
        public void OnArtifactPublished(TestRun run, string filepath)
        {
            if (archive == null) return;
            using var s = File.OpenRead(filepath);
            var entry = archive.CreateEntry(Path.GetFileName(filepath));
            using var s2 = entry.Open();
            s.CopyTo(s2);
            s.Close();
        }
    }
}
