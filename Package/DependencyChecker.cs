//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTap.Package
{
    internal static class DependencyChecker
    {
        static TraceSource log =  OpenTap.Log.CreateSource("Packages");

        /// <summary>
        /// detects issues and prints them to the log. use filterPackages if its wanted to filter the issues based on newly installed packages.
        /// </summary>
        public static Issue CheckInstalledPackages(string installDir)
        {
            var packages = new Installation(installDir).GetPackages();
            var tree = DependencyAnalyzer.BuildAnalyzerContext(packages.ToList());
            if (tree.BrokenPackages.Count == 0)
                return Issue.None;
            log.Error("Package Dependency Warning");
            foreach (var pkg in tree.BrokenPackages)
            {
                log.Warning("The Package '{0}' has the following dependency issues:", pkg.Name);
                foreach (var issue in tree.GetIssues(pkg))
                {
                    switch (issue.IssueType)
                    {
                        case DependencyIssueType.Missing:
                            log.Info("  * The package depends on '{0}' (v{1}) which is not installed.", issue.PackageName, issue.ExpectedVersion);
                            break;
                        case DependencyIssueType.DependencyMissing:
                            log.Info("  * The package depends on '{0} which has issues.", issue.PackageName);
                            log.Info("    See message related to other packages.");
                            break;
                        case DependencyIssueType.IncompatibleVersion:
                            log.Info("  * The package depends on '{0}' version '{1}', but '{2}' was installed.", issue.PackageName, issue.ExpectedVersion, issue.LoadedVersion);
                            break;
                    }
                }
            }
            return Issue.BrokenPackages;
        }

        /// <summary>
        /// detects issues and prints them to the log. use filterPackages if its wanted to filter the issues based on newly installed packages.
        /// </summary>
        private static Issue CheckPackages(IEnumerable<PackageDef> packages, IEnumerable<PackageDef> newPackages, LogEventType severity)
        {

            var tree = DependencyAnalyzer.BuildAnalyzerContext(packages.ToList());
            if (newPackages != null)
                tree = tree.FilterRelated(newPackages.ToList());
            if (tree.BrokenPackages.Count == 0)
                return Issue.None;
            foreach (var pkg in tree.BrokenPackages)
            {
                foreach (var issue in tree.GetIssues(pkg))
                {
                    switch (issue.IssueType)
                    {
                        case DependencyIssueType.Missing:
                            log.TraceEvent(severity,0, $"Package '{pkg.Name}' depends on '{issue.PackageName}' which will not be installed.");
                            break;
                        case DependencyIssueType.DependencyMissing:
                            log.TraceEvent(severity, 0, $"Package '{pkg.Name}' depends on '{issue.PackageName} which itself will be broken (See message related to other plugin).");
                            break;
                        case DependencyIssueType.IncompatibleVersion:
                            log.TraceEvent(severity, 0, $"Package '{pkg.Name}' depends on '{issue.PackageName}' version '{issue.ExpectedVersion}', but '{issue.LoadedVersion}' will be installed.");
                            break;
                    }
                }
            }
            return Issue.BrokenPackages;
        }

        static Issue checkPackages(IEnumerable<PackageDef> installedPackages, string[] packages, LogEventType severity)
        {
            var packages_new = new List<PackageDef>();
            foreach (string pkg in packages)
            {
                if (!File.Exists(pkg))
                {
                    log.Warning("Package does not exists.");
                    return Issue.MissingPackage;
                }
                packages_new.Add(PackageDef.FromPackage(pkg));
            }
            
            var new_names = packages_new.Select(pkg => pkg.Name).ToArray();
            var after_installation = packages_new.Concat(installedPackages.Where(pkg => new_names.Contains(pkg.Name) == false)).ToList();
            return CheckPackages(after_installation, packages_new, severity);
        }

        public enum Issue
        {
            None,
            BrokenPackages,
            MissingPackage
        }

        public static Issue CheckDependencies(Installation installation, IEnumerable<string> newPackages, LogEventType severity = LogEventType.Error)
        {
            return checkPackages(installation.GetPackages(), newPackages.Select(Path.GetFullPath).ToArray(), severity);
        }
        
        public static Issue CheckDependencies(IEnumerable<PackageDef> installedPackages, IEnumerable<PackageDef> newPackages, LogEventType severity = LogEventType.Error)
        {
            var newNames = newPackages.Select(pkg => pkg.Name).ToArray();
            var afterInstallation = newPackages.Concat(installedPackages.Where(pkg => newNames.Contains(pkg.Name) == false)).ToList();
            return CheckPackages(afterInstallation, newPackages, severity);
        }
    }
}
