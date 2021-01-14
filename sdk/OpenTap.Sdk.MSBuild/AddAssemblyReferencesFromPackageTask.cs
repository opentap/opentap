//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DotNet.Globbing;
using DotNet.Globbing.Token;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
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
            Globber = Glob.Parse(pattern, _globOptions);
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
        public List<GlobWrapper> Globs { get; }

        public static List<GlobTask> Parse(ITaskItem[] tasks)
        {
            var result = new List<GlobTask>();

            foreach (var task in tasks)
            {
                var includeAssemblies = task.GetMetadata("IncludeAssemblies");
                var excludeAssemblies = task.GetMetadata("ExcludeAssemblies");


                var includeGlobs = string.IsNullOrWhiteSpace(includeAssemblies) ? new [] {"**"} : includeAssemblies.Split(':', ';');
                var excludeGlobs = string.IsNullOrWhiteSpace(excludeAssemblies) ? new [] {"Dependencies/**"} : excludeAssemblies.Split(':', ';');
                
                result.Add(new GlobTask(task.ItemSpec, includeGlobs, excludeGlobs));
            }            
            
            return result;
        }
        public string PackageName { get; }

        private GlobTask(string packageName, IEnumerable<string> includePatterns, IEnumerable<string> excludePatterns)
        {
            PackageName = packageName;
            includePatterns = includePatterns.Distinct().Where(x => !string.IsNullOrWhiteSpace(x));
            excludePatterns = excludePatterns.Distinct().Where(x => !string.IsNullOrWhiteSpace(x));
            Globs = includePatterns.Select(x => new GlobWrapper(x, true)).Concat(
                excludePatterns.Select(x => new GlobWrapper(x, false))).ToList();
            Globs.Sort();
        }

        public bool Includes(string file, TaskLoggingHelper log)
        {
            foreach (var glob in Globs)
            {
                try
                {
                    if (glob.Globber.IsMatch(file))
                    {
                        return glob.Include;
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    log.LogError($"Error in glob pattern {glob.Globber.ToString()} when testing {file}");
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
        private HashSet<string> _added;
        private Regex dllRx = new Regex("<File +.*Path=\"(?<name>.+\\.dll)\"");

        [Output] 
        public string[] Assemblies { get; set; }

        [Required]
        public string PackageInstallDir { get; set; }

        public string TargetMsBuildFile { get; set; }
        public Microsoft.Build.Framework.ITaskItem[] OpenTapPackagesToReference { get; set; }
        
        private StringBuilder Result { get; } = new StringBuilder();

        private void Write()
        {
            using (var writer = File.CreateText(TargetMsBuildFile))
            {
                writer.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"no\"?>");
                writer.WriteLine(
                    "<Project ToolsVersion=\"14.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");
                writer.Write(Result.ToString());
                writer.WriteLine("</Project>");
            }
        }
        
        private void WriteItemGroup(IEnumerable<string> assembliesInPackage)
        {
            Result.AppendLine("  <ItemGroup>");

            foreach (var asmPath in assembliesInPackage)
            {
                Result.AppendLine($"    <Reference Include=\"{Path.GetFileNameWithoutExtension(asmPath)}\">");
                Result.AppendLine($"      <HintPath>$(OutDir)/{asmPath}</HintPath>");
                Result.AppendLine("    </Reference>");
            }

            Result.AppendLine("  </ItemGroup>");
        }

        private string ITaskItemString(ITaskItem item)
        {
            var include = item.GetMetadata("IncludeAssemblies");
            var exclude = item.GetMetadata("ExcludeAssemblies");
            var repo = item.GetMetadata("Repository");
            var version = item.GetMetadata("Version");
            
            var result = $@"<OpenTapPackageReference Include=""{item.ItemSpec}"" ";
            
            if (string.IsNullOrWhiteSpace(include) == false)
                result += $@"IncludeAssemblies=""{include}"" ";
            if (string.IsNullOrWhiteSpace(exclude) == false)
                result += $@"ExcludeAssemblies=""{exclude}"" ";
            if (string.IsNullOrWhiteSpace(repo) == false)
                result += $@"Repository=""{repo}"" ";
            if (string.IsNullOrWhiteSpace(version) == false)
                result += $@"Version=""{version}"" ";
            
            result += "/>";

            return result;
        }

        public override bool Execute()
        {
            if (OpenTapPackagesToReference == null || OpenTapPackagesToReference.Length == 0)
            {   // This happens when a .csproj file does not specify any OpenTapPackageReferences -- simply ignore it
                Write(); // write an "empty" file in this case, so msbuild does not think that the task failed, and re runs it endlessly
                Log.LogMessage("Got 0 OpenTapPackageReference targets.");
                return true;
            }
            
            if (TargetMsBuildFile == null)
                throw new Exception("TargetMsBuildFile is null");

            Assemblies = new string[] { };
            try
            {
                // Distincting the tasks is not necessary, but it is a much more helpful warning than 
                // 'No references added from package' which would be given in 'HandlePackage'.
                var distinctTasks = new List<ITaskItem>(OpenTapPackagesToReference.Length);
                foreach (var task in OpenTapPackagesToReference)
                {
                    if (distinctTasks.Contains(task))
                        Log.LogWarning($"Duplicate entry '{ITaskItemString(task)}' detected.");
                    else
                    {
                        distinctTasks.Add(task);
                    }
                }

                if (distinctTasks.Count != OpenTapPackagesToReference.Length)
                    Log.LogWarning("Skipped duplicate entries.");

                var globTasks = GlobTask.Parse(distinctTasks.ToArray());
                Log.LogMessage($"Got {globTasks.Count} OpenTapPackageReference targets.");

                foreach (var task in globTasks)
                {
                    var includeGlobs = task.Globs.Where(x => x.Include).Select(x => x.Globber.ToString());
                    var excludeGlobs = task.Globs.Where(x => x.Include == false).Select(x => x.Globber.ToString());
                    var includeGlobString = string.Join(",", includeGlobs);
                    var excludeGlobString = string.Join(",", excludeGlobs);

                    Log.LogMessage($"{task.PackageName} Include=\"{includeGlobString}\" Exclude=\"{excludeGlobString}\"");

                }

                foreach (var globTask in globTasks)
                {
                    HandlePackage(globTask);
                }
            }
            catch (Exception e)
            {
                Log.LogWarning($"Unexpected error while parsing patterns in '<OpenTapPackageReference>'. If the problem persists, please open an issue and include the build log.");
                Log.LogErrorFromException(e);
                throw;
            }
            finally
            {
                Write(); // Ensure we always write an output file. MSBuild may get confused otherwise.
            }

            return true;
        }

        private void HandlePackage(GlobTask globTask)
        {
            var assembliesInPackage = new List<string>();

            var packageDefPath = Path.Combine(PackageInstallDir, "Packages", globTask.PackageName, "package.xml");
            //  Silently ignore the package if its package.xml does not exist
            if (!File.Exists(packageDefPath))
            {
                Log.LogWarning($"No package named {globTask.PackageName}. Does a glob expression contain a semicolon?");
                return;
            }

            var dllsInPackage = dllRx.Matches(File.ReadAllText(packageDefPath)).Cast<Match>()
                .Where(match => match.Groups["name"].Success)
                .Select(match => match.Groups["name"].Value);

            var matchedDlls = dllsInPackage.Where(x => globTask.Includes(x, Log));
            
            foreach (var dllPath in matchedDlls.Distinct())
            {
                var absolutedllPath = Path.Combine(PackageInstallDir, dllPath);
                
                // Ensure we don't add references twice if they are matched by multiple patterns
                if (_added.Contains(absolutedllPath))
                {
                    Log.LogMessage($"{absolutedllPath} already added. Not adding again.");
                    continue;
                }

                _added.Add(absolutedllPath);

                if (IsDotNetAssembly(absolutedllPath))
                {
                    assembliesInPackage.Add(dllPath);
                }
                else
                {
                    Log.LogWarning($"{absolutedllPath} not recognized as a DotNet assembly. Reference not added.");
                }
            }
            
            Log.LogMessage(MessageImportance.Normal,
                "Found these assemblies in OpenTAP references: " + string.Join(", ", assembliesInPackage));

            if (!assembliesInPackage.Any())
            {
                Log.LogWarning($"No references added from package '{globTask.PackageName}'.");
            }
            else
            {
                WriteItemGroup(assembliesInPackage);
                Assemblies = Assemblies.Concat(assembliesInPackage).ToArray();
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
