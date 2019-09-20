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
using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using OpenTap;

// This file shows how to implement custom behavior behind Import/Export menu choices under the File menu choice.
// In this case, saving a a .zip file.
namespace OpenTap.Plugins.PluginDevelopment
{
    // The class should implement the ITestPlanImort and ITestPlanExport interfaces.
    // Once created, this implementation will be executed if a file with the corresponding file extension is selected.
    public class CompressedFormat : ITestPlanImport, ITestPlanExport
    {
        public void ExportTestPlan(TestPlan plan, string filePath)
        {
            using (var zipPackage = System.IO.Packaging.Package.Open(filePath, FileMode.Create))
            {
                zipPackage.PackageProperties.ContentType = "application/octet-stream";
                var uri = new Uri("Plan", UriKind.Relative);
                var zipPart = zipPackage.CreatePart(PackUriHelper.CreatePartUri(uri), "", CompressionOption.Maximum);
                if (zipPart != null) plan.Save(zipPart.GetStream());
            }
        }

        // Defines will to used this implementation.
        public string Extension
        {
            get { return ".zip"; }
        }
        public string Name
        {
            get { return "Compressed Test Plan"; }
        }
        public TestPlan ImportTestPlan(string filePath)
        {
            using (var zipPackage = PackageOpen(filePath))
            {
                var part = zipPackage.GetParts().FirstOrDefault(pt => pt.Uri.OriginalString.Contains("Plan"));
                if (part != null)
                {
                    return TestPlan.Load(part.GetStream(),filePath);
                }
                return new TestPlan();
            }
        }
        private static System.IO.Packaging.Package PackageOpen(string packagePath)
        {
            return System.IO.Packaging.Package.Open(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}
