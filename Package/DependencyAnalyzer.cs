//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OpenTap.Package
{
    /// <summary> Type of dependency issue. </summary>
    public enum DependencyIssueType
    {
        /// <summary> No issue noted (Usually notes an installed package). </summary>
        None,
        /// <summary> The dependency is missing. </summary>
        Missing,
        /// <summary> Incompatible version installed. </summary>
        IncompatibleVersion,
        /// <summary> One of the dependencies has a issue.</summary>
        DependencyMissing
    }

    /// <summary>
    /// Model object for a dependency issue.
    /// </summary>
    public struct DependencyIssue
    {
        /// <summary>
        /// Issue package, eg the missing package.
        /// </summary>
        public string PackageName { get; set; }
        /// <summary>
        /// Expected version.
        /// </summary>
        public VersionSpecifier ExpectedVersion { get; set; }

        /// <summary>
        /// The version available.
        /// </summary>
        public SemanticVersion LoadedVersion { get; set; }
        
        /// <summary> Denotes which kind of dependency issue it is. </summary>
        public DependencyIssueType IssueType { get; set; }
    }

    /// <summary>
    /// Algorithm for calculate nth degree dependency issues.
    /// </summary>
    public class DependencyAnalyzer
    {
        /// <summary>
        /// Broken packages from the packages used to build the object.
        /// </summary>
        public ReadOnlyCollection<PackageDef> BrokenPackages { get; set; }
        Dictionary<string, PackageDef> packagesLookup;
        Dictionary<PackageDef, List<PackageDef>> dependers;
        private DependencyAnalyzer() { }

        /// <summary>
        /// Returns the issues for a given package.
        /// </summary>
        /// <param name="pkg"></param>
        /// <returns></returns>
        public List<DependencyIssue> GetIssues(PackageDef pkg)
        {
            List<DependencyIssue> brokenDependencies = new List<DependencyIssue>();
            foreach (var dep in pkg.Dependencies)
            {
                if (packagesLookup.ContainsKey(dep.Name) == false)
                {
                    brokenDependencies.Add(new DependencyIssue() { PackageName = dep.Name, ExpectedVersion = dep.Version, IssueType = DependencyIssueType.Missing });
                    continue;
                }
                
                var installed = packagesLookup[dep.Name];
                var iv = installed.Version;
                var depbase = new DependencyIssue { PackageName = dep.Name, ExpectedVersion = dep.Version, LoadedVersion = iv };
                if (installed.Version == null)
                { // missing
                    depbase.IssueType = DependencyIssueType.Missing;
                }
                else if (!dep.Version.IsCompatible(iv))
                { // incompatible
                    depbase.IssueType = DependencyIssueType.IncompatibleVersion;
                }
                else if (BrokenPackages.Any(pkg2 => pkg2.Name == dep.Name))
                { // broken dependency
                    depbase.IssueType = DependencyIssueType.DependencyMissing;
                }
                else continue;
                brokenDependencies.Add(depbase);
            }
            return brokenDependencies;
        }
        /// <summary> Creates a new dependency analyzer that only shows things related to important_packages. </summary>
        /// <param name="important_packages"></param>
        /// <returns></returns>
        public DependencyAnalyzer FilterRelated(List<PackageDef> important_packages)
        {
            HashSet<string> visited = new HashSet<string>();
            Stack<string> stack = new Stack<string>(important_packages.Select(pkg => pkg.Name));
            // moving down.
            while (stack.Count > 0)
            {
                var top = stack.Pop();
                visited.Add(top);
                var pkg = packagesLookup[top];
                foreach (var dep in pkg.Dependencies)
                {
                    if (visited.Contains(dep.Name) == false)
                    {
                        visited.Add(dep.Name);
                        stack.Push(dep.Name);
                    }
                }
            }

            // moving up
            HashSet<string> visited2 = new HashSet<string>();
            Stack<string> stack2 = new Stack<string>(important_packages.Select(pkg => pkg.Name));
            // moving down.
            while (stack2.Count > 0)
            {
                var top = stack2.Pop();
                visited2.Add(top);
                var pkg = packagesLookup[top];
                foreach (var dep in dependers[pkg])
                {
                    if (visited2.Contains(dep.Name) == false)
                    {
                        visited2.Add(dep.Name);
                        stack2.Push(dep.Name);
                    }
                }
            }

            visited = new HashSet<string>(visited.Concat(visited2));


            return new DependencyAnalyzer
            {
                BrokenPackages = BrokenPackages.Where(pkg => visited.Contains(pkg.Name)).ToList().AsReadOnly(),
                packagesLookup = packagesLookup,
                dependers = dependers
            };
        }

        /// <summary>
        ///  Builds a DependencyTree based on the dependencies between the packages.
        /// </summary>
        /// <param name="packages"></param>
        /// <returns></returns>
        public static DependencyAnalyzer BuildAnalyzerContext(List<PackageDef> packages)
        {
            Dictionary<string, PackageDef> packagesLookup = packages.GroupBy(p => p.Name).Select(p => p.First()).ToDictionary(pkg => pkg.Name);
            Dictionary<PackageDef, List<PackageDef>> dependers = packages.ToDictionary(pkg => pkg, pkg => new List<PackageDef>());
            HashSet<PackageDef> broken_packages = new HashSet<PackageDef>();
            foreach (var package in packages)
            {
                foreach (var dep in package.Dependencies)
                {
                    if (false == packagesLookup.ContainsKey(dep.Name))
                    { // missing: create placeholder. null means missing.
                        packagesLookup[dep.Name] = new PackageDef { Dependencies = new List<PackageDependency>(), Name = dep.Name, Version = null, Files = new List<PackageFile>() };
                        dependers[packagesLookup[dep.Name]] = new List<PackageDef>();
                    }

                    var loadedpackage = packagesLookup[dep.Name];
                    if (false == dep.Version.IsCompatible(loadedpackage.Version) || null == loadedpackage.Version)
                    {
                        broken_packages.Add(package);
                    }

                    dependers[packagesLookup[dep.Name]].Add(package);
                }
            }

            // find nth order broken packages
            // if a dependency is broken, the depender is also broken.
            Stack<PackageDef> newBroken = new Stack<PackageDef>(broken_packages);
            while (newBroken.Count > 0)
            {
                var item = newBroken.Pop();
                var deps = dependers[item];
                foreach (var dep in deps)
                {
                    if (!broken_packages.Contains(dep))
                    {
                        broken_packages.Add(dep);
                        newBroken.Push(dep);
                    }
                }
            }

            DependencyAnalyzer deptree = new DependencyAnalyzer();
            deptree.BrokenPackages = broken_packages.ToList().AsReadOnly();
            deptree.packagesLookup = packagesLookup;
            deptree.dependers = dependers;
            return deptree;
        }
    }
}
