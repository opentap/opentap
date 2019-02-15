using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    public class Installation
    {
        static TraceSource log = Log.CreateSource("Installation");
        string TapPath { get; }

        /// <summary>
        /// Initialize an instance of a TAP installation.
        /// </summary>
        /// <param name="TapPath"></param>
        public Installation(string TapPath)
        {
            this.TapPath = TapPath;
        }

        /// <summary>
        /// Returns a list of installed packages.
        /// </summary>
        /// <returns></returns>
        public List<PackageDef> GetPackages()
        {
            List<PackageDef> plugins = new List<PackageDef>();
            List<string> package_files = new List<string>();

            // this way of seaching for package.xml files will find them both in their 8.x 
            // location (Package Definitions/<PkgName>.package.xml) and in their new 9.x
            // location (Packages/<PkgName>/package.xml)

            // Add normal package from TAP folder
            var packageDir = Path.GetFullPath(Path.Combine(TapPath, PackageDef.PackageDefDirectory));
            if (Directory.Exists(packageDir))
                package_files.AddRange(Directory.GetFiles(packageDir, "*" + PackageDef.PackageDefFileName, SearchOption.AllDirectories));
            var packageDir8x = Path.GetFullPath(Path.Combine(TapPath, "Package Definitions"));
            if (Directory.Exists(packageDir8x))
                package_files.AddRange(Directory.GetFiles(packageDir8x, "*.package.xml"));

            // Add system wide packages
            var systemWidePackageDir = Path.Combine(PackageDef.SystemWideInstallationDirectory, PackageDef.PackageDefDirectory);
            if (Directory.Exists(systemWidePackageDir))
                package_files.AddRange(Directory.GetFiles(systemWidePackageDir, "*" + PackageDef.PackageDefFileName, SearchOption.AllDirectories));
            var systemWidePackageDir8x = Path.Combine(PackageDef.SystemWideInstallationDirectory, "Package Definitions");
            if (Directory.Exists(systemWidePackageDir8x))
                package_files.AddRange(Directory.GetFiles(systemWidePackageDir8x, "*.package.xml"));

            foreach (var file in package_files)
            {
                var package = installedPackageMemorizer.Invoke(file);
                if(package != null && !plugins.Any(s => s.Name == package.Name))
                {
                    package.Location = file;
                    plugins.Add(package);
                }
            }

            return plugins;
        }

        /// <summary>
        /// Get a package definition of OpenTAP engine.
        /// </summary>
        /// <returns></returns>
        public PackageDef GetOpenTapPackage()
        {
            var opentap = GetPackages()?.FirstOrDefault(p => p.Name == "OpenTAP");
            if (opentap == null)
                log.Warning($"Could not find OpenTAP in {TapPath}.");
            
            return opentap;
        }


        IMemorizer<string, PackageDef> installedPackageMemorizer = new Memorizer<string, PackageDef, string>(null, loadPackageDef)
        {
            Validator = file => new FileInfo(file).LastWriteTimeUtc.Ticks
        };
        static PackageDef loadPackageDef(string file)
        {
            try
            {
                using (var f = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    return PackageDef.LoadFrom(f);
            }
            catch (Exception e)
            {
                log.Warning("Unable to read package file '{0}'. Moving it to '.broken'", file);
                log.Debug(e);
                var brokenfile = file + ".broken";
                if (File.Exists(brokenfile))
                    File.Delete(brokenfile);
                File.Move(file, brokenfile);
            }
            return null;
        }
    }
}
