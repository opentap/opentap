//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap.Cli;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Xml.Serialization;

namespace OpenTap.Package
{
    /// <summary> SHA1 hashes the files of a TapPackage and includes it in the package.xml file. </summary>
    internal class FileHashPackageAction : ICustomPackageAction
    {
        static TraceSource log = Log.CreateSource("Verify");

        /// <summary> Returns PackageActionStage.Create. </summary>
        public PackageActionStage ActionStage => PackageActionStage.Create;

        internal static byte[] hashFile(string file)
        {
            try
            {
                if (false == File.Exists(file))
                    return Array.Empty<byte>();
                var sha1 = SHA1.Create();
                using (var fstr = File.OpenRead(file))
                    return sha1.ComputeHash(fstr);
            }
            catch (Exception e)
            {
                log.Error("{0}", e.Message);
                log.Debug(e);
                return Array.Empty<byte>();
            }
        }
        /// <summary> SHA1 hash of a file in a package. </summary>
        public class Hash : ICustomPackageData
        {
            /// <summary> Creates a new instance of Hash. </summary>
            public Hash(byte[] hash)
            {
                Value = BitConverter.ToString(hash).Replace("-", "");
            }
            /// <summary> Creates a new instance of Hash. </summary>
            public Hash() => Value = "";
            /// <summary> The Base64 converted hash value. </summary>
            [XmlText]
            public string Value { get; set; }
            byte[] GetBytes()
            {
                if (Value.Length == 40)
                    return StringToByteArray(Value);
                else if (Value.Length == 28)
                    return Convert.FromBase64String(Value);
                else if (Value.Length == 0)
                    return Array.Empty<byte>();
                else
                    throw new FormatException("Value should be a hex or base64 encoded SHA1 hash");
            }

            /// <summary> Compares two hashes and returns true if they are the same. </summary>
            public override bool Equals(object obj)
            {
                if (obj is Hash hsh)
                {
                    return hsh.GetBytes().SequenceEqual(this.GetBytes());
                }

                return false;
            }

            /// <summary> Custom GetHashCode implementation. </summary>
            public override int GetHashCode() => -1937169414 + EqualityComparer<string>.Default.GetHashCode(Value);
        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }


        bool ICustomPackageAction.Execute(PackageDef package, CustomPackageActionArgs customActionArgs)
        {
            package.Files.AsParallel().ForAll(x => x.CustomData.Add(new Hash(hashFile(x.FileName))));
            if (string.IsNullOrEmpty(package.Hash))
            {
                package.Hash = package.ComputeHash();
            }
            return true;
        }

        int ICustomPackageAction.Order() => 1001; // This should come after everything. Sign has 1000 (assumed it would be the last).
    }

    /// <summary> CLI Action to verify the installed packages by checking their hashes. </summary>
    [Display("verify", "Verify the integrity of one or all installed packages by checking their fingerprints.", "package")]
    class VerifyPackageHashes : ICliAction
    {
        static TraceSource log = Log.CreateSource("Verify");
        static string Target = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        /// <summary> Verify a specific package. </summary>
        [UnnamedCommandLineArgument("package", Required = false, Description = "The package to verify the hash for.")]
        public string Package { get; set; }

        int exitCode;

