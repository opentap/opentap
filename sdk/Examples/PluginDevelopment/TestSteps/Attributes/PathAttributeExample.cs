//Copyright 2012-2019 Keysight Technologies
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

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Path Attributes Example", Groups: new[] {"Examples", "Plugin Development", "Attributes" },
        Description: "Shows an example of how to use the FilePath and DirectoryPath attribute.")]
    public class PathAttributesExample : TestStep
    {
        // This will create a button that opens a dialog for browsing for files. 
        [FilePath]
        public string MyFilePath { get; set; }

        // This will create a button that opens a dialog for browsing for files. This dialog will also filter for CSV files, with 'any files' filter being optional.
        [FilePath(FilePathAttribute.BehaviorChoice.Open, "csv")]
        public string MyCsvFilePath { get; set; }

        // This will create a button that opens a dialog for browsing for files. This one demonstrates advanced filtering options. It is possible to select between Image file filter, Mp3 file filter and 'all files' filter.
        [FilePath(FilePathAttribute.BehaviorChoice.Open, "Image Files(*.jpg, *.png)|*.jpg;*.png;*.jpeg|Mp3 (Audio) Files (*.mp3) | *.mp3 | All Files | *.*")]
        public string MyVariousMediaFilesPaths { get; set; }

        // This will create a button that opens a dialog for browsing through folders.
        [DirectoryPath]
        public string MyDirectoryPath { get; set; }

        public override void Run()
        {
            // Do Nothing
        }
    }
}
