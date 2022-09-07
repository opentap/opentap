//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;

namespace OpenTap.Package
{
    /// <summary>
    /// Finds dependencies for specified packages in Package Repositories
    /// </summary>
    [Obsolete("This class is no longer supported.")]
    public class DependencyResolver
    {
        /// <summary>
        /// List of all the dependencies including the specified packages
        /// </summary>
        public List<PackageDef> Dependencies = new List<PackageDef>();

        /// <summary>
        /// List of the dependencies that are currently not installed
        /// </summary>
        public List<PackageDef> MissingDependencies = new List<PackageDef>();

        /// <summary>
        /// List of the dependencies that could not be found in the package repositories
        /// </summary>
        public List<PackageDependency> UnknownDependencies = new List<PackageDependency>();

        /// <summary>
        /// List of dependency issues as exceptions. This can for example be version mismatches.
        /// </summary>
        public List<Exception> DependencyIssues = new List<Exception>();

        /// <summary>
        /// Instantiates a new dependency resolver.
        /// </summary>
        /// <param name="packages">The packages to resolve dependencies for.</param>
        /// <param name="tapInstallation">The tap installation containing installed packages.</param>
        /// <param name="repositories">The repositories to use for resolving dependencies</param>
        public DependencyResolver(Installation tapInstallation, IEnumerable<PackageDef> packages, List<IPackageRepository> repositories)
        {
        }
    }
}
