//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Keysight.OpenTap.Sdk.MSBuild
{
    /// <summary>
    /// MSBuild Task to help package plugin. This task is used by the OpenTAP SDK project template
    /// </summary>
    [Serializable]
    public class AddAssemblyReferencesFromPackage : Task
    {
        public string TargetMsBuildFile { get; set; }

        [Output]
        public string[] Assemblies { get; set; }

        public string PackageNames { get; set; }

        [Required]
        public string PackageInstallDir { get; set; }


        Regex dllRx = new Regex("<File +.*Path=\"(?<name>.+\\.dll)\"");
        public override bool Execute()
        {
            List<string> assembliesInPackages = new List<string>();
            if (!String.IsNullOrEmpty(PackageNames))
            {
                foreach (string packageName in PackageNames.Split(';'))
                {
                    string packageDefPath = Path.Combine(PackageInstallDir, "Packages", packageName, "package.xml");
                    if (File.Exists(packageDefPath))
                    {
                        var matches = dllRx.Matches(File.ReadAllText(packageDefPath));
                        foreach (Match m in matches)
                        {
                            if (m.Groups["name"].Success)
                            {
                                string dllPath = m.Groups["name"].Value;
                                if (dllPath.StartsWith("Dependencies"))
                                    continue;
                                string absolutedllPath = Path.Combine(PackageInstallDir, dllPath);
                                if (IsDotNetAssembly(absolutedllPath))
                                    assembliesInPackages.Add(dllPath);
                            }
                        }
                    }
                }
            }
            Log.LogMessage(MessageImportance.Normal, "Found these assemblies in OpenTAP references: " + String.Join(", ", assembliesInPackages));
            if (TargetMsBuildFile != null)
            {
                using (StreamWriter str = File.CreateText(TargetMsBuildFile))
                {
                    str.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"no\"?>");
                    str.WriteLine("<Project ToolsVersion=\"14.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");
                    str.WriteLine("  <ItemGroup>");

                    foreach (string asmPath in assembliesInPackages)
                    {
                        str.WriteLine("    <Reference Include=\"{0}\">", Path.GetFileNameWithoutExtension(asmPath));
                        str.WriteLine("      <HintPath>$(OutDir)\\{0}</HintPath>", asmPath);
                        str.WriteLine("    </Reference>");
                    }
                    str.WriteLine("  </ItemGroup>");
                    str.WriteLine("</Project>");
                }
            }
            Assemblies = assembliesInPackages.ToArray();


            return true;
        }

        private static bool IsDotNetAssembly(string fullPath)
        {
            try
            {
                AssemblyName testAssembly = AssemblyName.GetAssemblyName(fullPath);
                return true;
            }

            catch (Exception)
            {
                return false;
            }
        }
    }
}