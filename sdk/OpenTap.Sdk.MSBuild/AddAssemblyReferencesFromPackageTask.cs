//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DotNet.Globbing;
using DotNet.Globbing.Token;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace Keysight.OpenTap.Sdk.MSBuild
{
    internal class GlobWrapper : IComparable<GlobWrapper>
    {
        public bool Include { get; }

        private static GlobOptions _globOptions = new DotNet.Globbing.GlobOptions
            {Evaluation = {CaseInsensitive = false}};

        public Glob Globber { get; }

        public GlobWrapper(string pattern, bool include)
        {
            Include = include;
            Globber = DotNet.Globbing.Glob.Parse(pattern, _globOptions);
        }

        public int CompareTo(GlobWrapper other)
        {
            bool isLiteral(Glob glob)
            {
                var pattern = glob.ToString();
                return ((pattern.EndsWith(".dll") || pattern.EndsWith(".dll$")) && !(pattern.EndsWith("*.dll") || pattern.EndsWith("*.dll$"))) ||
                       glob.Tokens.All(x => x is LiteralToken);
            }
            
            var t1IsLiteral = isLiteral(Globber);
            var t2IsLiteral = isLiteral(other.Globber);

            // If one is literal, put it before the other;
            if (t1IsLiteral || t2IsLiteral)
            {
                var literalComp = t2IsLiteral.CompareTo(t1IsLiteral);
                if (literalComp != 0)
                    return literalComp;
            }
            
            // Otherwise, put the one with the most tokens first
            var lengthComp = other.Globber.Tokens.Length.CompareTo(Globber.Tokens.Length);
            if (lengthComp != 0)
                return lengthComp;
            
            // Put includes before excludes in case of ties 
            return other.Include.CompareTo(Include);
        }
    }

    internal class GlobTask
    {
        private List<GlobWrapper> _globs;

        public static List<GlobTask> ParseString(string tasks)
        {
            var result = new List<GlobTask>();
            var groups = tasks.Split(',').Where(x => x.Length > 0);
            
            foreach (var group in groups)
            {
                string g = group;
                if (g.StartsWith(";"))
                    g = string.Concat(g.Skip(1));

                var groupParts = g.Split(';').ToArray();
                
                if (groupParts.Length > 3)
                    throw new Exception("Semicolons are not valid in IncludeAssemblies/ExcludeAssemblies patterns.");

                if (groupParts.Length != 3)
                    throw new Exception($"Input '{group}' appears to be invalid.");
                
                // Remove the last character in this string to circumvent disgusting workaround needed in targets file
                groupParts[1] = groupParts[1].Substring(0, groupParts[1].Length - 1);
                var packageName = groupParts[0];
                var includeGlobs = "**";
                if (groupParts.Length > 1 && string.IsNullOrWhiteSpace(groupParts[1]) == false)
                {
                    includeGlobs = groupParts[1].Replace('\\', '/');
                }
                
                // It is not possible to tell the difference between ExcludeAssemblies being unspecified or deliberately set to ""
                // If all dependencies are included, builds are likely to fail -- assume this is not the intention
                string excludeGlobs = "Dependencies/**";
                if (groupParts.Length > 2)
                {
                    if (string.IsNullOrEmpty(groupParts[2]) == false)
                    {
                        excludeGlobs = groupParts[2].Replace('\\', '/');
                    }
                } 

                // If the pattern contained semicolons, they were lost by the split operation above
                result.Add(new GlobTask(packageName, includeGlobs.Split(':'), excludeGlobs.Split(':')));
            }            
            
            return result;
        }
        public string PackageName { get; }

        private GlobTask(string packageName, IEnumerable<string> includePatterns, IEnumerable<string> excludePatterns)
        {
            PackageName = packageName;
            includePatterns = includePatterns.Distinct().Where(x => !string.IsNullOrWhiteSpace(x));
            excludePatterns = excludePatterns.Distinct().Where(x => !string.IsNullOrWhiteSpace(x));
            _globs = includePatterns.Select(x => new GlobWrapper(x, true)).Concat(
                excludePatterns.Select(x => new GlobWrapper(x, false))).ToList();
            _globs.Sort();
        }

        public bool Includes(string file)
        {
            foreach (var glob in _globs)
            {
                if (glob.Globber.IsMatch(file))
                {
                    return glob.Include;
                }
            }

            // Default to excluding something that isn't explicitly included;
            // Note that everything is "explicitly included" when no patterns are specified, since we then default to including the "**" pattern
            return false;
        }
    }

    /// <summary>
    /// MSBuild Task to help package plugin. This task is used by the OpenTAP SDK project template
    /// </summary>
    [Serializable]
    public class AddAssemblyReferencesFromPackage : Task
    {
        public AddAssemblyReferencesFromPackage()
        {
            _added = new HashSet<string>();
        }
        private StreamWriter _writer;
        private HashSet<string> _added;
        private Regex dllRx = new Regex("<File +.*Path=\"(?<name>.+\\.dll)\"");

        [Output] 
        public string[] Assemblies { get; set; }

        [Required]
        public string PackageInstallDir { get; set; }

        public string TargetMsBuildFile { get; set; }
        public string PackagesAndGlobs { get; set; }

        private void InitializeWriter()
        {
            if (TargetMsBuildFile == null)
                throw new Exception("TargetMsBuildFile is null");
            _writer = File.CreateText(TargetMsBuildFile);
            _writer.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"no\"?>");
            _writer.WriteLine("<Project ToolsVersion=\"14.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");
        }
        private void CloseWriter()
        {
            _writer.WriteLine("</Project>");
            _writer.Close();
        }
        
        private void WriteItemGroup(IEnumerable<string> assembliesInPackage)
        {
            _writer.WriteLine("  <ItemGroup>");

            foreach (var asmPath in assembliesInPackage)
            {
                _writer.WriteLine($"    <Reference Include=\"{Path.GetFileNameWithoutExtension(asmPath)}\">");
                _writer.WriteLine($"      <HintPath>$(OutDir)/{asmPath}</HintPath>");
                _writer.WriteLine("    </Reference>");
            }

            _writer.WriteLine("  </ItemGroup>");
        }

        public override bool Execute()
        {
            if (PackagesAndGlobs == "^;,")
            {   // This happens when a .csproj file does not specify any OpenTapPackageReferences -- simply ignore it
                return true;
            }

            Assemblies = new string[] { };
            try
            {
                var globTasks = GlobTask.ParseString(PackagesAndGlobs);
                InitializeWriter();
                Parallel.ForEach(globTasks, HandlePackage);
                CloseWriter();
            }
            catch (Exception e)
            {
                Log.LogWarning($"Unexpected error when parsing patterns in '<OpenTapPackageReference>' ('{PackagesAndGlobs}'). If the problem persists, please open an issue and include the build log.");
                Log.LogErrorFromException(e);
                // throw;
            }

            return true;
        }

        private void HandlePackage(GlobTask globTask)
        {
            var assembliesInPackage = new List<string>();

            var packageDefPath = Path.Combine(PackageInstallDir, "Packages", globTask.PackageName, "package.xml");
            //  Silently ignore the package if its package.xml does not exist
            if (!File.Exists(packageDefPath))
                return;

            var dllsInPackage = dllRx.Matches(File.ReadAllText(packageDefPath)).Cast<Match>()
                .Where(match => match.Groups["name"].Success)
                .Select(match => match.Groups["name"].Value);

            var matchedDlls = dllsInPackage.Where(globTask.Includes);
            
            foreach (var dllPath in matchedDlls.Distinct())
            {
                var absolutedllPath = Path.Combine(PackageInstallDir, dllPath);

                lock (_added)
                {
                    // Ensure we don't add references twice if they are matched by multiple patterns
                    if (_added.Contains(absolutedllPath))
                        continue;
                    _added.Add(absolutedllPath);
                }

                if (IsDotNetAssembly(absolutedllPath))
                    assembliesInPackage.Add(dllPath);
            }
            
            Log.LogMessage(MessageImportance.Normal,
                "Found these assemblies in OpenTAP references: " + string.Join(", ", assembliesInPackage));

            if (!assembliesInPackage.Any())
            {
                Log.LogWarning($"No references added from package '{globTask.PackageName}'.");
            }
            else
            {

                lock (_writer)
                {
                    WriteItemGroup(assembliesInPackage);
                }

                lock (Assemblies)
                {
                    Assemblies = Assemblies.Concat(assembliesInPackage).ToArray();
                }
            }
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
