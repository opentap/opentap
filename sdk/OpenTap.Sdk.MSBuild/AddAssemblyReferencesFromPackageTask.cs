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
using System.Xml;
using System.Xml.Linq;
using DotNet.Globbing;
using DotNet.Globbing.Token;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;

namespace Keysight.OpenTap.Sdk.MSBuild
{
    internal class GlobWrapper : IComparable<GlobWrapper>
    {
        internal bool Include { get; }

        private static GlobOptions _globOptions = new DotNet.Globbing.GlobOptions
            {Evaluation = {CaseInsensitive = false}};

        internal Glob Globber { get; }

        internal GlobWrapper(string pattern, bool include)
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
        internal void LogWarningWithLineNumber(string msg)
        {
            Owner.LogWarningWithLineNumber(msg, TaskItem);
        }
        internal List<GlobWrapper> Globs { get; }

        internal static List<GlobTask> Parse(ITaskItem[] tasks,
            AddAssemblyReferencesFromPackage caller)
        {
            var result = new List<GlobTask>();

            foreach (var task in tasks)
            {
                var includeAssemblies = task.GetMetadata("IncludeAssemblies");
                var excludeAssemblies = task.GetMetadata("ExcludeAssemblies");


                var includeGlobs = string.IsNullOrWhiteSpace(includeAssemblies)
                    ? new[] {DefaultInclude}
                    : includeAssemblies.Split(':', ';');
                var excludeGlobs = string.IsNullOrWhiteSpace(excludeAssemblies)
                    ? new[] {DefaultExclude}
                    : excludeAssemblies.Split(':', ';');
                
                result.Add(new GlobTask(task, includeGlobs, excludeGlobs, caller));
            }            
            
            return result;
        }

        internal const string DefaultExclude = "Dependencies/**";

        internal const string DefaultInclude = "**";

        internal string PackageName { get; }
        private ITaskItem TaskItem { get; }
        private AddAssemblyReferencesFromPackage Owner { get; set; }

        private GlobTask(ITaskItem item, IEnumerable<string> includePatterns, IEnumerable<string> excludePatterns,
            AddAssemblyReferencesFromPackage owner)
        {
            this.Owner = owner;
            TaskItem = item;
            PackageName = item.ItemSpec;
            includePatterns = includePatterns.Distinct().Where(x => !string.IsNullOrWhiteSpace(x));
            excludePatterns = excludePatterns.Distinct().Where(x => !string.IsNullOrWhiteSpace(x));
            Globs = includePatterns.Select(x => new GlobWrapper(x, true)).Concat(
                excludePatterns.Select(x => new GlobWrapper(x, false))).ToList();
            Globs.Sort();
        }

        internal bool Includes(string file, TaskLoggingHelper log)
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
                    log.LogError(null, "OpenTAP Reference", null,
                        Owner.SourceFile, Owner.GetLineNumber(TaskItem), 0, 0, 0,
                        $"Error in glob pattern {glob.Globber} when testing against '{file}'");
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
        private XElement _document;
        private BuildVariableExpander Expander { get; set; }

        internal XElement Document
        {
            get
            {
                if (_document != null)
                    return _document;
                
                var expander = new BuildVariableExpander(SourceFile);
                // Expand all the build variables in the document to accurately identify which element corresponds to 'item'
                _document = XElement.Parse(expander.ExpandBuildVariables(File.ReadAllText(SourceFile)),
                    LoadOptions.SetLineInfo);
                return _document;
            }
        }
        
