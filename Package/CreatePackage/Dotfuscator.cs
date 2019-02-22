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
using System.Text;

namespace OpenTap.Package
{
    internal class ObfuscatorInput
    {
        public string RelativePath { get; set; }
        public bool IsPublic { get; set; }
        public bool IsLibrary { get; set; }
        public ObfuscatorInput(string relativePath)
        {
            IsPublic = true;//isDll(relativePath);
            IsLibrary = Path.GetExtension(relativePath).ToLowerInvariant().EndsWith(".dll");// IsPublic;

            RelativePath = relativePath;
        }
    }

    [Display("ObfuscateWithDotfuscator")]
    public class DotfuscatorData : ICustomPackageData
    {
    }

    /// <summary>
    /// For obfuscation of .NET DLLs when used with the packager.
    /// </summary>
    [Display("Built-in Dotfuscator runner")]
    public class Dotfuscator : ICustomPackageAction
    {
        protected readonly static TraceSource log =  OpenTap.Log.CreateSource("Dotfuscator");

        const string dotfuscatorCompanyFolder = "PreEmptive Solutions";

        public PackageActionStage ActionStage => PackageActionStage.Create;

        internal static IEnumerable<ObfuscatorInput> CreateInputs(IEnumerable<string> sr)
        {
            foreach (string path in sr)
            {
                yield return new ObfuscatorInput(path);
            }
        }

        /// <summary>
        /// Finds the most recent version of Dotfuscator.exe. Returns null if not found.
        /// </summary>
        /// <returns>Absolute path to Dotfuscator.exe</returns>
        private static string SearchForDotfuscator()
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            string PreEmpFolder = null;
            if (!String.IsNullOrEmpty(programFiles)) // on linux there is no Program Files folder
            {
                PreEmpFolder = Directory.GetDirectories(programFiles, dotfuscatorCompanyFolder).FirstOrDefault();
            }
            if (PreEmpFolder == null && !String.IsNullOrEmpty(programFilesX86))
            {
                PreEmpFolder = Directory.GetDirectories(programFilesX86, dotfuscatorCompanyFolder).FirstOrDefault();
            }
            if (PreEmpFolder == null)
            {
                return null;
            }
            var dirs = Directory.GetDirectories(PreEmpFolder);
            return dirs.Select(d => Path.Combine(d, "Dotfuscator.exe")).FirstOrDefault(File.Exists);
        }


        // To fix the break of WSL versions we have to add the old dependency dir as an extra search dir. This can't be done through the command line, but settings are merged together.
        private string GetConfigFilename(params string[] paths)
        {
            string tap_path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            paths = paths.Concat(Directory.EnumerateDirectories(tap_path, "*", SearchOption.AllDirectories)).Distinct().ToArray();

            var extraPaths = string.Join("", paths.Select(p => string.Format("<file dir=\"{0}\" />", p)));

            string configData = @"<?xml version=""1.0"" encoding=""utf-8"" standalone=""no""?>
<!DOCTYPE dotfuscator SYSTEM ""http://www.preemptive.com/dotfuscator/dtd/dotfuscator_v2.3.dtd"">
<dotfuscator version=""2.3"">
<input>
<loadpaths>
"+extraPaths+@"
</loadpaths>
</input>
</dotfuscator>";
            
            var path = Path.GetTempFileName();
            File.WriteAllText(path, configData);

            return path;
        }

        public int Order()
        {
            return 10;
        }

        public bool Execute(PackageDef package, CustomPackageActionArgs customActionArgs)
        {
            if (!package.Files.Any(s => s.HasCustomData<DotfuscatorData>()))
                return false;

            if (SearchForDotfuscator() == null)
                throw new InvalidOperationException($"Unable to obfuscate using obfuscator. Obfuscator tool not found.");

            var dotfuscatorFiles = new List<string>();
            var extraDirs = new List<string>() { Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) };
            foreach (PackageFile file in package.Files)
            {
                if (!file.HasCustomData<DotfuscatorData>())
                {
                    extraDirs.Add(Path.GetDirectoryName(Path.GetFullPath(file.FileName)));
                    continue;
                }
                string fullPath = Path.GetFullPath(file.FileName);
                dotfuscatorFiles.Add(fullPath);
            }

            string dotfuscatedOutput = Path.Combine(customActionArgs.TemporaryDirectory, "Dotfuscated");

            var programPath = SearchForDotfuscator();

            if (programPath == null) throw new Exception("Could not find Dotfuscator");

            var dotfuscatorInputs = CreateInputs(dotfuscatorFiles);

            var filePaths = dotfuscatorInputs.Select(f => Path.GetDirectoryName(Path.GetFullPath(f.RelativePath))).Concat(extraDirs).Distinct().ToArray();

            string inArgs = "/in:" + String.Join(",", dotfuscatorInputs.Select(file => (file.IsPublic ? "+" : "-") + '"' + file.RelativePath + '"'));
            string outArg = "/out:" + dotfuscatedOutput;
            string allArgs = String.Join(" ", new string[] { inArgs, "/honor:on /smart:on /rename:on /strip:on /keep:namespace -enha:on -cont:high -prune:off", outArg, GetConfigFilename(filePaths) });

            log.Debug("Running Dotfuscator... (This might take a while)");
            int exitCode = ProgramHelper.RunProgram(programPath, allArgs);

            var dotfuscatedFiles = Directory.EnumerateFiles(dotfuscatedOutput).ToList();

            foreach (string dotfuscatedFile in dotfuscatedFiles)
            {
                string filePath = Path.GetFileName(dotfuscatedFile);
                PackageFile inputFile = package.Files.First(file => Path.GetFileName(file.RelativeDestinationPath) == filePath);
                inputFile.FileName = dotfuscatedFile;
            }

            foreach (PackageFile file in package.Files)
            {
                file.RemoveCustomData<DotfuscatorData>();
            }
            return true;
        }
    }
}