        void verifyPackage(PackageDef pkg)
        {
            log.Debug("Verifying package: {0}", pkg.Name);
            bool ok = true;
            bool inconclusive = false;
            var brokenFiles = new List<(PackageFile, string)>();
            foreach (var file in pkg.Files)
            {
                var fn = Path.GetFileName(file.FileName);
                if (pkg.Name == "OpenTAP" && (fn == "tap.exe" || fn == "tap"))
                    continue;

                var hash = file.CustomData.OfType<FileHashPackageAction.Hash>().FirstOrDefault();
                if (hash == null)
                {
                    // hash not calculated for this package:
                    brokenFiles.Add((file, "is missing checksum information."));
                    inconclusive = true;
                }
                else
                {
                    // Make file.FileName Linux friendly
                    if (file.FileName.Contains('\\'))
                    {
                        string repl = file.FileName.Replace('\\', '/');
                        log.Debug("Replacing '\\' with '/' in {0}", file.FileName);
                        file.FileName = repl;
                    }

                    string fullpath =
                        Path.Combine(pkg.IsSystemWide() ? PackageDef.SystemWideInstallationDirectory : Target,
                            file.FileName);
                    
                    var hash2 = new FileHashPackageAction.Hash(FileHashPackageAction.hashFile(fullpath));
                    if (false == hash2.Equals(hash))
                    {
                        if (File.Exists(fullpath))
                        {
                            brokenFiles.Add((file, "has non-matching checksum."));
                            log.Debug("Hash does not match for '{0}'", file.FileName);
                        }
                        else
                        {
                            brokenFiles.Add((file, "is missing."));
                            log.Debug("File '{0}' is missing", file.FileName);
                        }
                        ok = false;
                    }
                    else
                    {
                        log.Debug("Hash matches for '{0}'", file.FileName);
                    }
                }
            }
            void print_issues()
            {
                foreach (var x in brokenFiles)
                    log.Info("File '{0}' {1}", x.Item1.FileName, x.Item2);
            }
            if (!ok)
            {
                exitCode = (int)PackageExitCodes.InvalidPackageDefinition;
                log.Error("Package '{0}' not verified.", pkg.Name);
                print_issues();
            }
            else
            {
                if (inconclusive)
                {
                    exitCode = (int)PackageExitCodes.InvalidPackageDefinition;
                    log.Warning("Package '{0}' is missing SHA1 checksum for verification.", pkg.Name);
                    print_issues();
                }
                else
                {
                    log.Info("Package '{0}' verified.", pkg.Name);
                }
            }
        }

        int ICliAction.Execute(CancellationToken cancellationToken)
        {
            var installation = new Installation(Target);
            var packages = installation.GetPackages();
            if (string.IsNullOrEmpty(Package))
            {
                foreach (var package in packages)
                    verifyPackage(package);
            }
            else
            {
                var pkg = packages.FirstOrDefault(p => p.Name == Package);
                if (pkg == null)
                {
                    log.Error("Unable to locate package '{0}'", Package);
                    log.Info("Installed packages: {0}", string.Join(", ", packages.Select(x => x.Name)));
                    return (int)PackageExitCodes.InvalidPackageName;
                }
                verifyPackage(pkg);
            }
            return exitCode;
        }

        public static List<(PackageDef Package, PackageFile File, PackageDef OffendingPackage)> CalculatePackageInstallConflicts(IEnumerable<PackageDef> installedPackages, IEnumerable<PackageDef> newPackages)
        {
            var conflicts = new List<(PackageDef, PackageFile, PackageDef)>();
            var allFiles = installedPackages.SelectMany(pkg => pkg.Files.Select(file => (pkg, file)))
                .ToLookup(x => x.file.FileName, StringComparer.CurrentCultureIgnoreCase);

            foreach (var package in newPackages)
            {
                var possibleConflicts = getConflictedFiles(allFiles, package);
                conflicts.AddRange(possibleConflicts
                    // remove conflicts that came from the same package name. These may be overwritten.
                    .Where(x => x.Item1.Name != package.Name)
                    // remove conflicts in files that came from Dependencies. 
                    .Where(x => x.Item2.FileName.StartsWith("Dependencies/", StringComparison.InvariantCultureIgnoreCase) == false)
                    .Select(x => (x.Item1, x.Item2, package)));
            }

            return conflicts;
        }

        static IEnumerable<(PackageDef, PackageFile)> getConflictedFiles(ILookup<string, (PackageDef, PackageFile)> installedFilesLookup, PackageDef package)
        {
            var conflictedFiles = new List<(PackageDef, PackageFile)>();
            foreach (var file in package.Files)
            {
                foreach (var installedFile in installedFilesLookup[file.FileName])
                {
                    var hash1 = installedFile.Item2.CustomData.OfType<FileHashPackageAction.Hash>().FirstOrDefault();
                    // Hash information does not exist. Lets just ignore it then.
                    if (hash1 == null) continue;
                    var hash = file.CustomData.OfType<FileHashPackageAction.Hash>().FirstOrDefault();
                    // Hash information does not exist. Lets just ignore it then.
                    // We also cannot compare the binary files, because they might not have been downloaded yet.
                    if (hash == null) continue;
                    if (hash1.Equals(hash) == false)
                    {
                        // conflict detected!!
                        conflictedFiles.Add(installedFile);
                    }
                }
            }
            return conflictedFiles;
        }
    }
}