        internal void LogWarningWithLineNumber(string msg, ITaskItem item)
        {
            Log.LogWarning(null, "OpenTAP Reference", null,
                SourceFile, GetLineNumber(item), 0, 0, 0, msg);
        }
        internal int GetLineNumber(ITaskItem item)
        {
            try
            {
                var packageName = item.ItemSpec;
                var version = item.GetMetadata("Version");
                var repository = item.GetMetadata("Repository");
                var includeAssemblies = item.GetMetadata("IncludeAssemblies");
                var excludeAssemblies = item.GetMetadata("ExcludeAssemblies");

                includeAssemblies = string.IsNullOrWhiteSpace(includeAssemblies)
                    ? GlobTask.DefaultInclude
                    : includeAssemblies;
                excludeAssemblies = string.IsNullOrWhiteSpace(excludeAssemblies)
                    ? GlobTask.DefaultExclude
                    : excludeAssemblies;

                foreach (var elem in Document.GetPackageElements(packageName))
                {
                    if (elem is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
                    {
                        var elemVersion = elem.ElemOrAttributeValue("Version", "");
                        var elemRepo = elem.ElemOrAttributeValue("Repository", "");
                        var elemIncludeAssemblies =
                            elem.ElemOrAttributeValue("IncludeAssemblies", GlobTask.DefaultInclude);
                        var elemExcludeAssemblies =
                            elem.ElemOrAttributeValue("ExcludeAssemblies", GlobTask.DefaultExclude);

                        if (elemVersion != version ||
                            elemRepo != repository ||
                            elemIncludeAssemblies != includeAssemblies ||
                            elemExcludeAssemblies != excludeAssemblies)
                            continue;

                        return lineInfo.LineNumber;
                    }
                }
            }
            catch
            {
                // Ignore exception
            }

            return 0;
        }
        public string SourceFile { get; set; }
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
        public ITaskItem[] OpenTapPackagesToReference { get; set; }
        
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

        /// <summary>
        /// Get the preferred path separator, based on what is used in the expansion of '$(OutDir)'.
        /// This is necessary because dotnet core throws up in certain circumstances when path separators are repeatedly flipped
        /// </summary>
        private string Separator { get; set; }
        private bool ShouldAppendSeparator { get; set; }

        private void WriteItemGroup(IEnumerable<string> assembliesInPackage)
        {
            Result.AppendLine("  <ItemGroup>");

            var OutDir = "$(OutDir)";
            if (ShouldAppendSeparator) OutDir += Separator;

            foreach (var asmPath in assembliesInPackage)
            {
                // Replace all path separators the preferred path separator based on '$(OutDir)'.
                var asm = Separator == "\\" ? asmPath.Replace("/", Separator) : asmPath.Replace("\\", Separator);

                Result.AppendLine($"    <Reference Include=\"{Path.GetFileNameWithoutExtension(asmPath)}\">");
                Result.AppendLine($"      <HintPath>{OutDir}{asm}</HintPath>");
                Result.AppendLine("    </Reference>");
            }

            Result.AppendLine("  </ItemGroup>");
        }

        public override bool Execute()
        {
            if (OpenTapPackagesToReference == null || OpenTapPackagesToReference.Length == 0)
            {   // This happens when a .csproj file does not specify any OpenTapPackageReferences -- simply ignore it
                Write(); // write an "empty" file in this case, so msbuild does not think that the task failed, and re runs it endlessly
                Log.LogMessage("Got 0 OpenTapPackageReference targets.");
                return true;
            }


            // Compute some values related to path separators in the generated props file
            {
                Expander = new BuildVariableExpander(SourceFile);
                string[] pathSeparators = { "\\", "/" };
                var outDir = Expander.ExpandBuildVariables("$(OutDir)");
                // A separator should be appended if '$(OutDir)' does not end with a path separator already
                ShouldAppendSeparator = pathSeparators.Contains(outDir.Last().ToString()) == false;
                if (ShouldAppendSeparator)
                {
                    // If it does not end with a path separator, use the last path separator in '$(OutDir)' as the separator.
                    var separatorCharIndex = pathSeparators.Max(outDir.LastIndexOf);
                    Separator = separatorCharIndex > -1 ? outDir[separatorCharIndex].ToString() : "/";
                }
                else Separator = "/";
            }
            
            if (TargetMsBuildFile == null)
                throw new Exception("TargetMsBuildFile is null");
            
            // Task instances are sometimes reused by the build engine -- ensure 'document' is reinstantiated
            _document = null;

            Assemblies = new string[] { };
            try
            {
                // Distincting the tasks is not necessary, but it is a much more helpful warning than 
                // 'No references added from package' which would be given in 'HandlePackage'.
                var distinctTasks = new List<ITaskItem>(OpenTapPackagesToReference.Length);
                foreach (var task in OpenTapPackagesToReference)
                {
                    if (distinctTasks.Contains(task))
                        LogWarningWithLineNumber($"Duplicate entry detected.", task);
                    else
                    {
                        distinctTasks.Add(task);
                    }
                }

                if (distinctTasks.Count != OpenTapPackagesToReference.Length)
                    Log.LogWarning("Skipped duplicate entries.");

                var globTasks = GlobTask.Parse(distinctTasks.ToArray(), this);
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
                // Ensure we always write an output file. MSBuild may get confused otherwise.
                Write();
            }

            return true;
        }

        private void HandlePackage(GlobTask globTask)
        {
            var assembliesInPackage = new List<string>();

            var packageDefPath = Path.Combine(PackageInstallDir, "Packages", globTask.PackageName, "package.xml");

            if (!File.Exists(packageDefPath))
            {
                globTask.LogWarningWithLineNumber(
                    $"No package named '{globTask.PackageName}'.");
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
                    globTask.LogWarningWithLineNumber($"{absolutedllPath} not recognized as a DotNet assembly. Reference not added.");
                }
            }
            
            Log.LogMessage(MessageImportance.Normal,
                "Found these assemblies in OpenTAP references: " + string.Join(", ", assembliesInPackage));

            if (!assembliesInPackage.Any())
            {
                globTask.LogWarningWithLineNumber($"No references added from package '{globTask.PackageName}'.");
            }
            else
            {
                WriteItemGroup(assembliesInPackage);
                Assemblies = Assemblies.Concat(assembliesInPackage).ToArray();
            }
        }

        private bool IsDotNetAssembly(string fullPath)
        {
            try
            {
                AssemblyName testAssembly = AssemblyName.GetAssemblyName(fullPath);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Low, $"Could not load assembly name from '{fullPath}'. {ex}\n{ex.GetType()}\n{ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
    }
}
