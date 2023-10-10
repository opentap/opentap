//Copyright 2012-2023 Keysight Technologies
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace OpenTap.Plugins.PluginDevelopment
{
    // This example shows how to implement IArtifactListener
    // to make a result listener which can zip artifacts from the test plan run.
    [Display("Artifacts Zip")]
    public class ZipArtifactsResultListener : ResultListener, IArtifactListener
    {
        // Let the user select where to save the file.
        [FilePath]
        public MacroString ZipFile { get; set; } = new MacroString
        {
            Text = "<Date>.zip"
        };

        public ZipArtifactsResultListener()
        {
            Name = "Zip";
        }

        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            // when the test plan starts, create the archive and close the stream.
            // after that the file will exist.
            // We will be opening and closing the file to insert items as needed.

            base.OnTestPlanRunStart(planRun);
            var fileName = GetTmpFileName(planRun);
            var fstr = File.OpenWrite(fileName);
            new ZipArchive(fstr, ZipArchiveMode.Create, false).Dispose();
        }

        public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
        {
            // When the test plan has completed, we can calculate the final name of it
            // and save it in the final location
            // and in addition publish it as an artifact.

            base.OnTestPlanRunCompleted(planRun, logStream);

            var fileName = GetTmpFileName(planRun);

            var finalName = ZipFile.Expand(planRun);
            try
            {
                // ensure that the folder exists before moving the file.
                var selectedFolder = Path.GetDirectoryName(Path.GetFullPath(finalName));
                if (string.IsNullOrEmpty(selectedFolder) == false)
                    Directory.CreateDirectory(selectedFolder);

                File.Move(fileName, finalName);
            }
            catch (IOException e)
            {
                if (e.Message.Contains("already exists"))
                {
                    // try finding another name for the file.
                    var ext = Path.GetExtension(finalName);
                    if (ext == null)
                    {
                        for (int i = 2; i < 20; i++)
                        {
                            var name2 = $"{finalName}.{i}";
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
                            var name2 = Path.ChangeExtension(finalName, $"{i}{ext}");
                            if (!File.Exists(name2))
                            {
                                File.Move(fileName, name2);
                                finalName = name2;
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
            this.Log.Debug("Wrote file: {0}", Path.GetFullPath(finalName));

            // finally publish the artifact.
            planRun.PublishArtifact(File.OpenRead(finalName), Path.GetFileName(finalName));
        }

        // this map is used to keep track of the parent-child association between step runs and plan runs.
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
            // get the plan run id of any step run, based on the id.
            // this assumes that the run id comes from the plan
            // and that the test plan run id is the topmost (has no parent).
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

        // this is the interface required by IArtifactListener
        // assume the artifactStream is a stream of data and artifactName is the name of the artifact
        // including possible extensions like 'png' or 'csv'.
        public void OnArtifactPublished(TestRun run, Stream artifactStream, string artifactName)
        {
            try
            {
                var fileName = GetTmpFileName(run);
                if (!File.Exists(fileName))
                    return; // The means file has been finalized (or something went wrong). After this we don't insert new data.

                // insert the object data into the zip file.
                var rawStream = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
                using (var archive = new ZipArchive(rawStream, ZipArchiveMode.Update, false))
                {

                    var entry = archive.CreateEntry(artifactName);
                    using (var s2 = entry.Open())
                    {
                        artifactStream.CopyTo(s2);
                    }
                }
            }
            finally
            {

                artifactStream.Close();
            }
        }
    }
}
