using System;
using System.Collections.Generic;

namespace OpenTap.Package
{
    /// <summary>
    /// Finds dependencies for specified packages in Package Repositories
    /// </summary>
    public interface IDependencyResolver
    {
        /// <summary>
        /// List of all the dependencies including the specified packages
        /// </summary>
        List<PackageDef> Dependencies { get; }

        /// <summary>
        /// List of the dependencies that are currently not installed
        /// </summary>
        List<PackageDef> MissingDependencies { get; }

        /// <summary>
        /// List of the dependencies that could not be found in the package repositories
        /// </summary>
        List<PackageDependency> UnknownDependencies { get; }

        /// <summary>
        /// List of dependency issues as exceptions. This can for example be version mismatches.
        /// </summary>
        List<Exception> DependencyIssues { get; }

        /// <summary>
        /// Returns the resolved dependency tree
        /// </summary>
        /// <returns>Multi line dependency tree string</returns>
        string GetDotNotation();

        /// <summary>
        /// Start resolving dependencies.
        /// </summary>
        void Resolve();
    }
}