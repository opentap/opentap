//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap.Cli;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Xml.Serialization;

namespace OpenTap.Package
{
    /// <summary> SHA1 hashes the files of a TapPackage and includes it in the package.xml file. </summary>
    class FileHashPackageAction : ICustomPackageAction
    {
        static TraceSource log = Log.CreateSource("Verify");

        /// <summary> Returns PacakgeActionStage.Create. </summary>
        public PackageActionStage ActionStage => PackageActionStage.Create;

        internal static byte[] hashFile(string file)
        {
            try
            {
                if (false == File.Exists(file))
                    return Array.Empty<byte>();
                var sha1 = SHA1CryptoServiceProvider.Create();
                using (var fstr = File.OpenRead(file))
                    return sha1.ComputeHash(fstr);
            }
            catch(Exception e)
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
            public Hash(byte[] hash) => Value = Convert.ToBase64String(hash);
            /// <summary> Creates a new instance of Hash. </summary>
            public Hash() => Value = "";
            /// <summary> The Base64 converted hash value. </summary>
            [XmlText]
            public string Value { get; set; }

            /// <summary> Compares two hashes and returns true if they are the same. </summary>
            public override bool Equals(object obj)
            {
                if(obj is Hash hsh)
                    return hsh.Value == Value;
                return false;
            }

            /// <summary> Custom GetHashCode implementation. </summary>
            public override int GetHashCode() => -1937169414 + EqualityComparer<string>.Default.GetHashCode(Value);
        }

        bool ICustomPackageAction.Execute(PackageDef package, CustomPackageActionArgs customActionArgs)
        {
            package.Files.AsParallel().ForAll(x => x.CustomData.Add(new Hash(hashFile(x.FileName))));
            return true;
        }

        int ICustomPackageAction.Order() => 1001; // This should come after everything. Sign has 1000 (assumed it would be the last).
    }

    /// <summary> CLI Action to verify the installed packages by checking their hashes. </summary>
    [Display("verify", "Verifies installed packages by checking their hashes.", "package")]
    class VerifyPackageHashes : ICliAction
    {
        static TraceSource log = Log.CreateSource("Verify");

        /// <summary> Verify a specific package. </summary>
        [UnnamedCommandLineArgument("Package", Required = false)]
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
                var filename = Path.GetFileName(file.FileName);
                if (pkg.Name == "OpenTAP" &&  (filename == "tap.exe" || filename == "tap" || filename == "tap.dll"))
                {
                    log.Debug("Skipping {0}.", filename);
                    continue;
                }
                var hash = file.CustomData.OfType<FileHashPackageAction.Hash>().FirstOrDefault();
                if(hash == null)
                {
                    // hash not calculated for this package:
                    brokenFiles.Add((file, "is missing checksum information."));
                    inconclusive = true;
                }
                else
                {
                    var hash2 = new FileHashPackageAction.Hash(FileHashPackageAction.hashFile(file.FileName));
                    if (false == hash2.Equals(hash))
                    {
                        if (File.Exists(file.FileName))
                        {
                            brokenFiles.Add((file, "has non-matching checksum."));
                        }
                        else
                        {
                            brokenFiles.Add((file, "is missing."));
                        }
                        ok = false;
                        log.Debug("Hash does not match for '{0}'", file.FileName);
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
                    log.Info("The file '{0}' {1}", x.Item1.FileName, x.Item2);
            }
            if (!ok)
            {
                exitCode = 1;
                log.Error("Package '{0}' not verified.", pkg.Name);
                print_issues();
            }
            else
            {
                if (inconclusive)
                {
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
            var installation = new Installation(Path.GetDirectoryName(typeof(TestPlan).Assembly.Location));
            var packages = installation.GetPackages();
            if (string.IsNullOrEmpty(Package))
            {
                foreach(var package in packages)
                    verifyPackage(package);
            }
            else
            {
                var pkg = packages.FirstOrDefault(p => p.Name == Package);
                if(pkg == null)
                {
                    log.Error("Unable to locate package '{0}'", Package);
                    log.Info("Installed packages are: {0}", string.Join(", ", packages.Select(x => x.Name)));
                    return 1;
                }  
                verifyPackage(pkg);
            }
            return exitCode;
        }
    }
}
