using System;
using System.Collections.Generic;
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
        
        
        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            base.OnTestPlanRunStart(planRun);
            var fileName = GetTmpFileName(planRun);
            var fstr = File.OpenWrite(fileName);
            using var archive = new ZipArchive(fstr, ZipArchiveMode.Create, false);    
        }

        public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
        {
            base.OnTestPlanRunCompleted(planRun, logStream);
            
            var fileName = GetTmpFileName(planRun);
            
            var newName = ZipFile.Expand(planRun);
            try
            {
                FileSystemHelper.EnsureDirectoryOf(newName);
                File.Move(fileName, newName);
                
            }
            catch (IOException e)
            {
                if (e.Message.Contains("already exists"))
                {
                    // try finding another name for the file.
                    var ext = Path.GetExtension(newName);
                    if (ext == null)
                    {
                        for (int i = 2; i < 20; i++)
                        {
                            var name2 = $"{newName}.{i}";
                            if (!File.Exists(name2))
                            {
                                File.Move(fileName, name2);
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (int i = 2; i < 20; i++)
                        {
                            var name2 = Path.ChangeExtension(newName, $"{i}{ext}");
                            if (!File.Exists(name2))
                            {
                                File.Move(fileName, name2);
                                newName = name2;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    throw;
                }
            }
            this.Log.Debug("Wrote file: {0}", Path.GetFullPath(newName));
            
            planRun.PublishArtifacts(File.OpenRead(newName), Path.GetFileName(newName));
        }

        readonly Dictionary<Guid, Guid> parentMap = new Dictionary<Guid, Guid>();
        
        public override void OnTestStepRunStart(TestStepRun stepRun)
        {
            base.OnTestStepRunStart(stepRun);
            parentMap.Add(stepRun.Id, stepRun.Parent);
        }

        public override void OnTestStepRunCompleted(TestStepRun stepRun)
        {
            base.OnTestStepRunCompleted(stepRun);
            parentMap.Remove(stepRun.Id);
        }

        Guid GetPlanRunId(Guid runId)
        {
            while (parentMap.TryGetValue(runId, out var runId2))
            {
                runId = runId2;
            }
            return runId;
        }

        string GetTmpFileName(TestRun run)
        {
            var id = GetPlanRunId(run.Id);
            var name = $"tmp.artifacts.{id}.zip";
            return name;
        }
        
        public void OnArtifactPublished(TestRun run, Stream artifactStream, string artifactName)
        {
            var fileName = GetTmpFileName(run);
            if (!File.Exists(fileName))
                return; // The file has been finalized.
            
            var rawStream = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
            using var archive = new ZipArchive(rawStream, ZipArchiveMode.Update, false);
            
            var entry = archive.CreateEntry(artifactName);
            using var s2 = entry.Open();
            artifactStream.CopyTo(s2);
            artifactStream.Close();
        }
    }
}
